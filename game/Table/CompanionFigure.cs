using System.Linq;
using Content.Companions;
using Core.Dice;
using Game.Board;
using Godot;

namespace Game.Table
{
    // THE COMPANION ON THE TABLE (cc_task_ui-issues-9-30.md 6.4; docs/updated_decisions.md): a KayKit
    // skeleton, mini-sized, NEVER ON THE MAP - it stands on the table beside the board's near corner,
    // and it stands, walks a step, or lies down there. What it does is decided in content, where it is
    // tested: which idle comes next is Idling's deal (Content.Companions), how it feels is
    // CompanionMind's (it watches the fight and the hero's rolls, PlayDirector). This only plays it.
    //
    // It also clears the board: a fallen foe topples, and the companion gestures at it and it skitters
    // off the mat (Mini.Skitter) - "a body that slides away from something that just barked at it reads
    // as cleared". And when the dialogue gives it a line (the story's "companion" speaker, which the
    // campaign's own companion answers to), it looks up and speaks with its hands.
    //
    // Every clip name, size and time is an export: Kathleen's to tune.
    public partial class CompanionFigure : Node3D
    {
        // how tall it stands, in metres: a mini's height (the board's heroes are 0.1875)
        [Export] public float Height { get; set; } = 0.15f;

        // where it stands, from the board's near-left corner, in metres (+X right, +Z toward you)
        [Export] public Vector3 FromCorner { get; set; } = new Vector3(-0.08f, 0f, 0.02f);

        // turned toward the board and a little toward the camera, in degrees
        [Export] public float TurnDegrees { get; set; } = 35f;

        [Export] public string[] IdleClips { get; set; } = { "Idle_A", "Idle_B" };

        [Export] public string WatchingClip { get; set; } = "Idle_B";

        [Export] public string AlertClip { get; set; } = "Interact";

        [Export] public string PleasedClip { get; set; } = "Jump_Full_Short";

        // the hero is down: it lies down beside the board, and it lies down to sleep through a rest
        [Export] public string LieClip { get; set; } = "Death_A_Pose";

        [Export] public string SpeakClip { get; set; } = "Interact";

        [Export] public string HuffClip { get; set; } = "Throw";

        // seconds it lies down for a rest
        [Export] public float RestSeconds { get; set; } = 4f;

        public string Companion { get; private set; } = "";

        Node3D _model;
        AnimationPlayer _player;
        Idling _idling;
        Mood _mood = Mood.Calm;
        double _gesture;
        double _resting;

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

            _model.RotationDegrees = new Vector3(0f, TurnDegrees, 0f);

            _player = Animate(_model);
            _idling = new Idling(IdleClips.Length, new SeededRng(System.Environment.TickCount));
            Play(Idle());

            GD.Print($"companion  {companion}: {scene.ResourcePath.GetFile()}, " +
                     $"{(_player == null ? "no clips" : _player.GetAnimationList().Length + " clips")}");
        }

        // the skeletons carry no clips of their own: the shared rig's are lent to the model's player.
        // The rig and the models share one skeleton (Rig_Medium), so the tracks' paths match
        static AnimationPlayer Animate(Node3D model)
        {
            AnimationPlayer player = model.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();

            if (player == null)
            {
                player = new AnimationPlayer { Name = "AnimationPlayer" };
                model.AddChild(player);
            }

            foreach (string rig in CompanionModels.Rigs)
            {
                if (!ResourceLoader.Exists(rig) || GD.Load<PackedScene>(rig)?.Instantiate() is not Node source) continue;

                AnimationPlayer from = Nodes.Under<AnimationPlayer>(source).FirstOrDefault();

                if (from != null)
                    foreach (StringName name in from.GetAnimationLibraryList())
                    {
                        string library = rig.GetFile().GetBaseName().ToLowerInvariant();
                        if (!player.HasAnimationLibrary(library))
                            player.AddAnimationLibrary(library, from.GetAnimationLibrary(name));
                    }

                source.Free();
            }

            return player.GetAnimationList().Length == 0 ? null : player;
        }

        // a clip by its short name, whichever library lent it
        string Full(string clip) =>
            _player?.GetAnimationList().FirstOrDefault(a => a == clip || a.EndsWith("/" + clip));

        void Play(string clip, bool loop = true)
        {
            string full = Full(clip);
            if (full == null) return;

            Animation animation = _player.GetAnimation(full);
            if (animation != null && loop) animation.LoopMode = Animation.LoopModeEnum.Linear;

            _player.Play(full, 0.25);
        }

        string Idle() => IdleClips.Length == 0 ? null : IdleClips[System.Math.Clamp((_idling?.Idle ?? 1) - 1, 0, IdleClips.Length - 1)];

        // stands where FromCorner says, beside a board just laid
        public void StandBeside(Game.Board.Board board)
        {
            if (board?.Map == null) return;

            Vector3 corner = board.Position + new Vector3(-board.MatWidth * 0.5f, 0f, board.MatDepth * 0.5f);
            Position = corner + FromCorner;
        }

        // CompanionMind's mood, on the main thread
        public void Feel(Mood mood)
        {
            _mood = mood;

            string clip = mood switch
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
            Play(SpeakClip, false);
            _gesture = 1.6;
        }

        // a rest: it lies down, then gets up
        public void Rest()
        {
            Play(LieClip, true);
            _resting = RestSeconds;
        }

        // a fallen foe: a gesture at it, and it skitters off the mat away from the companion
        public void Huff(Mini fallen)
        {
            if (fallen == null || !IsInsideTree() || !Visible) return;

            Play(HuffClip, false);
            _gesture = 1.2;

            Vector3 from = fallen.GetParent<Node3D>().ToLocal(GlobalPosition);
            fallen.Skitter(from);
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

            // calm: the idles are dealt, none twice running
            if (_mood == Mood.Calm && _resting <= 0 && _idling != null && _idling.Tick(delta)) Play(Idle());
        }
    }
}
