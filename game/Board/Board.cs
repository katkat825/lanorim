using System.Collections.Generic;
using Core.Characters;
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

        // where walls turn, end or meet (BoardTiles.Pillars.cs)
        [Export] public PackedScene PillarModel { get; set; }

        // THE MODELLED WALLS' HEIGHT, in metres (cc_task_working-notes-10-01.md 2.5: "about mini height"; the
        // hero stands 0.156, a monster 0.1875), and a cut-away wall's: one between the camera and floor it would
        // hide comes down to WallLow, and goes back up when the camera comes round (Cutaway.cs)
        [Export] public float WallTall { get; set; } = 0.16f;

        [Export] public float WallLow { get; set; } = 0.03f;

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

        public override string ToString() =>
            Map == null ? "an empty board" : $"{Metrics}, {_minis.Count} pieces";
    }
}
