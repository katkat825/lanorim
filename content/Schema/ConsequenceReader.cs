using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Core.Characters;
using Core.Resolution;
using Core.Words;

namespace Content.Schema
{
    // the nat-1 / nat-20 pool. the keeper delta of updated_decisions.md: the check passes or fails
    // on its numbers alone, and the natural 1 or 20 buys something from here *regardless* - which
    // is also how you can take damage outside combat.
    public static class ConsequenceReader
    {
        public static bool TryRead(string text, out IReadOnlyList<Consequence> consequences,
                                   out IReadOnlyList<string> problems) =>
            Entries.TryRead(text, out consequences, out problems);

        // every key a consequence takes
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "id", "polarity", "kind", "ability", "condition", "amount", "scales_with_level", "weight",
        };

        // the file: { "consequences": [ ... ] }, each entry read by ReadOne (EntryList)
        public static readonly EntryList<Consequence> Entries =
            new EntryList<Consequence>("consequences", "consequence", Keys, ReadOne, BothSides);

        // a pool with nothing on one side is a natural 20 that does nothing, which is the one
        // thing the delta is for
        static void BothSides(List<Consequence> found, List<string> problems)
        {
            if (found.All(c => c.Polarity != Polarity.Bane)) problems.Add("no banes in the pool");

            if (found.All(c => c.Polarity != Polarity.Boon)) problems.Add("no boons in the pool");
        }

        static Consequence ReadOne(JsonElement entry, string id, List<string> problems)
        {
            string polarityText = entry.Text("polarity", "bane");

            Polarity polarity = polarityText == "boon" ? Polarity.Boon : Polarity.Bane;

            if (polarityText != "boon" && polarityText != "bane")
                problems.Add($"{id}: '{polarityText}' is not 'bane' or 'boon'");

            if (!EnumWords.TryParse(entry.Text("kind"), out ConsequenceKind kind))
            {
                problems.Add($"{id}: '{entry.Text("kind")}' is not a kind of consequence " +
                             "(health, gold, ability_shift, condition, flavour)");
                return null;
            }

            Ability ability = entry.Ability("ability", problems, id) ?? Ability.Strength;
            Condition condition = entry.Condition("condition", problems, id);

            var one = new Consequence(id, polarity, kind,
                                      entry.Dice("amount", problems, id),
                                      ability, condition,
                                      entry.Weight(id, problems),
                                      entry.Flag("scales_with_level"));

            switch (kind)
            {
                case ConsequenceKind.Health:
                case ConsequenceKind.Gold:
                    if (one.Amount.IsNothing && !one.ScalesWithLevel)
                        problems.Add($"{id}: a {kind} consequence of nothing");
                    break;

                case ConsequenceKind.AbilityShift:
                    if (one.Amount.IsNothing)
                        problems.Add($"{id}: a shift of nothing - give it an 'amount' like -1");

                    if (!entry.Has("ability"))
                        problems.Add($"{id}: a shift needs to say which ability");
                    break;

                case ConsequenceKind.Condition:
                    if (condition == Condition.None)
                        problems.Add($"{id}: a condition consequence with no condition");
                    break;
            }

            return one;
        }

        public static ConsequencePool Srd() =>
            new ConsequencePool(Schema.Srd.ReadAll<Consequence>("consequences", TryRead, null));
    }

    // what a drawn consequence actually does. the pool says which one; this is the only place that
    // carries it out, so a boon and a bane cannot drift into two code paths.
    public static class Consequences
    {
        public sealed class Visit
        {
            public Visit(Consequence consequence, int amount)
            {
                Consequence = consequence;
                Amount = amount;
            }

            public Consequence Consequence { get; }

            // hit points lost or gained, gold found, the size of the shift
            public int Amount { get; }

            public string LineKey => Consequence.LineKey;

            public override string ToString() =>
                $"{Consequence.Id}: {Amount}";
        }

        public static Visit Befall(Consequence consequence, IResolver resolver, Actor actor,
                                   Inventory.Pack pack = null)
        {
            if (consequence == null || resolver == null || actor == null) return null;

            int amount = consequence.Measure(resolver, actor.Level);

            switch (consequence.Kind)
            {
                case ConsequenceKind.Health:
                    if (consequence.Polarity == Polarity.Bane)
                        // yes, outside combat: updated_decisions.md says so in as many words
                        actor.Suffer(Math.Abs(amount), DamageType.Bludgeoning);
                    else
                        actor.Mend(Math.Abs(amount));
                    break;

                case ConsequenceKind.Gold:
                    if (pack == null) break;

                    // the amount is gold; the purse is copper (Coins)
                    if (consequence.Polarity == Polarity.Boon) pack.Earn(Inventory.Coins.FromGold(Math.Abs(amount)));
                    else pack.Spend(Math.Min(pack.Copper, Inventory.Coins.FromGold(Math.Abs(amount))));
                    break;

                case ConsequenceKind.AbilityShift:
                    actor.Scores.ShiftUntilRest(consequence.Ability, amount);
                    break;

                case ConsequenceKind.Condition:
                    if (consequence.Polarity == Polarity.Bane) actor.Apply(consequence.Condition);
                    else actor.Remove(consequence.Condition);
                    break;
            }

            return new Visit(consequence, amount);
        }
    }
}
