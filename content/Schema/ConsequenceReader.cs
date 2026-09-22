using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Core.Characters;
using Core.Resolution;

namespace Content.Schema
{
    // the nat-1 / nat-20 pool. the keeper delta of updated_decisions.md: the check passes or fails
    // on its numbers alone, and the natural 1 or 20 buys something from here *regardless* - which
    // is also how you can take damage outside combat.
    public static class ConsequenceReader
    {
        public static bool TryRead(string text, out IReadOnlyList<Consequence> consequences,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<Consequence>();
            var trouble = new List<string>();

            consequences = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                foreach (JsonElement entry in document.RootElement.Items("consequences"))
                {
                    Consequence one = ReadOne(entry, trouble);

                    if (one != null) found.Add(one);
                }

                if (found.Count == 0 && trouble.Count == 0)
                    trouble.Add("no consequences in it");
            }

            // a pool with nothing on one side is a natural 20 that does nothing, which is the one
            // thing the delta is for
            if (found.All(c => c.Polarity != Polarity.Bane)) trouble.Add("no banes in the pool");

            if (found.All(c => c.Polarity != Polarity.Boon)) trouble.Add("no boons in the pool");

            return trouble.Count == 0;
        }

        static Consequence ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a consequence id");
                return null;
            }

            string polarityText = entry.Text("polarity", "bane");

            Polarity polarity = polarityText == "boon" ? Polarity.Boon : Polarity.Bane;

            if (polarityText != "boon" && polarityText != "bane")
                problems.Add($"{id}: '{polarityText}' is not 'bane' or 'boon'");

            if (!TryKind(entry.Text("kind"), out ConsequenceKind kind))
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
                                      entry.Number("weight", 1),
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

        static bool TryKind(string id, out ConsequenceKind kind)
        {
            switch ((id ?? "").ToLowerInvariant())
            {
                case "health": kind = ConsequenceKind.Health; return true;
                case "gold": kind = ConsequenceKind.Gold; return true;
                case "ability_shift": kind = ConsequenceKind.AbilityShift; return true;
                case "condition": kind = ConsequenceKind.Condition; return true;
                case "flavour": kind = ConsequenceKind.Flavour; return true;

                default: kind = ConsequenceKind.Flavour; return false;
            }
        }

        public static ConsequencePool Srd()
        {
            var all = new List<Consequence>();

            foreach ((string _, string text) in Srd_Files())
            {
                TryRead(text, out IReadOnlyList<Consequence> read, out _);

                all.AddRange(read);
            }

            return new ConsequencePool(all);
        }

        static IEnumerable<(string Path, string Text)> Srd_Files() =>
            Schema.Srd.ReadFolder("consequences");
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

                    if (consequence.Polarity == Polarity.Boon) pack.Earn(Math.Abs(amount));
                    else pack.Spend(Math.Min(pack.Gold, Math.Abs(amount)));
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
