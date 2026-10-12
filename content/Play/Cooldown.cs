namespace Content.Play
{
    // NOT AGAIN SO SOON: something that may happen once and then not for so many seconds of play - the GM screen's
    // rattle, a companion's bark (cc_task_i). the clock is the caller's (the table's seconds of play, a test's
    // numbers), so this is testable without one
    public sealed class Cooldown
    {
        double _last;
        bool _ever;

        public Cooldown(double seconds) => Seconds = seconds;

        public double Seconds { get; }

        public bool Ready(double now) => !_ever || now - _last >= Seconds;

        // it happens now, and the wait starts again
        public void Mark(double now)
        {
            _last = now;
            _ever = true;
        }

        // happens now if it may, and says whether it did
        public bool Take(double now)
        {
            if (!Ready(now)) return false;

            Mark(now);
            return true;
        }

        public override string ToString() => $"{Seconds:0} seconds" + (_ever ? $", last at {_last:0}" : "");
    }
}
