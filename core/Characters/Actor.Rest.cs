using System.Linq;
using Core.Words;

namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- rest -----------------------------------------------------------------------------

        public void ShortRest()
        {
            Health.ShortRest();
            Boons.Rested();
            Scores.Rested();
        }

        public void Raise(int at = 1)
        {
            IsDead = false;
            Health.Revive(at);
            Remove(Condition.Unconscious);
        }

        public void LongRest()
        {
            // SRD 5.2.1: a rest needs at least 1 hit point to start
            if (IsDead || IsDown) return;

            Health.LongRest();
            Scores.Rested();
            ClearConditions();
            EndConcentration();
            Boons.LongRested();
        }

        public override string ToString() =>
            $"{Id} (level {Level} {EnumWords.Name(Side)}) {Health}, ac {ArmorClass}" +
            (_conditions.Count > 0
                ? ", " + string.Join(" ", _conditions.Select(c => c.Id()))
                : "");
    }
}
