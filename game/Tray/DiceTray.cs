using System;
using System.Collections.Generic;
using System.Linq;
using Game.Audio;
using Game.Dice;
using Godot;

namespace Game.Tray
{
    // The tray is an object, not a room. It owns the felt, the walls, the dice in it, and the one
    // question the rules ask it: throw these, and tell me what they came up.
    //
    // ADAPTED from the old build's DiceTray. The vessel is the same - measure the scene, dress it
    // from a skin, stagger the launch, wait for every die to be both settled and still. What went
    // is everything that assumed a three-die pool: roles, Impact, Snag cues, the marks, the
    // companion's nose, picking a die to re-throw. What arrived is a d20.
    //
    // Nothing here decides a rule. It hands back a TrayRoll and the rules read it.
    public partial class DiceTray : Node3D
    {
        [Export] public NodePath DiceRootPath { get; set; } = "Dice";

        [Export] public NodePath ThrowPointsRootPath { get; set; } = "ThrowPoints";

        // two bodies, not one: physics_material_override lives on the StaticBody3D, so a felt
        // floor and wooden walls have to be two bodies or they share one bounce
        [Export] public NodePath TrayFloorPath { get; set; } = "TrayFloor";

        [Export] public NodePath TrayWallsPath { get; set; } = "TrayWalls";

        [Export] public TraySkin Skin { get; set; }

        // dice launched on the same frame collide in the air, which is where most cocked landings
        // came from in the old build. a couple of ticks apart and they arrive as a handful
        [Export] public int LaunchStaggerTicks { get; set; } = 3;

        // the rattle starts before the die leaves the hand, because that is the order the sound
        // happens in at a table
        [Export] public int RattleLeadTicks { get; set; } = 14;

        // the answer. fired once a throw, after every die is still
        public event Action<TrayRoll> Rolled;

        readonly List<DieBody> _dice = new();
        readonly List<Node3D> _throwPoints = new();
        readonly List<DieAudio> _voices = new();
        readonly List<DieBody> _active = new();

        readonly Dictionary<DieBody, (uint Layer, uint Mask)> _benched = new();
        readonly List<(DieBody Die, Transform3D From, int Delay)> _pending = new();

        bool _awaitingSettle;

        StaticBody3D _floorBody;
        StaticBody3D _wallsBody;

        TrayBounds _bounds = TrayBounds.Shipped;

        public TrayBounds Bounds => _bounds;

        public TrayRoll Last { get; private set; }

        // true while a throw is in the air and its answer is still owed - including the handfuls
        // of a big spell that have not gone up yet
        public bool IsThrowing => _awaitingSettle || _pending.Count > 0 || _queued.Count > 0;

        // how many dice go on the felt at once: every die the scene seats, or MostAtOnce of them
        public int Seats => MostAtOnce > 0 ? Math.Min(MostAtOnce, _dice.Count) : _dice.Count;

        public override void _Ready()
        {
            _dice.AddRange(GetNode(DiceRootPath).GetChildren().OfType<DieBody>());
            _throwPoints.AddRange(GetNode(ThrowPointsRootPath).GetChildren().OfType<Node3D>());

            // gathered before the skin, which hands each die a voice built from the tray it lands in
            _voices.AddRange(_dice.Select(d => d.GetChildren().OfType<DieAudio>().FirstOrDefault()));

            if (_dice.Count != _throwPoints.Count)
                GD.PushError($"dice tray: {_dice.Count} dice but {_throwPoints.Count} throw points " +
                             "- they pair by index");

            // a smaller tray round the same dice, if one is asked for (DiceTray.Sizing)
            Resize();

            // the skin sets the friction and bounce, so it goes on before anything is thrown
            ApplySkin();

            // measure and bind before any throw: a die not told where the tray is measures its
            // escape from the world origin and declares itself lost on the first bounce
            _bounds = Measure();

            foreach (DieBody die in _dice) Bind(die);

            GD.Print($"tray    {_bounds}");
            GD.Print($"tray    {SkinName}");

            foreach (DieBody die in _dice) die.Settled += _ => TryRead();

            Seat(0);
        }
    }
}
