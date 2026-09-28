namespace Game.Dice
{
    public sealed class NudgeThenRethrow : IDieRecovery
    {
        readonly int _maxNudges;
        readonly int _maxRethrows;
        readonly int _maxEscapes;

        readonly int _maxRestless;

        public NudgeThenRethrow(int maxNudges, int maxRethrows, int maxEscapes, int maxRestless = 3)
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

        public DieRecoveryStep Escaped(in EscapedDie die) => Fading(die.EscapesSoFar, _maxEscapes);

        public DieRecoveryStep Restless(in RestlessDie die) => Fading(die.RethrowsSoFar, _maxRestless);

        // the escape's answer, and a restless die's: thrown again with less energy each time, so the
        // last one drops inside the tray, then accepted where it lies
        static DieRecoveryStep Fading(int soFar, int most)
        {
            if (soFar > most) return DieRecoveryStep.Accept;

            return DieRecoveryStep.Rethrow(1f - (float)soFar / most);
        }
    }
}
