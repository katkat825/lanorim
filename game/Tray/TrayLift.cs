using System.Collections.Generic;
using System.Linq;
using Game.Dice;
using Godot;

namespace Game.Tray
{
    // THE TRAY COMES TO YOU (updated_decisions.md; cc_task_ui-issues-9-30.md 4). Asked for the hero's
    // dice, the tray "lifts up to roll, blocking part of the grid map, then settles back down again an
    // appropriate amount of time after the roll stops, so the player can read/see the roll before it
    // gets small again by going back onto the table".
    //
    // TWEENS ONLY. The tray is only ever moved while nothing in it is moving: it rises before the
    // throw and goes back after the dice are read, and while it travels the dice that are showing are
    // frozen, so nothing in the physics is carried. It is moved, never turned or scaled: the dice
    // settle under the world's gravity, and the tray measures its own shape in its own space
    // (TrayBounds), so a throw up there is the same throw as one on the table. The GM's rolls behind
    // the screen never come to the tray, so they never move it.
    //
    // Every size and time is an export here: Kathleen's to tune.
    public partial class TrayLift : Node
    {
        // where on the screen the tray's middle comes to, 0..1 across and down
        [Export] public Vector2 ScreenAnchor { get; set; } = new Vector2(0.5f, 0.52f);

        // how much of the screen's height the tray fills when it is up, and at most how much of its
        // width (a 4:3 screen is narrower for the same height)
        [Export(PropertyHint.Range, "0.2,0.95,0.01")] public float ScreenShare { get; set; } = 0.85f;

        [Export(PropertyHint.Range, "0.2,0.95,0.01")] public float MostOfTheWidth { get; set; } = 0.85f;

        [Export] public float RiseSeconds { get; set; } = 0.45f;

        // once the dice are read: long enough to see what they say
        [Export] public float HoldSeconds { get; set; } = 1.5f;

        [Export] public float ReturnSeconds { get; set; } = 0.6f;

        public DiceTray Tray { get; set; }

        public Camera3D Camera { get; set; }

        enum State { Resting, Rising, Up, Holding, Returning }

        State _now = State.Resting;

        // developer diagnostic, exempt from localization: each change of state is logged
        State _state
        {
            get => _now;
            set
            {
                if (value != _now) GD.Print($"tray    lift {value.ToString().ToLowerInvariant()}");
                _now = value;
            }
        }
        Vector3 _rest;
        Tween _tween;
        double _hold;
        readonly List<DieBody> _frozen = new List<DieBody>();

        // up and still: a throw can go
        public bool IsUp => _state is State.Up or State.Holding;

        // on the table and still
        public bool IsResting => _state == State.Resting;

        // up where the throw goes, or on the table: never on the way
        public bool IsStill => _state is State.Resting or State.Up or State.Holding;

        // the dice are wanted: up it comes (and stays, if it is already up or on its way back down
        // for the next throw of the same attack)
        public void Rise()
        {
            if (Tray == null || Camera == null || Tray.IsThrowing) return;

            if (_state is State.Up or State.Rising) return;

            _settleAfterRise = false;

            if (_state == State.Holding)
            {
                _state = State.Up;
                return;
            }

            if (_state == State.Resting) _rest = Tray.Position;

            Move(Up(), RiseSeconds, State.Rising, Tween.EaseType.Out);
        }

        // the dice are read: hold a moment, then back to the table
        public void Settle()
        {
            _hold = HoldSeconds;

            if (_state == State.Up) _state = State.Holding;
            else if (_state == State.Rising) _settleAfterRise = true;
        }

        // read before it had finished rising (a quick throw): it holds once it is up
        bool _settleAfterRise;

        // straight back, no tween (the table is going, a fight was torn down)
        public void Home()
        {
            _tween?.Kill();
            Thaw();

            if (_state != State.Resting && Tray != null) Tray.Position = _rest;

            _state = State.Resting;
        }

        public override void _Process(double delta)
        {
            switch (_state)
            {
                case State.Rising when !Running():
                    Thaw();
                    _state = _settleAfterRise ? State.Holding : State.Up;
                    _settleAfterRise = false;
                    break;

                case State.Holding:
                    _hold -= delta;
                    if (_hold <= 0 && !Tray.IsThrowing) Move(_rest, ReturnSeconds, State.Returning, Tween.EaseType.InOut);
                    break;

                case State.Returning when !Running():
                    Thaw();
                    _state = State.Resting;
                    break;
            }
        }

        bool Running() => _tween != null && _tween.IsValid() && _tween.IsRunning();

        void Move(Vector3 to, float seconds, State state, Tween.EaseType ease)
        {
            _tween?.Kill();
            Freeze();

            _state = state;
            _tween = CreateTween();
            _tween.TweenProperty(Tray, "position", to, Mathf.Max(0.01f, seconds))
                  .SetTrans(Tween.TransitionType.Cubic)
                  .SetEase(ease);
        }

        // the tray's position (in its parent's space) with its felt at the screen anchor, near enough
        // to the camera to fill ScreenShare of the height and no more than MostOfTheWidth of the width
        Vector3 Up()
        {
            Vector2 screen = Camera.GetViewport().GetVisibleRect().Size;
            Vector3 origin = Camera.ProjectRayOrigin(screen * ScreenAnchor);
            Vector3 along = Camera.ProjectRayNormal(screen * ScreenAnchor);

            TrayBounds bounds = Tray.Bounds;
            float tan = Mathf.Tan(Mathf.DegToRad(Camera.Fov) * 0.5f);
            float aspect = screen.Y > 0 ? screen.X / screen.Y : 16f / 9f;

            // seen from the camera's pitch the tray's depth is foreshortened; its width is not
            float pitch = Mathf.Abs(Camera.GlobalBasis.Z.Y);
            float tall = bounds.HalfDepth * 2f * Mathf.Max(0.3f, pitch) + bounds.WallThickness * 4f;
            float wide = bounds.HalfWidth * 2f;

            float byHeight = tall / (2f * tan * ScreenShare);
            float byWidth = wide / (2f * tan * aspect * MostOfTheWidth);
            float distance = Mathf.Max(byHeight, byWidth);

            Vector3 felt = origin + along * distance;
            Vector3 global = felt - Tray.GlobalBasis * new Vector3(0f, bounds.FeltY, 0f);

            Node3D parent = Tray.GetParent() as Node3D;
            return parent == null ? global : parent.ToLocal(global);
        }

        // the dice showing are frozen while the tray travels, so the move carries them rather than
        // throwing them; each is let go as it was
        void Freeze()
        {
            foreach (DieBody die in Nodes.Under<DieBody>(Tray).Where(d => d.Visible && !d.Freeze))
            {
                die.Freeze = true;
                _frozen.Add(die);
            }
        }

        void Thaw()
        {
            foreach (DieBody die in _frozen.Where(IsInstanceValid)) die.Freeze = false;
            _frozen.Clear();
        }

        public override string ToString() => $"tray lift {_state}";
    }
}
