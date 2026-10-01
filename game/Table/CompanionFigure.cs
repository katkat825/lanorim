using System.Linq;
using Content.Companions;
using Core.Dice;
using Game.Board;
using Godot;

namespace Game.Table
{
    // THE COMPANION ON THE TABLE (cc_task_ui-issues-9-30.md 6.4, cc_task_ui-issues-10-01.md 4): a Quaternius
    // animal, mini-sized, NEVER ON THE MAP. It stands on the table above the map (the far side, toward the GM
    // screen), a little to the right of the screen, with open table round it, and it is alive: it idles, walks
    // between spots near its home now and then (Roaming), and settles its head after a while still. What it
    // does is decided in content, where it is tested: which idle comes next is Idling's deal, where it walks
    // is Roaming's, how it feels is CompanionMind's (it watches the fight and the hero's rolls, PlayDirector).
    // This only plays it.
    //
    // It also clears the board: a fallen foe topples, and the companion lunges at it and it skitters off the
    // mat (Mini.Skitter). When the dialogue gives it a line (the story's "companion" speaker), it looks up.
    //
    // Every clip name, size, distance and time is an export: Kathleen's to tune. A clip is a list, the first
    // the model has wins, so the dogs' Idle_2_HeadLow and the deer's Idle_Headlow are both "head low".
    public partial class CompanionFigure : Node3D
    {
        // how tall it stands, in metres: the board's wolf mini is 0.11
        [Export] public float Height { get; set; } = 0.11f;

        // its home, from the GM screen's right-hand front corner, in metres (+X right, +Z toward the map): in the
        // gap between the screen and the map, a little right of the screen
        [Export] public Vector3 FromGmScreen { get; set; } = new Vector3(0.18f, 0f, 0.12f);

        // how far from home it wanders, and never nearer the map's far edge than this
        [Export] public float WanderRadius { get; set; } = 0.12f;

        [Export] public float MapClearance { get; set; } = 0.06f;

        // seconds between walks (give or take half the jitter), and its walking speed in metres a second
        [Export] public float WalkEvery { get; set; } = 9f;

        [Export] public float WalkJitter { get; set; } = 4f;

        [Export] public float WalkSpeed { get; set; } = 0.05f;

        // seconds standing still before it settles (head low); the pack has no sit or lie-down clip
        [Export] public float SettleAfter { get; set; } = 14f;

        // the model's own forward, in degrees about the table's up (Quaternius animals face +Z)
        [Export] public float ForwardDegrees { get; set; } = 0f;

        [Export] public string[] IdleClips { get; set; } = { "Idle", "Idle_2", "Eating" };

        [Export] public string[] WalkClip { get; set; } = { "Walk" };

        [Export] public string[] SettleClip { get; set; } = { "Idle_2_HeadLow", "Idle_Headlow" };

        [Export] public string[] WatchingClip { get; set; } = { "Idle_2" };

        [Export] public string[] AlertClip { get; set; } = { "Idle_HitReact1" };

        [Export] public string[] PleasedClip { get; set; } = { "Jump_ToIdle" };

        // the hero is down, or a rest: it lies its head low beside the board
        [Export] public string[] LieClip { get; set; } = { "Idle_2_HeadLow", "Idle_Headlow" };

        [Export] public string[] SpeakClip { get; set; } = { "Idle_HitReact2" };

        [Export] public string[] HuffClip { get; set; } = { "Attack", "Attack_Headbutt" };

        // seconds it lies down for a rest
        [Export] public float RestSeconds { get; set; } = 4f;

        public string Companion { get; private set; } = "";

        Node3D _model;
        AnimationPlayer _player;
        Idling _idling;
        Roaming _roaming;
        Vector3 _lookAt;
        Mood _mood = Mood.Calm;
        double _gesture;
        double _resting;
        bool _settled;

        // wears the class's companion, painted like the minis (paint may be null); none, and it is gone
        public void Wear(string companion, Material paint)
        {
            _model?.QueueFree();
            _model = null;
            _player = null;
            Companion = companion ?? "";

            PackedScene scene = CompanionModels.For(companion);
            Visible = scene != null;

            if (scene == null) return;

            _model = scene.Instantiate<Node3D>();
            AddChild(_model);

            if (paint != null) PaintedModel.Paint(_model, paint, "companion");

            Aabb box = PaintedModel.Bounds(_model);
            if (box.Size.Y > 0.0001f) _model.Scale = Vector3.One * (Height / box.Size.Y);

            _model.RotationDegrees = new Vector3(0f, ForwardDegrees, 0f);

            _player = Nodes.Under<AnimationPlayer>(_model).FirstOrDefault();
            _idling = new Idling(IdleClips.Length, new SeededRng(System.Environment.TickCount));
            Play(Idle());

            GD.Print($"companion  {companion}: {scene.ResourcePath.GetFile()}, " +
                     $"{(_player == null ? "no clips" : _player.GetAnimationList().Length + " clips")}");
        }

        // the first of a clip's names the model has, by its short name, whatever its library
        string Full(string[] clip) =>
            _player == null || clip == null
                ? null
                : clip.Select(c => _player.GetAnimationList()
                                          .FirstOrDefault(a => a.ToString().Equals(c, System.StringComparison.OrdinalIgnoreCase) ||
                                                               a.ToString().EndsWith("/" + c, System.StringComparison.OrdinalIgnoreCase)))
                      .FirstOrDefault(a => a != null);

        void Play(string[] clip, bool loop = true)
        {
            string full = Full(clip);
            if (full == null) return;

            Animation animation = _player.GetAnimation(full);
            if (animation != null) animation.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;

            _player.Play(full, 0.25);
        }

        string[] Idle() => IdleClips.Length == 0
            ? null
            : new[] { IdleClips[System.Math.Clamp((_idling?.Idle ?? 1) - 1, 0, IdleClips.Length - 1)] };

        // its home beside the GM screen (FromGmScreen from the screen's right-hand front corner), or beyond
        // the map's far right corner when there's no screen; never nearer the map than MapClearance
        public void StandBy(Game.Board.Board board, GmScreen screen)
        {
            if (board?.Map == null) return;

            float farEdge = board.Position.Z - board.MatDepth * 0.5f;
            Vector3 corner = screen != null && screen.Standing.Size.X > 0
                ? new Vector3(screen.Standing.End.X, board.Position.Y, screen.Standing.End.Z)
                : board.Position + new Vector3(board.MatWidth * 0.5f, 0f, -board.MatDepth * 0.5f);

            Vector3 home = corner + FromGmScreen;
            float nearest = farEdge - MapClearance;

            _roaming = new Roaming((home.X, home.Z), WanderRadius, corner.X + 0.02, nearest,
                                   new SeededRng(System.Environment.TickCount), WalkEvery, WalkJitter);

            Position = new Vector3((float)_roaming.Home.X, board.Position.Y, (float)_roaming.Home.Z);
            _lookAt = board.Position;
            Face(_lookAt);
        }

        // turned to look at a point on the table
        void Face(Vector3 point)
        {
            Vector3 to = point - Position;
            if (to.LengthSquared() > 0.000001f) Rotation = new Vector3(0f, Mathf.Atan2(to.X, to.Z), 0f);
        }

        // CompanionMind's mood, on the main thread
        public void Feel(Mood mood)
        {
            _mood = mood;
            Stop();

            string[] clip = mood switch
            {
                Mood.Watching => WatchingClip,
                Mood.Alert => AlertClip,
                Mood.Pleased => PleasedClip,
                Mood.Quiet => LieClip,
                _ => Idle(),
            };

            Play(clip, mood is Mood.Calm or Mood.Watching or Mood.Quiet);
        }

        // it has a line in the dialogue
        public void Speak()
        {
            Stop();
            Play(SpeakClip, false);
            _gesture = 1.6;
        }

        // a rest: it lies its head down, then gets up
        public void Rest()
        {
            Stop();
            Play(LieClip, true);
            _resting = RestSeconds;
        }

        // a fallen foe: a lunge at it, and it skitters off the mat away from the companion
        public void Huff(Mini fallen)
        {
            if (fallen == null || !IsInsideTree() || !Visible) return;

            Stop();
            Face(GetParent<Node3D>().ToLocal(fallen.GlobalPosition));
            Play(HuffClip, false);
            _gesture = 1.2;

            Vector3 from = fallen.GetParent<Node3D>().ToLocal(GlobalPosition);
            fallen.Skitter(from);
        }

        // a walk cut short where it stands
        void Stop()
        {
            if (_roaming?.Walking == true) _roaming.Arrived((Position.X, Position.Z));
            _settled = false;
        }

        public override void _Process(double delta)
        {
            if (_player == null) return;

            if (_resting > 0 && (_resting -= delta) <= 0) Feel(Mood.Calm);

            if (_gesture > 0)
            {
                if ((_gesture -= delta) <= 0) Feel(_mood);
                return;
            }

            if (_mood != Mood.Calm || _resting > 0 || _roaming == null) return;

            if (_roaming.Walking)
            {
                Walk(delta);
                return;
            }

            if (_roaming.Tick(delta))
            {
                _settled = false;
                Face(Target);
                Play(WalkClip);
                return;
            }

            // still a while: it settles its head
            if (!_settled && _roaming.Still >= SettleAfter)
            {
                _settled = true;
                Play(SettleClip);
                return;
            }

            // calm and awake: the idles are dealt, none twice running
            if (!_settled && _idling != null && _idling.Tick(delta)) Play(Idle());
        }

        Vector3 Target => new Vector3((float)_roaming.At.X, Position.Y, (float)_roaming.At.Z);

        void Walk(double delta)
        {
            Vector3 to = Target - Position;
            float step = WalkSpeed * (float)delta;

            if (to.Length() <= step)
            {
                Position = Target;
                _roaming.Arrived((Position.X, Position.Z));
                Face(_lookAt);
                Play(Idle());
                return;
            }

            Position += to.Normalized() * step;
        }
    }
}
