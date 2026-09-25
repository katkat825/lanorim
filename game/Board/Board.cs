using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Space;
using Godot;

namespace Game.Board
{
    // THE MAP ON THE TABLE. A MapLayout drawn as tiles and walls, with a mini standing on every
    // square somebody is in.
    //
    // REWRITTEN for lanorim. The old build's Board was eleven hundred lines and most of them were
    // the abandoned direction: verbs you reached for, a door you checked with a pool of dice, the
    // hero's own turn run from inside the board. This one draws and reports. It decides nothing -
    // the Encounter in core/Combat is the referee, and this is the felt it is played on.
    //
    // TO LAY OUT: the tile models and materials. BoardTiles takes a PackedScene per kind and falls
    // back to boxes when it has none, so the board is playable before a single Quaternius model is
    // hooked up - and that is exactly the order ART_DIRECTION section 10 asks for.
    public partial class Board : Node3D
    {
        // one square, edge to edge. 60 mm reads as a battle map beside 50 mm dice
        [Export] public float CellSize { get; set; } = 0.06f;

        [Export] public PackedScene MiniScene { get; set; }

        // WHICH FIGURE STANDS ON THE PIECE, by side. This is a STAND-IN for the mini pack, which is
        // Phase 7: docs/v1_minis_map.md pairs a mini with a CLASS for heroes and with a STATBLOCK
        // for monsters (rogue -> Ninja, goblin -> Goblin), and neither of those is a thing an Actor
        // can be asked for today - a hero's Id is the name the player typed, so it is not something
        // to key art off. Allegiance is the one stable distinction there is, so the board can tell
        // a hero from an enemy and nothing finer. Every enemy is therefore the same figure until
        // the pack format lands.
        [Export] public PackedScene HeroFigure { get; set; }

        [Export] public PackedScene EnemyFigure { get; set; }

        [Export] public PackedScene WallModel { get; set; }

        [Export] public PackedScene DoorwayModel { get; set; }

        [Export] public PackedScene RubbleModel { get; set; }

        // the painted-miniature shader, which goes on everything (ART_DIRECTION section 8)
        [Export] public Material Paint { get; set; }

        [Export] public Material WallMaterial { get; set; }

        [Export] public Material DoorMaterial { get; set; }

        [Export] public Material RoughMaterial { get; set; }

        [Export] public Material MatMaterial { get; set; }

        // the wet-erase grid painted on the mat. null and the map is a bare sheet with no squares
        // on it, which is a map you cannot play on
        [Export] public Material LinesMaterial { get; set; }

        public BoardMetrics Metrics { get; private set; } = BoardMetrics.Shipped;

        public MapLayout Map { get; private set; }

        BoardTiles _tiles;
        Node3D _tileRoot;
        Node3D _miniRoot;
        MeshInstance3D _mat;

        Node3D _grid;

        readonly Dictionary<Actor, Mini> _minis = new();

        // the squares held up while the player decides, and the one-off pulse when something
        // happens on a square. two different things, so two nodes
        CellLights _lights;
        CellFlash _flash;

        public override void _Ready()
        {
            _tileRoot = new Node3D { Name = "Tiles" };
            _miniRoot = new Node3D { Name = "Minis" };

            AddChild(_tileRoot);
            AddChild(_miniRoot);

            AddChild(_lights = new CellLights { Name = "Lights" });
            AddChild(_flash = new CellFlash { Name = "Flash" });
        }


        // --- the map ------------------------------------------------------------------------

        // lay a map out. idempotent: laying another one clears the first
        public void Lay(MapLayout map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));

            Metrics = new BoardMetrics(map.Columns, map.Rows, CellSize);

            foreach (Node child in _tileRoot.GetChildren()) child.QueueFree();

            Undress();

            _tiles = new BoardTiles(Metrics)
            {
                Wall = WallMaterial,
                Door = DoorMaterial,
                Rough = RoughMaterial,
                WallModel = WallModel,
                DoorwayModel = DoorwayModel,
                RubbleModel = RubbleModel,
                Paint = Paint,
            };

            _tiles.Build(_tileRoot, map);

            LayTheMat();
        }

        // --- the props ---------------------------------------------------------------------------

        Node3D _propRoot;

        // what stands on the squares: the map builder's props (the barrel, the cauldron), each on its
        // square, fitted to it, turned a quarter at a time, and painted like everything else. A prop
        // whose model was never pulled stands as nothing - the map plays the same without it
        public int Dress(System.Collections.Generic.IEnumerable<Content.Maps.Prop> props)
        {
            Undress();

            if (props == null || Map == null) return 0;

            _propRoot = new Node3D { Name = "Props" };
            AddChild(_propRoot);

            int stood = 0;

            foreach (Content.Maps.Prop prop in props)
            {
                PackedScene scene = PropModels.For(prop.Id);

                if (scene == null) continue;

                var model = scene.Instantiate<Node3D>();
                if (Paint != null) PaintedModel.Paint(model, Paint);

                Aabb bounds = PaintedModel.Bounds(model);
                float scale = PaintedModel.ToFitWidth(bounds, Metrics.CellSize * 0.85f);

                var piece = new Node3D
                {
                    Name = $"{prop.Id}_{prop.Cell.X}_{prop.Cell.Y}",
                    Position = Metrics.Centre(prop.Cell) - new Vector3(0f, bounds.Position.Y * scale, 0f),
                    Rotation = new Vector3(0f, Mathf.Pi * 0.5f * prop.Turn, 0f),
                    Scale = Vector3.One * scale,
                };

                piece.AddChild(model);
                _propRoot.AddChild(piece);
                stood++;
            }

            return stood;
        }

        void Undress()
        {
            _propRoot?.QueueFree();
            _propRoot = null;
        }

        // the mat under the tiles - a plain quad, because the unexplored parts of a map are meant
        // to read as blank table (ART_DIRECTION section 7)
        void LayTheMat()
        {
            _mat ??= new MeshInstance3D { Name = "Mat" };

            if (_mat.GetParent() == null) AddChild(_mat);

            _mat.Mesh = new PlaneMesh { Size = new Vector2(Metrics.Width, Metrics.Depth) };
            _mat.MaterialOverride = MatMaterial;
            _mat.Position = new Vector3(0f, -MatDrop, 0f);

            RuleTheGrid();
        }

        // THE SQUARES, PAINTED ON THE MAT. Thin slabs a whisker above the parchment rather than a
        // texture, so the grid is exactly the grid the rules use - Metrics draws both, and a line
        // cannot drift from the square it divides the way a hand-drawn map image could.
        //
        // ADAPTED from the old build's Board, which had it and this one had not: lanorim's map has
        // been a bare sheet with no squares on it since it was scaffolded. The lines are deliberately
        // FAINT - a wet-erase grid sits under the map, it does not fence it - and the old build
        // records getting this wrong once already: its first pass was so dark the grid won out over
        // the parchment.
        void RuleTheGrid()
        {
            _grid ??= new Node3D { Name = "Grid" };

            if (_grid.GetParent() == null) AddChild(_grid);

            foreach (Node line in _grid.GetChildren()) line.QueueFree();

            if (LinesMaterial == null)
            {
                GD.PushError("board: no grid material - the map will be a sheet with no squares on it");
                return;
            }

            // scaled off the square, not fixed in metres, so a bigger map keeps the same weight of
            // line rather than growing hairlines
            float width = Metrics.CellSize * LineWidth;
            float thick = Metrics.CellSize * LineThickness;

            // THE LINE SITS ENTIRELY ABOVE THE MAT, AND THIS IS THE BUG THAT CAUSED THE CRAWLING.
            //
            // These were centred on y = 0 - so a slab 1.2 mm tall straddled the plane, and the mat
            // at MatDrop below it passed through the slab 0.1 mm from its underside. Two surfaces a
            // tenth of a millimetre apart are inside the depth buffer's precision at this distance,
            // so which one won was decided per pixel per frame; the handheld camera drift then moved
            // the argument around and the grid appeared to crawl.
            //
            // Sitting the slab's BOTTOM a clear gap above the mat is the whole fix. Nothing is
            // coplanar with anything, so there is nothing left to flicker between.
            float bottom = LineClearance * Metrics.CellSize;
            float centre = bottom + thick * 0.5f;

            // one more line than squares: the outside edges are painted too
            var down = new BoxMesh { Size = new Vector3(width, thick, Metrics.Depth + width) };
            var across = new BoxMesh { Size = new Vector3(Metrics.Width + width, thick, width) };

            for (int x = 0; x <= Metrics.Columns; x++)
                _grid.AddChild(Line($"Down{x:00}", down,
                    new Vector3(x * Metrics.CellSize - Metrics.HalfWidth, centre, 0f)));

            for (int y = 0; y <= Metrics.Rows; y++)
                _grid.AddChild(Line($"Across{y:00}", across,
                    new Vector3(0f, centre, y * Metrics.CellSize - Metrics.HalfDepth)));
        }

        // AS A SHARE OF A SQUARE, so the grid is the same weight at any map scale - and wide enough
        // that a line is more than one pixel on screen. At 0.022 a 125 mm square drew a 2.75 mm
        // line, which is about a pixel at the table camera: too thin for the renderer to hold
        // steady, and the reason the grid appeared to crawl. MSAA (project.godot) is the other half
        // of that fix; this is the half that makes the line thick enough to anti-alias.
        //
        // Width is not darkness. The grid is still meant to be FAINT - that lives in the sepia of
        // Mat_lines in board.tscn, not here.
        const float LineWidth = 0.032f;

        // how tall the slab is. It is seen from above, so this only has to be enough to not be a
        // zero-height plane - which would be coplanar with the mat and back to flickering
        const float LineThickness = 0.006f;

        // and how far its underside clears the mat. Also a share of a square, so a map drawn at any
        // scale keeps the same clearance in proportion to everything around it
        const float LineClearance = 0.006f;

        // how far the mat itself sits below the board's own surface plane. Named because the grid
        // has to know it is there to stay clear of it
        public const float MatDrop = 0.0005f;

        // the top of the grid line, which is what anything drawn ON the map has to clear in turn
        public float GridTop => Metrics.CellSize * (LineClearance + LineThickness);

        MeshInstance3D Line(string name, Mesh mesh, Vector3 at) => new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = LinesMaterial,
            Position = at,
        };

        // a door opened mid-fight: the map is immutable, so the fight hands over a new one and
        // only the squares that changed are rebuilt
        public void Reopen(MapLayout map, Border at)
        {
            Map = map;

            _tiles?.Update(_tileRoot, map, at);
        }


        // --- the pieces ---------------------------------------------------------------------

        // put a piece on the board for an actor. the same actor twice moves the piece it has
        public Mini Place(Actor actor, Cell at)
        {
            if (actor == null) return null;

            if (!_minis.TryGetValue(actor, out Mini mini))
            {
                mini = Make(actor);

                if (mini == null) return null;

                _minis[actor] = mini;
            }

            mini.PlaceAt(Metrics.Centre(at));

            return mini;
        }

        // WHICH MODEL A PIECE IS, when something knows better than its allegiance: the fight asks the
        // statblock's mini (MiniModels). Null, or an answer of null, and it is HeroFigure/EnemyFigure
        public System.Func<Actor, (PackedScene Model, float Height)?> FigureFor { get; set; }

        public Mini Of(Actor actor) =>
            actor != null && _minis.TryGetValue(actor, out Mini mini) ? mini : null;

        Mini Make(Actor actor)
        {
            if (MiniScene == null)
            {
                GD.PushError("board: no mini scene - there is nothing to stand on the map");
                return null;
            }

            var mini = MiniScene.Instantiate<Mini>();

            if (mini == null)
            {
                GD.PushError("board: the mini scene is not a Mini");
                return null;
            }

            mini.Name = actor.Id;
            mini.Paint = Paint;

            // set before the piece enters the tree: Mini.Stand runs in _Ready and measures whatever
            // figure it finds, so a model handed over after AddChild would be scaled off the
            // stand-in's height rather than its own
            mini.FigureModel = actor.Side == Allegiance.Hero ? HeroFigure : EnemyFigure;

            if (FigureFor?.Invoke(actor) is { } figure)
            {
                mini.FigureModel = figure.Model;
                mini.FigureHeight = figure.Height;
            }

            _miniRoot.AddChild(mini);

            return mini;
        }

        // A MINI IS AN OBJECT THAT GETS MOVED. It arcs up, travels, sets down - it does not walk,
        // because a painted miniature has no legs that work (ART_DIRECTION section 5).
        public bool Walk(Actor actor, IReadOnlyList<Cell> route)
        {
            Mini mini = Of(actor);

            if (mini == null || route == null || route.Count < 2) return false;

            mini.Follow(route.Select(Metrics.Centre).ToList());

            return true;
        }

        public void Strike(Actor attacker, Actor target)
        {
            Mini mini = Of(attacker);
            Mini at = Of(target);

            if (mini == null || at == null) return;

            mini.Strike(at.Position);
        }

        // it took a condition: a wobble and a scuff, so the player reads the state off the figure
        // and not only off a bar
        public void Wobble(Actor actor) => Of(actor)?.Wobble();

        // and it went over. THE BODY STAYS ON THE MAP
        public void Topple(Actor actor) => Of(actor)?.Topple();

        public void Clear(Actor actor)
        {
            if (actor == null || !_minis.Remove(actor, out Mini mini)) return;

            mini.QueueFree();
        }

        public void ClearAll()
        {
            foreach (Mini mini in _minis.Values) mini.QueueFree();

            _minis.Clear();
        }


        // --- lighting squares up ----------------------------------------------------------------

        // every square this actor could reach this turn. the UI paints them and the player clicks
        // one; the rules already worked out which they are
        public void ShowReach(Battlefield field, Actor actor, int squares, Color colour)
        {
            if (field == null || actor == null) return;

            Flash(field.Reachable(actor, squares).Keys, colour);
        }

        // and what a burst would catch, before it is cast
        public void ShowBurst(Battlefield field, Cell centre, int radius, Color colour)
        {
            if (field == null) return;

            Flash(field.Burst(centre, radius), colour);
        }

        public void Flash(IEnumerable<Cell> cells, Color colour)
        {
            _lights?.Show(cells.Select(Metrics.Centre).ToList(), Metrics.CellSize, colour);
        }

        public void Unflash() => _lights?.Clear();

        // one square, pulsing once, because something happened on it
        public void Pulse(Cell at) => _flash?.Show(Metrics.Centre(at), Metrics.CellSize);


        // --- what the mouse is over ---------------------------------------------------------------

        // the square under a screen point, or null when the ray misses the mat entirely. a plane,
        // not a raycast against the tiles: the point is the SQUARE, and a square with nothing on it
        // still has to be clickable
        public Cell? CellUnder(Vector2 at)
        {
            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return null;

            Vector3 from = camera.ProjectRayOrigin(at);
            Vector3 along = camera.ProjectRayNormal(at);

            var mat = new Plane(GlobalTransform.Basis.Y.Normalized(), GlobalTransform.Origin);

            Vector3? hit = mat.IntersectsRay(from, along);

            if (!hit.HasValue) return null;

            Cell cell = Metrics.At(ToLocal(hit.Value));

            return Map != null && Map.Contains(cell) ? cell : null;
        }

        public Vector3 Where(Cell cell) => Metrics.Centre(cell);

        public override string ToString() =>
            Map == null ? "an empty board" : $"{Metrics}, {_minis.Count} pieces";
    }
}
