using Core.Characters;
using Core.Resolution;
using Core.Words;

namespace Core.Magic
{
    // what one effect did to one creature. a casting is a list of these, one per target per
    // effect, and nothing in it is a decision - it is a report.
    public sealed class Landing
    {
        public Landing(SpellEffect effect, Actor target, bool landed, int amount = 0,
                       Attempt attempt = null, Condition condition = Condition.None)
        {
            Effect = effect;
            Target = target;
            Landed = landed;
            Amount = amount;
            Attempt = attempt;
            Condition = condition;
        }

        public SpellEffect Effect { get; }

        public Actor Target { get; }

        public bool Landed { get; }

        // damage dealt, hit points healed, temporary hit points granted
        public int Amount { get; }

        // the attack roll or the saving throw, when there was one
        public Attempt Attempt { get; }

        public Condition Condition { get; }

        public override string ToString() =>
            $"{Effect.Kind.Id()} on {Target?.Id ?? "the ground"}: " +
            (Landed ? "landed" : "resisted") + (Amount != 0 ? $" {Amount}" : "");
    }
}
