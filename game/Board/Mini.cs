using System;
using System.Collections.Generic;
using Godot;
using Content.Minis;
using Game.Audio;

namespace Game.Board
{
    // grid holds the destination before the slide starts, so an interrupted move never leaves model and view disagreeing
    public partial class Mini : Node3D
    {
        [Export] public NodePath VoicePath { get; set; } = "Voice";

        // height is stated, scale is derived, so swapping the model keeps the piece the right height
        [Export] public NodePath FigurePath { get; set; } = "Figure";

        // 1.25 squares tall, taller than walls so a piece is never hidden. board.tscn's CellSize is
        // the other half of that ratio - move one and move the other
        [Export] public float FigureHeight { get; set; } = 0.1875f;

        // the model this piece stands up. null and the scene's own stand-in stays, which is what
        // every piece was before there were models - so a board with no models still plays
        [Export] public PackedScene FigureModel { get; set; }

        [Export] public Material Paint { get; set; }

        // WHAT A PIECE SOUNDS LIKE AND WHAT ITS MODEL CAN DO are not ported yet: the old build's
        // MiniVoice, PackVoice and MiniClips all read a campaign's mini pack, and lanorim has no
        // pack format yet (Phase 7). Every motion below already falls back to the procedural one
        // when there is no clip to play, so what is here IS the fallback path - which is also the
        // path every base-game piece took in the old build.

        AudioStreamPlayer3D _player;

        MiniStep _step;

        float _elapsed;

        public bool IsMoving => _step != null;

        // fires on a real move, not a refusal; the door check waits on this
        public event Action Arrived;

        // the instant the piece touches the mat, partway through a move. the sound hangs here
        public event Action SetDown;

        bool _refusing;

        public override void _Ready()
        {
            _player = GetNodeOrNull<AudioStreamPlayer3D>(VoicePath);

            if (_player == null)
                GD.PushError($"mini: no AudioStreamPlayer3D at '{VoicePath}' - it will be set down in silence");

            Stand(GetNodeOrNull<Node3D>(FigurePath));
        }

        // feet stay on the base whatever the model's origin, so a model built in a hole still stands right
        void Stand(Node3D figure)
        {
            if (figure == null)
            {
                GD.PushError($"mini: no figure at '{FigurePath}' - the piece is a base with nothing on it");
                return;
            }

            // a warning, not an error: until the shader is on the Board's Paint slot, every piece
            // wears whatever its pack came with, and that is the expected state today rather than
            // a fault. res://shaders/painted.tres is the thing to put there.
            if (Paint == null)
                GD.PushWarning("mini: the figure has no paint - it will wear whatever its pack " +
                               "came with. Put res://shaders/painted.tres on the Board's Paint slot.");

            Dress(figure);

            PaintedModel.Paint(figure, Paint);

            // a rigged figure is measured off its posed skeleton, because its AABB is stuck in the
            // bind pose; anything else is measured off its geometry, which is telling the truth
            bool rigged = PaintedModel.PosedHeight(figure, out float tall, out float feet);

            Aabb bounds = PaintedModel.Bounds(figure);

            if (!rigged)
            {
                tall = bounds.Size.Y;
                feet = bounds.Position.Y;
            }

            float scale = tall <= 0f ? 1f : FigureHeight / tall;

            figure.Scale = Vector3.One * scale;

            // only the height is corrected: centring the bounding box would push an off-centre figure's feet off the square
            figure.Position = new Vector3(
                figure.Position.X,
                figure.Position.Y - feet * scale,
                figure.Position.Z);

            GD.Print($"mini    {figure.Name} {tall:0.00} units {(rigged ? "posed" : "measured")} " +
                     $"-> {FigureHeight * 1000f:0} mm tall");
        }

        // SWAP THE STAND-IN FOR A REAL MODEL, AND STOP IT MOVING.
        //
        // The Quaternius packs are rigged and animated - seventeen clips each - and a mini is not.
        // ART_DIRECTION section 5 is explicit: on-map pieces are objects that get picked up and set
        // down, not things that walk, and section 1 counts "no walk cycles" as a feature rather than
        // a gap. So the rig is used for exactly one thing: to stand the figure in the pack's own
        // idle pose and then stop. A model imported and left alone sits in its BIND pose, which for
        // these packs is a T-pose - arms straight out, which reads as a crucifixion rather than a
        // miniature - and one frame of Idle is the cheapest fix that does not need a modeller.
        //
        // The player is then stopped dead. Nothing ticks, nothing blends, and the piece is as static
        // as the cylinder it replaced.
        void Dress(Node3D figure)
        {
            if (FigureModel == null) return;

            foreach (Node child in figure.GetChildren())
            {
                figure.RemoveChild(child);
                child.QueueFree();
            }

            Node model = FigureModel.Instantiate();

            if (model == null)
            {
                GD.PushError($"mini: '{FigureModel.ResourcePath}' did not instantiate - the piece " +
                             "will be a base with nothing on it");
                return;
            }

            figure.AddChild(model);

            Pose(model);
        }

        // the pack's idle, held on one frame. Named clips vary between packs, so it takes the one
        // called Idle if there is one and the first clip if there is not, rather than assuming
        static void Pose(Node model)
        {
            AnimationPlayer player = null;

            foreach (Node child in model.GetChildren())
                if (child is AnimationPlayer found) { player = found; break; }

            if (player == null) return;

            string clip = null;

            foreach (string name in player.GetAnimationList())
            {
                if (name.Contains("Idle", StringComparison.OrdinalIgnoreCase)) { clip = name; break; }

                clip ??= name;
            }

            if (clip == null) return;

            // seek with update:true writes the pose onto the skeleton, then the player is switched
            // off entirely - advance(0) on a stopped player would put the rest pose back
            player.Play(clip);
            player.Seek(RestingFrame, true);
            player.Pause();
            player.ProcessMode = ProcessModeEnum.Disabled;
        }

        // seconds into the idle. not 0: the first frame of a loop is often the neutral pose the
        // clip was built from, and a little way in reads more like a figure caught standing
        const float RestingFrame = 0.35f;

        public void PlaceAt(Vector3 boardLocal)
        {
            _step = null;
            _elapsed = 0f;
            Position = boardLocal;

        }

        // the whole route as one movement, starting from where it is now, so a mid-move click retargets and the first waypoint is replaced
        public void Follow(IReadOnlyList<Vector3> route)
        {
            if (route == null || route.Count == 0) return;

            var through = new List<Vector3>(route) { [0] = Position };

            _step = new MiniStep(through);
            _elapsed = 0f;
            _refusing = false;

        }

        // view only; the board decides whether to refuse, and the piece ends exactly where it started
        public void Refuse(Vector3 toward) => Lean(toward);

        // same motion as a refusal, but named apart so a swing does not read as a refused move
        public void Strike(Vector3 toward)
        {

            Lean(toward);
        }

        // ignored mid-move and when toppled: interrupting a carry or reacting on a corpse reads as a glitch
        public void Wobble()
        {
            if (_toppled) return;


            if (IsMoving) return;

            Lean(Position + new Vector3(0f, 0f, FigureHeight * 0.22f));
        }

        void Lean(Vector3 toward)
        {
            _step = MiniStep.Refusing(Position, toward);
            _elapsed = 0f;
            _refusing = true;
        }

        // view only, laid on its side and left; the grid already removed it (Board.Lift)
        public void Topple()
        {
            if (_toppled) return;

            _toppled = true;
            _step = null;

            // the procedural tip-over. A death is a mini going over and lying on the map, and
            // it stays there - a room that fills with toppled pieces is a better record of a hard
            // fight than any kill counter (ART_DIRECTION section 5)
            // lifted as it rotates about the feet, or the toppled piece lies half inside the mat
            RotateX(-Mathf.Pi * 0.5f);
            Position += new Vector3(0f, FigureHeight * 0.16f, 0f);
        }

        public bool IsToppled => _toppled;

        bool _toppled;


        // TAKEN OFF THE TABLE (the eye check, 2026-09-16). A toppled piece used to lie where it
        // fell for the rest of the fight - "freed from the grid but left toppled where it fell" -
        // and by the third round the board was more bodies than squares.
        //
        // The fall still registers: it topples, it LIES there for a beat, and then it goes. What
        // changed is WHO takes it: nothing lifts itself off the table any more, and the DM's hand
        // never sweeps (five deaths in a round is five queued gestures). The companion huffs at it
        // and it SKITTERS - away from the creature, off the edge of the mat, gone. A body that
        // slides away from something that just barked at it reads as cleared; a body that rises
        // straight up reads as a bug.
        //
        // It is HIDDEN rather than freed. A Piece holds this node and a save rebuilds the fight
        // from the actors, so freeing it here would leave a dangling reference for the sake of one
        // object that costs nothing to keep.

        // toppled and left there, so the fall is seen before the huff comes for it
        public const double ClearedAfter = 0.8;

        // and then away, quicker than a hand would have been, because nothing is carrying it
        public const double ClearedOver = 0.3;

        // how far it slides before it is out of sight, in figure heights
        public const float SkitterReach = 2.2f;

        double _clearing = -1.0;

        Vector3 _clearedFrom;

        Vector3 _skitter = Vector3.Zero;

        public bool BeingCleared => _clearing >= 0.0;

        // away from whatever huffed at it, flat across the felt; straight back when nothing did
        public void Skitter(Vector3 shooedFrom)
        {
            if (!_toppled || _clearing >= 0.0 || !Visible) return;

            Vector3 away = Position - shooedFrom;
            away.Y = 0f;

            _skitter = away.LengthSquared() < 0.000001f
                ? Vector3.Forward
                : away.Normalized();

            _clearing = ClearedAfter + ClearedOver;
            _clearedFrom = Position;
        }

        // counted down in _Process rather than ended with a tween callback, for the reason
        // Bubble.cs spells out at its own line: a Callable.From(...) holds a C# delegate alive on
        // Godot's side, and one per piece killed is one per piece leaked
        void Clearing(double delta)
        {
            _clearing -= delta;

            if (_clearing <= 0.0)
            {
                _clearing = -1.0;
                Visible = false;
                return;
            }

            if (_clearing >= ClearedOver) return;

            float gone = 1f - (float)(_clearing / ClearedOver);

            // it goes fast at first and then is simply not there; a piece that decelerates to a
            // stop off the mat is a piece you watched being deleted
            Position = _clearedFrom + _skitter * (gone * FigureHeight * SkitterReach);
        }

        public override void _Process(double delta)
        {
            if (_clearing >= 0.0) { Clearing(delta); return; }

            if (_step == null) return;

            float was = _elapsed;
            _elapsed += (float)delta;

            Position = _step.At(_elapsed);

            // the moment the piece touches down. Nothing plays yet - the mini pack that carries
            // the foley is Phase 7 - but the beat is here and it is the one to hang it on
            if (_step.SetsDownBetween(was, _elapsed)) SetDown?.Invoke();

            if (!_step.IsDone(_elapsed)) return;

            _step = null;

            // cleared before Arrived fires: a listener may start the next move inside it
            bool refusal = _refusing;
            _refusing = false;

            if (!refusal) Arrived?.Invoke();
        }
    }
}
