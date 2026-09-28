using Content.Inventory;
using Core.Characters;
using Core.Magic;
using Core.Resolution;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // --- resting ---------------------------------------------------------------------------

        // short rest: spend hit dice by choice, and everything per-rest comes back
        public int ShortRest(IResolver resolver, int hitDiceToSpend = 0)
        {
            // SRD 5.2.1: a rest needs at least 1 hit point to start
            if (Actor.IsDown) return 0;

            Revert();

            int healed = 0;

            for (int i = 0; i < hitDiceToSpend; i++)
                healed += Actor.Health.SpendHitDie(resolver,
                                                   Actor.AbilityModifier(Ability.Constitution));

            Actor.ShortRest();

            // gives nothing back in either mode today; the call is here so that if a short rest
            // ever does, it is one line and not a thing somebody has to remember to add
            Caster?.Rested(Rest.Short);

            RestoreAfterShortRest();
            Budget.LongRest();

            Equipment.Apply(Actor);

            return healed;
        }

        // long rest: full hit points, the delta from SRD (updated_decisions.md)
        public void LongRest()
        {
            // SRD 5.2.1: a rest needs at least 1 hit point to start - nothing comes back, slots
            // and features included, not only the hit points
            if (Actor.IsDown || Actor.IsDead) return;

            Revert();

            Actor.LongRest();

            // slots all come back; points refills and forgets which high spells went off today
            Caster?.Rested(Rest.Long);

            // Goodberry's berries last a day
            Pack.Vanish();

            _spent.Clear();
            Budget.LongRest();

            Rerolls();

            Equipment.Apply(Actor);
        }
    }
}
