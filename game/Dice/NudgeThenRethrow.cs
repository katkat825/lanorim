namespace Game.Dice
{
    public sealed class NudgeThenRethrow : IDieRecovery
    {
        readonly int _maxNudges;
        readonly int _maxRethrows;
        readonly int _maxEscapes;

        readonly int _maxRestless;

        public NudgeThenRethrow(int maxNudges, int maxRethrows, int maxEscapes, int maxRestless = 1)
        {
            _maxNudges = maxNudges;
            _maxRethrows = maxRethrows;
            _maxEscapes = maxEscapes;
            _maxRestless = maxRestless;
        }

        public DieRecoveryStep Cocked(in CockedDie die)
        {
            if (die.NudgesSoFar < _maxNudges) return DieRecoveryStep.Nudge;
            if (die.RethrowsSoFar < _maxRethrows) return DieRecoveryStep.Rethrow();

            // accept rather than loop forever when still wedged; DieBody logs it loudly
            return DieRecoveryStep.Accept;
        }

        public DieRecoveryStep Escaped(in StrayDie die) => Fading(die.TimesSoFar, _maxEscapes);

        // past the hard ceiling: nudged (at most _maxRestless times), then read as it lies. never thrown again
        public DieRecoveryStep Restless(in StrayDie die) =>
            die.TimesSoFar <= _maxRestless ? DieRecoveryStep.Nudge : DieRecoveryStep.Accept;

        // the escape's answer: thrown again with less energy each time, so the last one drops inside the
        // tray, then accepted where it lies
        static DieRecoveryStep Fading(int soFar, int most)
        {
            if (soFar > most) return DieRecoveryStep.Accept;

            return DieRecoveryStep.Rethrow(1f - (float)soFar / most);
        }
    }
}
