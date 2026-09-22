using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Localization;

namespace Core.Resolution
{
    // a natural 1 or 20 on a check changes something *whatever the numbers said*
    // (updated_decisions.md). the check still passes or fails on its total alone; this is the
    // side-effect, and it is the reason a check can hurt you outside combat.
    public enum ConsequenceKind
    {
        // lose (bane) or gain (boon) hit points
        Health,

        // gold found, or dropped
        Gold,

        // a shift to one ability score that lasts until the next rest, short or long
        AbilityShift,

        // one of the v1 conditions, until the next rest
        Condition,

        // nothing mechanical - a line of flavour the narrator reads
        Flavour,
    }

    public enum Polarity
    {
        // drawn on a natural 1
        Bane,

        // drawn on a natural 20
        Boon,
    }

    public sealed class Consequence
    {
        public Consequence(string id, Polarity polarity, ConsequenceKind kind,
                           DiceRoll amount = default, Ability ability = Ability.Strength,
                           Condition condition = Condition.None, int weight = 1,
                           bool scalesWithLevel = false)
        {
            Id = id;
            Polarity = polarity;
            Kind = kind;
            Amount = amount;
            Ability = ability;
            Condition = condition;
            Weight = weight < 1 ? 1 : weight;
            ScalesWithLevel = scalesWithLevel;
        }

        public string Id { get; }

        public Polarity Polarity { get; }

        public ConsequenceKind Kind { get; }

        // how much health, how much gold, how big the shift. a shift of -1 is written "-1".
        public DiceRoll Amount { get; }

        public Ability Ability { get; }

        public Condition Condition { get; }

        // relative chance of being drawn against the others of its polarity
        public int Weight { get; }

        // "a pouch with 5 + level gold in it" - the hero's level is added after the roll
        public bool ScalesWithLevel { get; }

        public string NameKey => KeyConventions.Key(KeyConventions.ConsequenceNs, Id, "name");

        public string LineKey => KeyConventions.Key(KeyConventions.ConsequenceNs, Id, "line");

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return LineKey;
        }

        public int Measure(IResolver resolver, int level)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            return resolver.Roll(Amount) + (ScalesWithLevel ? level : 0);
        }

        public override string ToString() =>
            $"{Id} [{Polarity.ToString().ToLowerInvariant()}] {Kind}" +
            (Amount.IsNothing ? "" : $" {Amount}") +
            (Kind == ConsequenceKind.AbilityShift ? $" to {Ability.Id()}" : "") +
            (Kind == ConsequenceKind.Condition ? $" {Condition.Id()}" : "") +
            (ScalesWithLevel ? " + level" : "");
    }

    // the pool a natural 1 or 20 draws from. weighted, and it never runs dry - the same
    // consequence may come up twice in a campaign, which is fine and cheaper than tracking a bag.
    public sealed class ConsequencePool
    {
        readonly List<Consequence> _all;

        public ConsequencePool(IEnumerable<Consequence> consequences) =>
            _all = (consequences ?? Enumerable.Empty<Consequence>()).Where(c => c != null).ToList();

        public IReadOnlyList<Consequence> All => _all;

        public IEnumerable<Consequence> Of(Polarity polarity) =>
            _all.Where(c => c.Polarity == polarity);

        public bool Has(Polarity polarity) => Of(polarity).Any();

        // null when nothing of that polarity is in the pool - an empty pool is a quiet campaign,
        // not a crash
        public Consequence Draw(IRng rng, Polarity polarity)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            List<Consequence> candidates = Of(polarity).ToList();

            if (candidates.Count == 0) return null;

            int total = candidates.Sum(c => c.Weight);
            int ticket = rng.Roll(total);

            foreach (Consequence c in candidates)
            {
                ticket -= c.Weight;

                if (ticket <= 0) return c;
            }

            return candidates[candidates.Count - 1];
        }

        // the attempt decides whether anything is drawn at all, and which side of the pool
        public Consequence DrawFor(IRng rng, Attempt attempt)
        {
            if (attempt == null || !attempt.DrawsConsequence) return null;

            return Draw(rng, attempt.Roll.IsNaturalOne ? Polarity.Bane : Polarity.Boon);
        }

        public ConsequencePool With(IEnumerable<Consequence> more) =>
            new ConsequencePool(_all.Concat(more ?? Enumerable.Empty<Consequence>()));

        public override string ToString() =>
            $"{_all.Count} consequences: {Of(Polarity.Bane).Count()} banes, " +
            $"{Of(Polarity.Boon).Count()} boons";
    }
}
