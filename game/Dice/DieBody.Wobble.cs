using Godot;

namespace Game.Dice
{
    public partial class DieBody
    {
        // --- a die that wobbles (cc_task_working-notes-10-01.md 1.1) --------------------------------------
        //
        // Kathleen: "dice just keep wobbling/vibrating at the same rate and never slow". FOUND (2026-10-03):
        // it was the d4 and the d6, never the d20, and it was the physics step. Godot Physics gives a resting
        // solid a face contact (three or four points) only while that face is within about a degree of flat;
        // past that it gives an edge (two points). At 60 steps a second a d4 lying on its face tips further
        // than that in one step, so every step it stands on two corners, rocks onto the next pair, and goes
        // round its three corners every three steps, for good: 7 rad/s of spin that never decays, the same on
        // every die (a limit cycle, not a throw). Damping, bounce, CCD, contact tolerances and solver
        // iterations changed nothing; 120 steps a second ended it. The old build ran at 120; the harvest
        // left project.godot's [physics] section behind, and this code's comments still said "at 120 Hz".
        //
        // A wobble, measured: a die in contact with something, moving slowly (its fastest point under
        // WobbleCeiling) but never slowly enough to count as still, for longer than WobbleSeconds. A tumble
        // that is slowing down passes through this band in well under a second. Logged once per throw as
        // "wobbling", with what it was touching, so a sweep can count them (developer diagnostic, not
        // localized).
        [Export] public float WobbleCeiling { get; set; } = 0.4f;

        [Export] public float WobbleSeconds { get; set; } = 2f;

        double _wobbleTime;
        bool _wobbleLogged;

        // seen wobbling for WobbleSeconds: the ceiling's nudge comes now rather than at MaxFlightSeconds
        bool Wobbling => _wobbleTime >= WobbleSeconds;

        // what the last physics step touched, kept by Listen for the wobble line
        int _contactsNow;
        bool _onDieNow;

        // the speed of the die's fastest point: a die can drift slowly while spinning hard
        float PointSpeed => LinearVelocity.Length() + AngularVelocity.Length() * Solid.Circumradius;

        void WatchWobble(double delta, bool still)
        {
            bool slow = !still && _contactsNow > 0 && PointSpeed < WobbleCeiling;
            _wobbleTime = slow ? _wobbleTime + delta : 0;

            if (_wobbleTime < WobbleSeconds || _wobbleLogged) return;

            _wobbleLogged = true;

            (int value, float alignment) = ReadFace();
            GD.Print($"{Name}: wobbling for {_wobbleTime:0.00}s at {_flightTime:0.00}s - linear {LinearVelocity.Length():0.000}, " +
                     $"angular {AngularVelocity.Length():0.000}, {_contactsNow} contacts{(_onDieNow ? " (on a die)" : "")}, " +
                     $"face {value} at {alignment:0.000}, height {(TraySpace?.ToLocal(GlobalPosition) ?? GlobalPosition).Y:0.000}");
        }

        // SLOWING TO A STOP. A die lying on a face (the face it would be read by is flat enough to count) and
        // moving slowly has nothing left to decide: what is left is a spin on the felt or a rock on its
        // corners, and below SlowSpeed it bleeds off at SlowDamp a second. Only on a face: a die on an edge or
        // on a neighbour is left to fall as it would (damping every slow die was tried on 09-28 and left dice
        // sliding slowly off their neighbours for good).
        //
        // OFF (0), MEASURED: at 120 ticks, 300 throws of four d4s, d6s and d20s each with SlowDamp 4 and with
        // 0 wobbled once and zero times, cocked 113 and 79 times, and left the tray 56 and 48 times - nothing
        // it fixes, and a little more cocking, within the noise. At 60 ticks a damp of 3 on every die changed
        // nothing either: the rocking was the solver's, put back every step. A dial for Kathleen, left at 0
        [Export] public float SlowSpeed { get; set; } = 0.15f;

        [Export] public float SlowDamp { get; set; } = 0f;

        void Calm(PhysicsDirectBodyState3D state)
        {
            if (SlowDamp <= 0f || !_inFlight || state.GetContactCount() == 0) return;

            Vector3 linear = state.LinearVelocity;
            if (linear.Length() + state.AngularVelocity.Length() * Solid.Circumradius >= SlowSpeed) return;

            if (_faces.Read(state.Transform.Basis).Alignment < RequiredAlignment) return;

            float keep = Mathf.Max(0f, 1f - SlowDamp * (float)state.Step);

            // across the felt only: gravity and the felt settle the up-and-down between them
            state.AngularVelocity *= keep;
            state.LinearVelocity = new Vector3(linear.X * keep, linear.Y, linear.Z * keep);
        }

        void ForgetWobble()
        {
            _wobbleTime = 0;
            _wobbleLogged = false;
        }
    }
}
