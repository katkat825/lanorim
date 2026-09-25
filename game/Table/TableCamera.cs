using System;
using Godot;

namespace Game.Table
{
    // THE CAMERA IS A PERSON LEANING OVER A TABLE (ART_DIRECTION section 4).
    //
    // Fixed three-quarter overhead, orbitable in ninety-degree snaps, zooming from "the whole map"
    // to "leaning over one mini". The snap is not a limitation to work around - it is the thing
    // that means every tile only ever has to look right from four angles, which is most of why the
    // art budget for this game is a fraction of a normal one.
    //
    // THE NUMBERS HERE ARE DIALS AND WANT AN EYE ON THEM. Pitch, the two zoom ends, how long a
    // quarter turn takes and how much the drift moves are all things you judge by looking, not by
    // reading. What this file guarantees is only that they are applied smoothly and land exactly
    // on the quarter, so a tile authored square stays square.
    public partial class TableCamera : Camera3D
    {
        // what the camera looks at: the middle of the board. a Node3D, so the board can move it
        [Export] public NodePath SubjectPath { get; set; }

        // degrees off the felt. 60 is the documented angle and what every headless check assumes
        [Export] public float Pitch { get; set; } = 60f;

        // metres from the subject, at the two ends of the zoom
        [Export] public float Nearest { get; set; } = 0.55f;

        [Export] public float Furthest { get; set; } = 2.4f;

        // where it starts: 0 is all the way out
        [Export] public float Zoom { get; set; } = 0.75f;

        [Export] public float ZoomStep { get; set; } = 0.12f;

        // seconds for a quarter turn. long enough to follow, short enough not to wait through
        [Export] public float TurnSeconds { get; set; } = 0.35f;

        // THE HANDHELD DRIFT. ART_DIRECTION section 4: this one effect sells "a real object in a
        // real room" more than any polygon budget. Metres and degrees of wander, and it must stay
        // small - past about a centimetre it stops reading as a person and starts reading as a bug
        [Export] public float DriftMetres { get; set; } = 0.004f;

        [Export] public float DriftDegrees { get; set; } = 0.12f;

        [Export] public float DriftSpeed { get; set; } = 0.35f;

        // which quarter the camera is parked on, and which it is travelling to
        public int Quarter { get; private set; }

        int _from;
        float _turning = 1f;      // 0 at the start of a turn, 1 when it has arrived
        double _drift;

        Node3D _subject;

        // SOMETHING TO FOLLOW for a moment - an enemy taking its turn (combat_ux.md). The camera
        // eases toward it and back to the board's middle when it is let go (null)
        public Vector3? Following { get; set; }

        // seconds to get most of the way to a new point to follow
        [Export] public float FollowSeconds { get; set; } = 0.4f;

        Vector3? _focus;

        public override void _Ready()
        {
            _subject = SubjectPath == null || SubjectPath.IsEmpty
                ? null
                : GetNodeOrNull<Node3D>(SubjectPath);

            _from = Quarter;
            _turning = 1f;

            Place(1f);
        }

        // turn a quarter. positive is clockwise seen from above, which is the direction a hand
        // pushes a board round
        public void Turn(int quarters)
        {
            if (quarters == 0) return;

            // mid-turn, the new one is added to where it was going, so a double tap turns twice
            // rather than throwing the first one away
            _from = Quarter;
            Quarter += quarters;
            _turning = 0f;
        }

        public void ZoomBy(float by)
        {
            Zoom = Mathf.Clamp(Zoom + by, 0f, 1f);
        }

        public override void _Process(double delta)
        {
            if (_turning < 1f)
                _turning = Mathf.Min(1f, _turning + (float)delta / Mathf.Max(0.01f, TurnSeconds));

            _drift += delta * DriftSpeed;

            Place((float)delta);
        }

        void Place(float delta)
        {
            Vector3 wanted = Following ?? _subject?.GlobalTransform.Origin ?? Vector3.Zero;

            // eased toward what it follows; the first frame starts there
            _focus = _focus.HasValue
                ? _focus.Value.Lerp(wanted, Mathf.Clamp(delta / Mathf.Max(0.05f, FollowSeconds) * 2.5f, 0f, 1f))
                : wanted;

            Vector3 centre = _focus.Value;

            // eased, so it leaves and arrives softly and is linear in the middle
            float t = _turning >= 1f ? 1f : Ease(_turning);

            float yaw = Mathf.DegToRad(90f * Mathf.Lerp(_from, Quarter, t));

            float pitch = Mathf.DegToRad(Mathf.Clamp(Pitch, 5f, 89f));

            float distance = Mathf.Lerp(Furthest, Nearest, Mathf.Clamp(Zoom, 0f, 1f));

            // spherical: out along the yaw, up by the pitch
            var offset = new Vector3(
                Mathf.Sin(yaw) * Mathf.Cos(pitch),
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * Mathf.Cos(pitch)) * distance;

            // the drift is two slow sine waves at different rates, so it never repeats visibly and
            // never settles - a held camera does neither
            var wander = new Vector3(
                Mathf.Sin((float)_drift * 0.9f) * DriftMetres,
                Mathf.Sin((float)_drift * 1.31f + 1.7f) * DriftMetres,
                Mathf.Sin((float)_drift * 0.71f + 3.1f) * DriftMetres);

            GlobalPosition = centre + offset + wander;

            LookAt(centre, Vector3.Up);

            // and a whisper of roll, which is the part that actually reads as hands
            RotateObjectLocal(Vector3.Forward,
                              Mathf.DegToRad(Mathf.Sin((float)_drift * 0.53f) * DriftDegrees));
        }

        // smoothstep
        static float Ease(float t) => t * t * (3f - 2f * t);

        // TRUE WHEN THE CAMERA IS PARKED. A screenshot taken mid-turn is a screenshot of a blur,
        // so the headless checks wait on this.
        public bool IsStill => _turning >= 1f;

        // the quarter it is on, brought back into 0..3 for anything that wants to name it
        public int Facing => ((Quarter % 4) + 4) % 4;

        public override string ToString() =>
            $"quarter {Facing}, pitch {Pitch:0}, zoom {Zoom:0.00}" + (IsStill ? "" : ", turning");
    }
}
