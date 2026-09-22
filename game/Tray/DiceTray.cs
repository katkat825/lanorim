using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
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

        // how many dice the scene can put on the felt at once
        public int Seats => _dice.Count;

        public override void _Ready()
        {
            _dice.AddRange(GetNode(DiceRootPath).GetChildren().OfType<DieBody>());
            _throwPoints.AddRange(GetNode(ThrowPointsRootPath).GetChildren().OfType<Node3D>());

            // gathered before the skin, which hands each die a voice built from the tray it lands in
            _voices.AddRange(_dice.Select(d => d.GetChildren().OfType<DieAudio>().FirstOrDefault()));

            if (_dice.Count != _throwPoints.Count)
                GD.PushError($"dice tray: {_dice.Count} dice but {_throwPoints.Count} throw points " +
                             "- they pair by index");

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


        // --- measuring the scene ------------------------------------------------------------

        // measured off the scene in this node's space, so the tray can be parented anywhere and
        // still know its own shape. walls contribute only their thickness - they lean outward, so
        // the usable felt is the floor they stand on, not the gap between them
        TrayBounds Measure()
        {
            _floorBody ??= GetNodeOrNull<StaticBody3D>(TrayFloorPath);
            _wallsBody ??= GetNodeOrNull<StaticBody3D>(TrayWallsPath);

            (CollisionShape3D shape, BoxShape3D box) = FirstBox(_floorBody);

            if (box == null)
            {
                GD.PushError($"dice tray: no box-shaped floor under '{TrayFloorPath}' to measure - " +
                             $"falling back to the authored tray, {TrayBounds.Shipped}");
                return TrayBounds.Shipped;
            }

            // the top of the floor box, not its centre: dice rest on the felt, not inside it
            float feltY = ToLocal(shape.GlobalPosition).Y + box.Size.Y * 0.5f;

            var measured = new TrayBounds(box.Size.X * 0.5f, box.Size.Z * 0.5f, feltY,
                                          WallThickness());

            if (measured.IsUsable) return measured;

            GD.PushError($"dice tray: measured a tray with no felt inside it ({measured}) - " +
                         $"falling back to the authored tray, {TrayBounds.Shipped}");

            return TrayBounds.Shipped;
        }

        // the thinnest horizontal wall dimension: the thin side is the one that eats into the felt
        float WallThickness()
        {
            float thinnest = float.MaxValue;

            foreach (CollisionShape3D shape in Shapes(_wallsBody))
                if (shape.Shape is BoxShape3D box)
                    thinnest = Mathf.Min(thinnest, Mathf.Min(box.Size.X, box.Size.Z));

            if (thinnest < float.MaxValue) return thinnest;

            GD.PushError($"dice tray: no box-shaped walls under '{TrayWallsPath}' to measure - " +
                         "taking the authored thickness");

            return TrayBounds.Shipped.WallThickness;
        }

        static (CollisionShape3D Shape, BoxShape3D Box) FirstBox(Node body)
        {
            foreach (CollisionShape3D shape in Shapes(body))
                if (shape.Shape is BoxShape3D box) return (shape, box);

            return (null, null);
        }

        // walks the tree rather than naming nodes, so a tray with more walls still measures
        static IEnumerable<CollisionShape3D> Shapes(Node node)
        {
            if (node == null) yield break;

            foreach (Node child in node.GetChildren())
            {
                if (child is CollisionShape3D shape) yield return shape;

                foreach (CollisionShape3D deeper in Shapes(child)) yield return deeper;
            }
        }

        void Bind(DieBody die)
        {
            die.TraySpace = this;
            die.LostBelowY = _bounds.LostBelowY;
            die.LostRadius = _bounds.LostRadius;
        }


        // --- the skin ------------------------------------------------------------------------

        public string SkinName =>
            Skin?.ResourcePath is { Length: > 0 } path ? path.GetFile().GetBaseName() : "none";

        // idempotent; physics and look come off one TraySurface, so a felt look with plank bounce
        // is impossible by construction
        void ApplySkin()
        {
            _floorBody ??= GetNodeOrNull<StaticBody3D>(TrayFloorPath);
            _wallsBody ??= GetNodeOrNull<StaticBody3D>(TrayWallsPath);

            if (_floorBody == null || _wallsBody == null)
            {
                GD.PushError($"dice tray: the tray needs two StaticBody3D at '{TrayFloorPath}' and " +
                             $"'{TrayWallsPath}' - physics materials belong to bodies, not shapes");
                return;
            }

            if (Skin == null)
            {
                GD.PushError("dice tray: no skin - the tray will be untextured and will bounce " +
                             "like Godot's default");
                return;
            }

            Dress(_floorBody, Skin.Floor, "floor");
            Dress(_wallsBody, Skin.Walls, "walls");

            // one voice for the whole tray: it holds nothing per-die
            var voice = new SurfaceVoice(Skin);

            foreach (DieAudio audio in _voices) if (audio != null) audio.Voice = voice;
        }

        static void Dress(StaticBody3D body, TraySurface surface, string which)
        {
            if (surface == null)
            {
                GD.PushError($"dice tray: the skin has no {which} surface");
                return;
            }

            body.PhysicsMaterialOverride = surface.Physics;

            foreach (MeshInstance3D mesh in Meshes(body)) mesh.MaterialOverride = surface.Material;
        }

        static IEnumerable<MeshInstance3D> Meshes(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh) yield return mesh;

                foreach (MeshInstance3D deeper in Meshes(child)) yield return deeper;
            }
        }

        // a felt chosen off the table. materials are cosmetic and always will be
        // (ART_DIRECTION section 6): no tray and no die grants a bonus
        public bool Wear(string name)
        {
            TraySkin wearing = TraySkin.Load(name);

            if (wearing == null)
            {
                GD.PushWarning($"dice tray: there is no skin called '{name}' in {TraySkin.Folder} " +
                               $"- the tray keeps {SkinName}");
                return false;
            }

            Skin = wearing;
            ApplySkin();

            GD.Print($"tray    {SkinName}");

            // every skin needs its own fairness sweep: bounce decides how a die settles
            return true;
        }


        // --- throwing -------------------------------------------------------------------------

        // THE ONE WAY IN. Hand it the dice the rules want thrown, in the order the rules will read
        // them, and the answer comes back through Rolled.
        //
        //     tray.Throw(Die.D20);                       a check, a save, an attack
        //     tray.Throw(Die.D20, Die.D20);              advantage - both are thrown and shown
        //     tray.Throw(Die.D8, Die.D8, Die.D8);        damage
        public bool Throw(params Die[] dice)
        {
            if (dice == null || dice.Length == 0)
            {
                GD.PushError("dice tray: asked to throw nothing");
                return false;
            }

            foreach (Die die in dice)
            {
                if (die.IsReal() && die != Die.D100) continue;

                GD.PushError($"dice tray: {die.Label()} is not a solid the tray can throw " +
                             "(a d100 is two d10s)");
                return false;
            }

            if (IsThrowing)
            {
                GD.PushError("dice tray: asked to throw while a throw is still in the air");
                return false;
            }

            // MORE DICE THAN THE TRAY SEATS IS NOT AN ERROR, IT IS A BIG SPELL. Meteor Swarm is
            // 20d6 and no tray holds twenty dice comfortably, so the tray does what a person does:
            // throws a handful, writes the numbers down, and throws the rest. The faces come back
            // in one TrayRoll in the order they were asked for, so nothing downstream can tell.
            _queued.Clear();
            _read.Clear();

            for (int i = _dice.Count; i < dice.Length; i += _dice.Count)
                _queued.Add(dice.Skip(i).Take(_dice.Count).ToArray());

            Handful(dice.Take(_dice.Count).ToArray());

            return true;
        }

        // the handfuls still to throw, and the faces read from the ones already thrown
        readonly List<Die[]> _queued = new();
        readonly List<Felt> _read = new();

        void Handful(Die[] dice)
        {
            Seat(dice.Length);

            for (int i = 0; i < _active.Count; i++) _active[i].Size = dice[i];

            ThrowAll();
        }

        public bool Throw(DiceRoll roll) =>
            roll.RollsAnything && Throw(Enumerable.Repeat(roll.Die, roll.Count).ToArray());

        // how many dice play; the rest are benched - frozen, hidden and off the collision layers -
        // so a stale last face can never be read as part of a throw nobody made
        void Seat(int count)
        {
            count = Mathf.Clamp(count, 0, _dice.Count);

            _active.Clear();

            for (int i = 0; i < _dice.Count; i++)
            {
                DieBody die = _dice[i];

                if (i < count)
                {
                    _active.Add(die);
                    Unbench(die);
                }
                else
                {
                    Bench(die);
                }
            }
        }

        void Bench(DieBody die)
        {
            if (_benched.ContainsKey(die)) return;

            _benched[die] = (die.CollisionLayer, die.CollisionMask);

            die.CollisionLayer = 0;
            die.CollisionMask = 0;
            die.Freeze = true;
            die.Visible = false;
        }

        void Unbench(DieBody die)
        {
            if (!_benched.TryGetValue(die, out (uint Layer, uint Mask) was)) return;

            die.CollisionLayer = was.Layer;
            die.CollisionMask = was.Mask;
            die.Freeze = false;
            die.Visible = true;

            _benched.Remove(die);
        }

        void ThrowAll()
        {
            _awaitingSettle = true;
            _pending.Clear();

            // captioned once a throw, not once a bounce: a die strikes a wooden tray a dozen times
            // between the hand and the felt, and what a deaf player needs to know is that the dice
            // went up
            Captions?.Says(Sound.Dice);

            for (int i = 0; i < _active.Count; i++)
            {
                Transform3D from = _throwPoints[i].GlobalTransform;

                _voices[i]?.Rattle(from.Origin);

                _pending.Add((_active[i], from, RattleLeadTicks + i * LaunchStaggerTicks));
            }
        }

        // set by the table so a throw is captioned; null when nobody is listening
        public Game.Access.Captioned Captions { get; set; }

        public override void _PhysicsProcess(double delta)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                (DieBody die, Transform3D from, int delay) = _pending[i];

                if (delay > 0)
                {
                    _pending[i] = (die, from, delay - 1);
                    continue;
                }

                die.Throw(from);
                _pending.RemoveAt(i);
            }
        }

        public override void _Process(double delta)
        {
            // belt and braces: a die knocked loose by another's landing moves without re-arming its
            // own flight tracking, so no Settled signal is coming for it
            if (_awaitingSettle) TryRead();
        }

        // waits on IsAtRest as well as IsSettled, so a die bumped after settling is not read
        // mid-tumble
        void TryRead()
        {
            if (!_awaitingSettle || _pending.Count > 0) return;

            if (!_active.All(d => d.IsSettled && d.IsAtRest)) return;

            _awaitingSettle = false;

            // read the faces live rather than each die's latched value, so a die nudged flat after
            // settling is read as it is now
            var felt = _active.Select(d =>
            {
                (int value, float alignment) = d.ReadFace();

                return new Felt(d.Size, value, alignment);
            }).ToList();

            // developer diagnostic, exempt from localization
            GD.Print("tray    " + string.Join(", ", _active.Select(
                (d, i) => $"{d.Name} {d.Size.Label()} {felt[i].Value} ({felt[i].Alignment:0.00})")));

            _read.AddRange(felt);

            // another handful to go: the answer is not owed until the last of them is still
            if (_queued.Count > 0)
            {
                Die[] next = _queued[0];
                _queued.RemoveAt(0);

                Handful(next);
                return;
            }

            Last = new TrayRoll(_read);

            Rolled?.Invoke(Last);
        }
    }
}
