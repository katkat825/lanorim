using System;
using Godot;
using Core.Dice;
using Game.Audio;

namespace Game.Dice
{
    // named DieBody rather than Die because Core.Dice.Die is the die-size enum
    // deliberately not a [Tool] script, or the editor would bake a stale mesh and mass into die.tscn
    public partial class DieBody : RigidBody3D
    {
        [Signal] public delegate void SettledEventHandler(int value);

        // one signal per recovery action: a merged signal once let consumers add the counters and overcount ~3x
        [Signal] public delegate void NudgedEventHandler(float alignment, int attempt);

        [Signal] public delegate void CockedEventHandler(float alignment, int attempt);

        [Signal] public delegate void LeftTrayEventHandler(int attempt);

        // a plain C# event, not a [Signal]: DieHit is a struct and signals carry only Variants
        public event Action<DieHit> Struck;

        Die _size = Die.D6;

        [Export]
        public Die Size
        {
            get => _size;
            set
            {
                _size = value;
                if (IsNodeReady()) Build();
            }
        }

        [Export] public float ThrowSpeedMin { get; set; } = 0.8f;
        [Export] public float ThrowSpeedMax { get; set; } = 1.3f;

        [Export] public float LiftMin { get; set; } = 0.10f;
        [Export] public float LiftMax { get; set; } = 0.35f;

        // kept low: a wide spread makes three dice converge and collide mid-flight, the main cause of cocked landings
        [Export] public float Spread { get; set; } = 0.12f;

        [Export] public float SpinMin { get; set; } = 12f;
        [Export] public float SpinMax { get; set; } = 30f;

        [Export] public float RestLinearSpeed { get; set; } = 0.02f;
        [Export] public float RestAngularSpeed { get; set; } = 0.15f;
        [Export] public float RestHoldSeconds { get; set; } = 0.2f;

        // how squarely the resting face must point at the felt, as a dot product (1.0 flat, 0.9 ~26deg)
        // a floor, not the value used: some shapes need stricter, see RequiredAlignment
        [Export] public float CockedAlignment { get; set; } = 0.9f;

        // give-up count for cocked re-throws, so a wedged die can't loop forever
        [Export] public int MaxCockedRethrows { get; set; } = 3;

        [Export] public float NudgeSpeed { get; set; } = 0.45f;

        [Export] public int MaxNudges { get; set; } = 2;

        // null means world space: die.tscn opened on its own, with nothing to leave
        // not [Export]: a node reference exported on a scene instanced three times is a NodePath waiting to break
        public Node3D TraySpace { get; set; }

        // out of the tray: below this Y or beyond this radius, both measured in TraySpace so the tray can stand anywhere
        // defaults for a die nothing has told otherwise; DiceTray measures the real tray and hands these over
        public float LostBelowY { get; set; } = -0.2f;

        public float LostRadius { get; set; } = 0.6f;

        [Export] public int MaxLostRethrows { get; set; } = 3;

        // THE HARD CEILING. A die still moving this long after it left the hand is nudged once, given
        // AfterNudgeSeconds, and then read as it lies (frozen where it is). Never thrown again, never an endless
        // wait: Kathleen saw the old answer, a re-throw, as a die that "keeps wobbling" and then jumps. The wobble
        // itself was the physics step (DieBody.Wobble.cs); since 120 ticks a second this is a backstop that a
        // 2000-throw sweep never reached
        [Export] public float MaxFlightSeconds { get; set; } = 6f;

        [Export] public float AfterNudgeSeconds { get; set; } = 2f;

        // how many times the ceiling nudges before it reads the die as it lies
        [Export] public int MaxRestlessNudges { get; set; } = 1;

        // a settled die moving again this long - knocked loose by another's landing - is watched as a throw again
        [Export] public float KnockedLooseSeconds { get; set; } = 0.3f;

        // dice get a density, not a mass, so a bigger solid comes out heavier like a real set
        // only collisions feel it: speed and spin are set directly, so a throw ignores shape
        [Export] public float Density { get; set; } = 400f;

        [Export] public Color Ink { get; set; } = new(0.12f, 0.1f, 0.09f);

        bool _showNumbers = true;

        [Export]
        public bool ShowNumbers
        {
            get => _showNumbers;
            set
            {
                _showNumbers = value;
                if (IsNodeReady()) Build();
            }
        }

        // the give-up case logs regardless; if that one is frequent, that is the finding
        [Export] public bool LogSettles { get; set; } = true;

        bool _reportContacts = true;

        [Export]
        public bool ReportContacts
        {
            get => _reportContacts;
            set
            {
                _reportContacts = value;

                // set both together: ContactMonitor on with MaxContactsReported at 0 reports nothing, looking like a die that never touches anything
                ContactMonitor = value;
                MaxContactsReported = value ? ContactsWatched : 0;
            }
        }

        // a d12 landing flat reports several contacts at once; enough that the summed hit isn't clipped
        const int ContactsWatched = 6;

        // collision threshold, as the speed the die lost to it; mass-independent so a d4 and d12 need the same bump
        // has to clear gravity: the felt pushes a resting die back up ~0.08 m/s per tick at 120 Hz
        [Export] public float MinHitSpeed { get; set; } = 0.18f;

        // one bounce lasts several steps; without this gap one collision fires several times and rips
        [Export] public float MinHitGap { get; set; } = 0.045f;

        public IDieRecovery Recovery { get; set; }

        // derived, not stored, so it answers before the node is ready and can't disagree with Size; solids are cached
        public DieSolid Solid => DieSolid.For(Size);

        public float RequiredAlignment => Mathf.Max(CockedAlignment, Solid.MinFlatAlignment);

        DieFaceTable _faces;

        readonly RandomNumberGenerator _rng = new();

        // a state change queued for the next physics step - see _IntegrateForces
        struct Kick
        {
            public bool Reposition;
            public Transform3D Where;
            public Vector3 Linear;
            public Vector3 Angular;
        }

        bool _kickQueued;
        Kick _kick;

        bool _inFlight;
        double _flightTime;
        double _stillTime;

        // see MaxFlightSeconds and KnockedLooseSeconds
        int _restless;
        double _looseTime;
        bool _knocked;

        // where the last throw came from, so a cocked die can be thrown again identically
        Transform3D _lastThrowFrom;
        int _cockedRethrows;
        int _lostRethrows;
        int _nudges;

        // hits since the current throw left the hand - one means the first impact
        int _hits;

        // hits fire on the rising edge, so the last step's answer is kept
        bool _wasStruck;

        double _sinceHit;

        public bool IsSettled => !_inFlight;

        // physically still now, unlike IsSettled which latches when the settle was declared
        // a re-thrown die can bump one already settled, so IsSettled can stay true while its face changes; wait on this too
        public bool IsAtRest =>
            Freeze || LinearVelocity.Length() < RestLinearSpeed && AngularVelocity.Length() < RestAngularSpeed;

        // zero while a throw is in the air, so a caller that skips IsSettled gets an obviously wrong number, not a stale plausible one
        public int SettledValue { get; private set; }
    }
}
