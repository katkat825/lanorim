using Godot;
using Game.Audio;

namespace Game.Dice
{
    public partial class DieBody
    {
        public override void _IntegrateForces(PhysicsDirectBodyState3D state)
        {
            // listen before the kick: the state's contacts are what the last step solved, so reading them after a teleport describes an address the die has left
            if (_reportContacts) Listen(state);

            Calm(state);

            if (!_kickQueued) return;
            _kickQueued = false;

            if (_kick.Reposition) state.Transform = _kick.Where;

            // set velocities outright, not as impulses: a die's tumble should be the spin it was given, not what its inertia makes of a torque
            state.LinearVelocity = _kick.Linear;
            state.AngularVelocity = _kick.Angular;

            _inFlight = true;
            _flightTime = 0;
            _stillTime = 0;
        }

        // summed, not per contact: a flat landing touches at several corners in one step, and the sum is what makes a flat slam bigger than a corner tap
        // rising edge only: a bounce is in contact for several steps at 120 Hz, so only the onset fires
        // mass-independent: impulse over mass is the speed the collision cost the die
        void Listen(PhysicsDirectBodyState3D state)
        {
            _sinceHit += state.Step;

            int contacts = state.GetContactCount();

            float impulse = 0f;
            float flatness = 0f;
            bool againstDie = false;

            for (int i = 0; i < contacts; i++)
            {
                // sum magnitudes, not vectors: a die wedged between two walls is hit twice, and vector-adding would cancel them into silence
                float force = state.GetContactImpulse(i).Length();
                impulse += force;

                // weighted by force, so the surface reported is the one that did the hitting
                flatness += Mathf.Abs(state.GetContactLocalNormal(i).Dot(Vector3.Up)) * force;

                // the tray is a StaticBody3D and dice are RigidBody3D, so the cases separate themselves
                if (!againstDie && state.GetContactColliderObject(i) is RigidBody3D) againstDie = true;
            }

            float speed = Mass > 0f ? impulse / Mass : 0f;
            bool struck = speed >= MinHitSpeed;

            if (struck && !_wasStruck && _sinceHit >= MinHitGap)
            {
                _hits++;
                _sinceHit = 0;

                Struck?.Invoke(new DieHit(
                    Size,
                    impulse,
                    speed,
                    impulse > 0f ? flatness / impulse : 0f,
                    againstDie,

                    // fastest-moving point, not the centre: a die can drift slowly while spinning hard, which is a tumble not a settle
                    state.LinearVelocity.Length() + state.AngularVelocity.Length() * Solid.Circumradius,

                    _hits == 1));
            }

            _wasStruck = struck;
            _contactsNow = contacts;
            _onDieNow = againstDie;
        }
    }
}
