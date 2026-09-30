using System;
using Core.Space;
using Godot;

namespace Game.Board
{
    public partial class Board
    {
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
                if (Paint != null) PaintedModel.Paint(model, Paint, "prop");

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

        // THE MAT RUNS A RING OF SQUARES PAST THE MAP on every side, ruled like the rest: a
        // wet-erase mat is bigger than what is drawn on it (Kathleen, 2026-09-28). The ring is
        // table, not map - nothing stands or moves there but a mini set aside
        public const int Margin = 1;

        public float MatWidth => Metrics.Width + 2 * Margin * Metrics.CellSize;

        public float MatDepth => Metrics.Depth + 2 * Margin * Metrics.CellSize;

        // the mat under the tiles - a plain quad, because the unexplored parts of a map are meant
        // to read as blank table (ART_DIRECTION section 7)
        void LayTheMat()
        {
            _mat ??= new MeshInstance3D { Name = "Mat" };

            if (_mat.GetParent() == null) AddChild(_mat);

            _mat.Mesh = new PlaneMesh { Size = new Vector2(MatWidth, MatDepth) };
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

            // one more line than squares: the outside edges are painted too - the mat's, a ring past
            // the map's
            var down = new BoxMesh { Size = new Vector3(width, thick, MatDepth + width) };
            var across = new BoxMesh { Size = new Vector3(MatWidth + width, thick, width) };

            for (int x = -Margin; x <= Metrics.Columns + Margin; x++)
                _grid.AddChild(Line($"Down{x + Margin:00}", down,
                    new Vector3(x * Metrics.CellSize - Metrics.HalfWidth, centre, 0f)));

            for (int y = -Margin; y <= Metrics.Rows + Margin; y++)
                _grid.AddChild(Line($"Across{y + Margin:00}", across,
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
    }
}
