using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Localization;

namespace Content.Sheet
{
    // ONE ABILITY SCORE IMPROVEMENT, AS THE PLAYER SPENT IT: +2 to one score, or +1 to two
    // (decisions_checklist.md section 1, 2026-09-24 - the player chooses; feats stay deferred).
    public readonly struct AbilityImprovement : IEquatable<AbilityImprovement>
    {
        AbilityImprovement(Ability first, Ability? second)
        {
            First = first;
            Second = second;
        }

        public Ability First { get; }

        // null is "+2 to First"; set is "+1 to First and +1 to Second"
        public Ability? Second { get; }

        public bool IsDouble => !Second.HasValue;

        public static AbilityImprovement Two(Ability ability) => new AbilityImprovement(ability, null);

        public static AbilityImprovement OneEach(Ability first, Ability second) =>
            new AbilityImprovement(first, second);

        // what it adds, score by score
        public IEnumerable<(Ability ability, int points)> Points =>
            Second.HasValue
                ? new[] { (First, 1), (Second.Value, 1) }
                : new[] { (First, 2) };

        // the save's words: "str" or "dex+con", in a fixed order so the save diffs
        public string Word =>
            Second.HasValue ? $"{First.Id()}+{Second.Value.Id()}" : First.Id();

        public static bool TryParse(string word, out AbilityImprovement improvement)
        {
            improvement = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string[] parts = word.Split('+');

            if (parts.Length == 1 && Abilities.TryParse(parts[0], out Ability only))
            {
                improvement = Two(only);
                return true;
            }

            if (parts.Length == 2 && Abilities.TryParse(parts[0], out Ability a) &&
                Abilities.TryParse(parts[1], out Ability b) && a != b)
            {
                improvement = OneEach(a, b);
                return true;
            }

            return false;
        }

        public bool Equals(AbilityImprovement other) =>
            First == other.First && Second == other.Second;

        public override bool Equals(object obj) => obj is AbilityImprovement other && Equals(other);

        public override int GetHashCode() => ((int)First * 31) ^ (Second.HasValue ? (int)Second.Value + 7 : 0);

        public override string ToString() => Second.HasValue
            ? $"+1 {First.Id()}, +1 {Second.Value.Id()}"
            : $"+2 {First.Id()}";
    }

    // why an improvement was refused, as a key the level-up screen shows
    public static class ImprovementRefusals
    {
        public const string NonePending = "none_pending";
        public const string SameTwice = "same_twice";
        public const string OverTwenty = "over_twenty";

        public static string Key(string why) =>
            KeyConventions.Key(KeyConventions.UiNs, "improvement", why, "name");

        public static IEnumerable<string> Keys() =>
            new[] { NonePending, SameTwice, OverTwenty }.Select(Key)
                .Concat(new[]
                {
                    KeyConventions.Key(KeyConventions.UiNs, "improvement", "pending", "name"),
                    KeyConventions.Key(KeyConventions.UiNs, "improvement", "two", "name"),
                    KeyConventions.Key(KeyConventions.UiNs, "improvement", "one_each", "name"),
                    KeyConventions.Key(KeyConventions.UiNs, "improvement", "suggested", "name"),
                });

        // the rule, for a set of scores: +2/+0 or +1/+1, two different abilities for +1/+1, and
        // nothing past 20. null when it is fine
        public static string Check(AbilityImprovement choice, Func<Ability, int> score)
        {
            if (choice.Second.HasValue && choice.Second.Value == choice.First) return SameTwice;

            foreach ((Ability ability, int points) in choice.Points)
                if (score(ability) + points > Abilities.Ceiling) return OverTwenty;

            return null;
        }
    }

    public sealed partial class Hero
    {
        readonly List<AbilityImprovement> _improvements = new List<AbilityImprovement>();

        // the improvements this character has spent, in the order it spent them
        public IReadOnlyList<AbilityImprovement> Improvements => _improvements;

        // how many its level has given that are not spent yet. reaching an ASI level adds one;
        // nothing spends it on the player's behalf - the level-up screen does, when the player says
        public int PendingImprovements =>
            Math.Max(0, AbilityScoreImprovements(Level, Class) - _improvements.Count);

        // SPEND ONE, OR SAY WHY NOT (Improve, because Spend is the action economy's word). the
        // scores move, and then everything that reads them is read again: the hit point maximum
        // (Constitution) is rebuilt, keeping the damage taken; armor class, saves, skills, attack
        // bonuses and the spell DC read the scores live already
        public bool Improve(AbilityImprovement choice, out string whyNotKey)
        {
            whyNotKey = null;

            if (PendingImprovements <= 0)
            {
                whyNotKey = ImprovementRefusals.Key(ImprovementRefusals.NonePending);
                return false;
            }

            string why = ImprovementRefusals.Check(choice, a => Actor.Scores.Base(a));

            if (why != null)
            {
                whyNotKey = ImprovementRefusals.Key(why);
                return false;
            }

            Apply(choice);
            _improvements.Add(choice);

            Rederive();

            return true;
        }

        public bool Improve(AbilityImprovement choice) => Improve(choice, out _);

        void Apply(AbilityImprovement choice)
        {
            foreach ((Ability ability, int points) in choice.Points)
                Actor.Scores.Raise(ability, points);
        }

        // THE OLD RULE, KEPT AS A SUGGESTION: +2 on the class's first priority while it has room,
        // then the next. the level-up screen pre-fills it; the sim and any hero the player does not
        // drive spend it (ImproveAsSuggested). never applied to the player's hero on its own
        public AbilityImprovement Suggested()
        {
            List<Ability> order = Class.Priority.Concat(Abilities.All).Distinct().ToList();

            foreach (Ability ability in order)
                if (Actor.Scores.Base(ability) <= Abilities.Ceiling - 2)
                    return AbilityImprovement.Two(ability);

            // everything is at 19 or 20: +1 to two that are at 19, if there are two
            List<Ability> nineteen = order.Where(a => Actor.Scores.Base(a) < Abilities.Ceiling).ToList();

            return nineteen.Count >= 2
                ? AbilityImprovement.OneEach(nineteen[0], nineteen[1])
                : AbilityImprovement.Two(order[0]);
        }

        // for the sim, and any hero nobody is choosing for: spend every pending one as suggested
        public int ImproveAsSuggested()
        {
            int spent = 0;

            while (PendingImprovements > 0 && Improve(Suggested())) spent++;

            return spent;
        }

        // the hit point maximum is the one number the sheet stores; everything else is read live
        void Rederive()
        {
            int missing = Actor.Health.Maximum - Actor.Health.Current;
            int temporary = Actor.Health.Temporary;
            int dice = Actor.Health.HitDice;

            Actor.SetHealth(new Health(
                Class.HitPointsAt(Level, Actor.AbilityModifier(Ability.Constitution)),
                Class.HitDie, Level));

            Actor.Health.Take(Math.Max(0, missing));
            Actor.Health.GrantTemporary(temporary);
            Actor.Health.SetHitDice(dice);
        }

        // a save or creation handing back improvements already spent: each is checked against the
        // rule as it is applied, and the ones that no longer fit are returned rather than forced
        internal IReadOnlyList<AbilityImprovement> Respend(IEnumerable<AbilityImprovement> spent)
        {
            var refused = new List<AbilityImprovement>();

            foreach (AbilityImprovement choice in spent ?? Enumerable.Empty<AbilityImprovement>())
            {
                if (PendingImprovements <= 0 ||
                    ImprovementRefusals.Check(choice, a => Actor.Scores.Base(a)) != null)
                {
                    refused.Add(choice);
                    continue;
                }

                Apply(choice);
                _improvements.Add(choice);
            }

            return refused;
        }
    }
}
