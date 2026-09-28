using Godot;

namespace Game.Dice
{
    public partial class DieBody
    {
        public override void _Ready()
        {
            _rng.Randomize();

            Recovery ??= new NudgeThenRethrow(MaxNudges, MaxCockedRethrows, MaxLostRethrows, MaxRestlessRethrows);

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

            GetNode<CollisionShape3D>("CollisionShape3D").Shape = DieParts.BuildHull(solid);
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
            _restlessRethrows = 0;
            Launch(from);
        }

        // split from Throw so the cocked and escaped paths re-throw without resetting the attempt counters that bound them
        // energy scales the horizontal push; at zero the die drops inside the tray and cannot escape
        void Launch(Transform3D from, float energy = 1f)
        {
            _lastThrowFrom = from;
            SettledValue = 0;
            _nudges = 0;

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

            // a die given up on was frozen where it lay; any kick is a fresh throw
            Freeze = false;
        }
    }
}
