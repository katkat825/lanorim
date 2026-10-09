using System.Linq;
using Content.Companions;
using Core.Dice;
using Game.Board;
using Godot;

namespace Game.Table
{
    // THE COMPANION ON THE TABLE (cc_task_ui-issues-9-30.md 6.4, cc_task_ui-issues-10-01.md 4): a Quaternius
    // monster (an animal until task G), mini-sized, NEVER ON THE MAP. It stands on the table above the map (the far side, toward the GM
    // screen), a little to the right of the screen, with open table round it, and it is alive: it idles, walks
    // between spots near its home now and then (Roaming), and settles its head after a while still. What it
    // does is decided in content, where it is tested: which idle comes next is Idling's deal, where it walks
    // is Roaming's, how it feels is CompanionMind's (it watches the fight and the hero's rolls, PlayDirector).
    // This only plays it.
    //
    // It also clears the board: a fallen foe topples, and the companion lunges at it and it skitters off the
    // mat (Mini.Skitter). When the dialogue gives it a line (the story's "companion" speaker), it looks up.
    //
    // Every size, distance and time is an export: Kathleen's to tune. The clip names are the model's, in its look
    // (CompanionModels, CompanionClips), and so are its hover and its colours: a flyer hangs a little off the
    // table and bobs, and a recoloured one wears its own copy of the atlas (AtlasRecolour).
    public partial class CompanionFigure : Node3D
    {
        // how tall it stands, in metres. Twice the board's wolf mini (0.11) since 2026-10-03 (Kathleen: "make
        // companions twice as big"); it keeps its whole body off the map whichever way it faces (Reach)
        [Export] public float Height { get; set; } = 0.22f;

        // its home, from the GM screen's right-hand front corner, in metres (+X right, +Z toward the map): in the
        // gap between the screen and the map, a little right of the screen
        [Export] public Vector3 FromGmScreen { get; set; } = new Vector3(0.18f, 0f, 0.12f);

        // how far its body reaches from where it stands, measured when it is dressed (0 before)
        public float Reach { get; private set; }

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

        // a hovering companion's bob, rises and falls a second
        [Export] public float BobsPerSecond { get; set; } = 0.45f;

        // seconds it lies down for a rest
        [Export] public float RestSeconds { get; set; } = 4f;

        public string Companion { get; private set; } = "";

        Node3D _model;
        CompanionLook _look;
        CompanionClips _clips = CompanionClips.Flying;
        float _hover;
        double _bobbing;
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

            _look = CompanionModels.For(companion);
            PackedScene scene = CompanionModels.Model(_look);
            Visible = scene != null;

            if (scene == null) return;

            _clips = _look.Clips ?? CompanionClips.Flying;

            _model = scene.Instantiate<Node3D>();
            AddChild(_model);

            // its own colours before the paint, which reads them
            AtlasRecolour.Apply(_model, _look.Recolour);

            if (paint != null) PaintedModel.Paint(_model, paint, "companion");

            Aabb box = PaintedModel.Bounds(_model);
            float scale = box.Size.Y > 0.0001f ? Height / box.Size.Y : 1f;
            _model.Scale = Vector3.One * scale;

            // its lowest point on the table, or Hover above it: the Flying monsters are modelled a body's height
            // off the ground, so they're brought down to it first
            _hover = -box.Position.Y * scale + _look.Hover;
            _model.Position = new Vector3(0f, _hover, 0f);

            // nose or tail, the furthest it reaches from where it stands, whichever way it turns
            Reach = Mathf.Max(Mathf.Max(Mathf.Abs(box.Position.X), Mathf.Abs(box.End.X)),
                              Mathf.Max(Mathf.Abs(box.Position.Z), Mathf.Abs(box.End.Z))) * scale;

            _model.RotationDegrees = new Vector3(0f, ForwardDegrees, 0f);

            _player = Nodes.Under<AnimationPlayer>(_model).FirstOrDefault();
            _idling = new Idling(_clips.Idles.Length, new SeededRng(System.Environment.TickCount));
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

        // a clip that doesn't loop plays once and the idle follows it, so a flyer never hangs in the air on the
        // last frame of a nod
        void Play(string[] clip, bool loop = true)
        {
            string full = Full(clip);
            if (full == null) return;

            Animation animation = _player.GetAnimation(full);
            if (animation != null) animation.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;

            _player.Play(full, 0.25);

            if (loop || Full(Idle()) is not { } idle || idle == full) return;

            _player.GetAnimation(idle).LoopMode = Animation.LoopModeEnum.Linear;
            _player.Queue(idle);
        }

        string[] Idle() => _clips.Idles.Length == 0
            ? null
            : new[] { _clips.Idles[System.Math.Clamp((_idling?.Idle ?? 1) - 1, 0, _clips.Idles.Length - 1)] };

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

            // its body as well as its feet: a nose turned toward the map never reaches it, nor a tail the screen
            float nearest = farEdge - MapClearance - Reach;

            _roaming = new Roaming((home.X, home.Z), WanderRadius, corner.X + 0.02 + Reach, nearest,
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
                Mood.Watching => _clips.Watching,
                Mood.Alert => _clips.Alert,
                Mood.Pleased => _clips.Pleased,
                Mood.Quiet => _clips.Lie,
                _ => Idle(),
            };

            Play(clip, mood is Mood.Calm or Mood.Watching || mood == Mood.Quiet && _clips.LieLoops);
        }

        // it has a line in the dialogue
        public void Speak()
        {
            Stop();
            Play(_clips.Speak, false);
            _gesture = 1.6;
        }

        // a rest: it lies its head down, then gets up
        public void Rest()
        {
            Stop();
            Play(_clips.Lie, _clips.LieLoops);
            _resting = RestSeconds;
        }

        // a fallen foe: a lunge at it, and it skitters off the mat away from the companion
        public void Huff(Mini fallen)
        {
            if (fallen == null || !IsInsideTree() || !Visible) return;

            Stop();
            Face(GetParent<Node3D>().ToLocal(fallen.GlobalPosition));
            Play(_clips.Huff, false);
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
            Bobbing(delta);

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
                Play(_clips.Walk);
                return;
            }

            // still a while: it settles its head
            if (!_settled && _roaming.Still >= SettleAfter)
            {
                _settled = true;
                Play(_clips.Settle);
                return;
            }

            // calm and awake: the idles are dealt, none twice running
            if (!_settled && _idling != null && _idling.Tick(delta)) Play(Idle());
        }

        // a hovering companion rises and falls a little; one standing on the table doesn't
        void Bobbing(double delta)
        {
            if (_model == null || _look == null || _look.Bob <= 0f) return;

            _bobbing += delta * BobsPerSecond * Mathf.Tau;
            _model.Position = new Vector3(0f, _hover + _look.Bob * Mathf.Sin((float)_bobbing), 0f);
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
