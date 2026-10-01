using Godot;

namespace Game.Dice
{
    public partial class DieBody
    {
        public override void _Ready()
        {
            _rng.Randomize();

            Recovery ??= new NudgeThenRethrow(MaxNudges, MaxCockedRethrows, MaxLostRethrows, MaxRestlessNudges);

            // re-apply: an [Export] setter never runs for the default value, so without this the physics server would watch nothing while the field says true
            ReportContacts = _reportContacts;

            Build();
        }

        // rebuild mesh, hull, numerals, face table and mass from the solid; idempotent - Size changes and duplicates call it again
        void Build()
        {
            DieSolid solid = Solid;
            _faces = solid.FaceTable();

            Mass = Density * solid.Volume;

            // the hull goes on square: a d6's turn (TurnTheCube) must not outlive it. a die that was a d6 last throw and
            // a d20 this one landed on a turned hull its faces weren't read by - cocked at the same tilt every time,
            // nudged, re-thrown (cc_task_e-shop-species-and-ui-notes.md 2.1)
            CollisionShape3D hull = GetNode<CollisionShape3D>("CollisionShape3D");
            hull.Shape = DieParts.BuildHull(solid);
            hull.Basis = Basis.Identity;
            GetNode<MeshInstance3D>("MeshInstance3D").Mesh = DieParts.BuildMesh(solid);

            Node existing = GetNodeOrNull(DieParts.NumbersNode);
            if (existing != null)
            {
                RemoveChild(existing);
                existing.Free();
            }

            if (ShowNumbers) AddChild(DieParts.BuildNumbers(solid, Ink));
        }

        // roughly along -Z, up the tray and away from the camera. safe to call mid-flight
        public void Throw(Transform3D from)
        {
            _cockedRethrows = 0;
            _lostRethrows = 0;
            _restless = 0;
            Launch(from);
        }

        // split from Throw so the cocked and escaped paths re-throw without resetting the attempt counters that bound them
        // energy scales the horizontal push; at zero the die drops inside the tray and cannot escape
        void Launch(Transform3D from, float energy = 1f)
        {
            TurnTheCube();

            _lastThrowFrom = from;
            SettledValue = 0;
            _nudges = 0;

            // a throw, even a cocked or escaped die's second, gets the whole ceiling again
            _restless = 0;

            // the next contact is this throw's first impact, whatever the last one left behind
            _hits = 0;
            _wasStruck = false;
            _sinceHit = MinHitGap;

            // normalising a zero vector yields NaN, so a pure drop is its own case
            Vector3 direction = energy <= 0.001f
                ? Vector3.Zero
                : new Vector3(
                    _rng.RandfRange(-Spread, Spread),
                    _rng.RandfRange(LiftMin, LiftMax),
                    -1f).Normalized();

            Queue(new Kick
            {
                Reposition = true,

                // random start orientation, so a throw never begins on the same face twice
                Where = new Transform3D(new Basis(RandomTurn()), from.Origin),

                Linear = direction * _rng.RandfRange(ThrowSpeedMin, ThrowSpeedMax) * energy,
                Angular = RandomAxis() * _rng.RandfRange(SpinMin, SpinMax),
            });
        }

        // THE d6 ROLLED LOW (found 2026-10-03, after the move to 120 ticks): faces 1, 2 and 3 came up more than 4, 5
        // and 6, about 4% each way - pooled chi-squared 35 to 40 on 32,000-48,000 faces, far past the 0.1% line. They
        // are the cube's positive axes (+Y, +Z, +X). The physics never sees a number, so the lean was the engine's: the
        // solver works through a body's own axes in a fixed order and leans, near a tie, toward the positive ones. A
        // true box shape leaned the same way. So each throw turns the cube's collision shape by one of its 24
        // rotations: the same cube to the eye and to the physics, but which of its faces the engine takes for +X is
        // dealt afresh every throw, and the lean is shared by all six. Measured the same way after: chi-squared 8.4 on
        // 32,000 faces, uniform, no one-sided drift. The other solids' sweeps were uniform, so only the cube turns
        void TurnTheCube()
        {
            if (Size != Core.Dice.Die.D6 || GetNodeOrNull<CollisionShape3D>("CollisionShape3D") is not CollisionShape3D shape)
                return;

            Vector3[] axes = { Vector3.Right, Vector3.Up, Vector3.Back };

            int i = _rng.RandiRange(0, 2);
            int j = (i + _rng.RandiRange(1, 2)) % 3;

            Vector3 x = axes[i] * (_rng.Randf() < 0.5f ? -1f : 1f);
            Vector3 y = axes[j] * (_rng.Randf() < 0.5f ? -1f : 1f);

            shape.Basis = new Basis(x, y, x.Cross(y));
        }

        // a small lift and spin in place to topple a die on an edge or a neighbour, deliberately weak so one throw still looks like one
        void Nudge()
        {
            Vector3 dir = new Vector3(
                _rng.RandfRange(-0.4f, 0.4f), 1f, _rng.RandfRange(-0.4f, 0.4f)).Normalized();

            Queue(new Kick
            {
                Reposition = false,
                Linear = dir * NudgeSpeed,
                Angular = RandomAxis() * _rng.RandfRange(SpinMin, SpinMax) * 0.35f,
            });
        }

        // in flight from this instant, not when the physics server next runs: the kick lands in _IntegrateForces frames later headless, and a caller polling IsSettled between would read the last throw's face
        void Queue(Kick kick)
        {
            _kick = kick;
            _kickQueued = true;

            // a sleeping body is skipped by _IntegrateForces entirely
            Sleeping = false;

            _inFlight = true;
            _flightTime = 0;
            _stillTime = 0;
            _knocked = false;
            _looseTime = 0;
            ForgetWobble();

            // a die given up on was frozen where it lay; any kick is a fresh throw
            Freeze = false;
        }
    }
}
