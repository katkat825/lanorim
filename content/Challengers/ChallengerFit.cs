using System;
using System.Collections.Generic;
using System.Linq;
using Content.Encounters;
using Content.Monsters;
using Core.Dice;
using Core.Tables;

namespace Content.Challengers
{
    // FITTING A CHALLENGER TO THE HERO (cc_task_cg 3.2), on the solo budget the campaign check already holds fights to
    // (FightBudget: SRD 5.2.1's XP Budget per Character for one character, and the solo enemy cap).
    //
    // A single or a fixed group has the difficulty its XP comes to: the lowest of low, moderate and high whose budget
    // it fits. It is OUT OF BAND when it's over the high budget, or under half the low one (a fight the hero can't
    // feel). A team is drawn to a difficulty instead (Team)
    public static class ChallengerFit
    {
        // under this share of the low budget, a single or a group is too easy to be offered
        public const double Floor = 0.5;

        public static EncounterDifficulty DifficultyOf(int xp, int level)
        {
            foreach (EncounterDifficulty difficulty in Enum.GetValues<EncounterDifficulty>())
                if (xp <= FightBudget.BudgetFor(level, difficulty))
                    return difficulty;

            return EncounterDifficulty.High;
        }

        // how far outside the band it is, as a ratio: 0 when it fits, more the further out (the "closest ones" when
        // nothing fits are the smallest)
        public static double Distance(int xp, int level)
        {
            double floor = FightBudget.BudgetFor(level, EncounterDifficulty.Low) * Floor;
            double top = FightBudget.BudgetFor(level, EncounterDifficulty.High);

            if (xp < floor) return floor / Math.Max(1, xp) - 1;

            return xp > top ? xp / top - 1 : 0;
        }

        public static bool Fits(int xp, int level) => Distance(xp, level) == 0;

        public static int XpOf(IEnumerable<Mustered> group, Func<string, Monster> find) =>
            group.Sum(m => find(m.Monster) is Monster monster ? FightBudget.Xp(monster) * m.Count : 0);

        // a single's or a group's monsters, at the most their dice come to: what the roster shows and the fight fields
        public static IReadOnlyList<Mustered> Fixed(IEnumerable<Band> bands) =>
            bands.Select(b => new Mustered(b.Monster, Math.Max(1, b.Count.Maximum))).ToList();

        // A TEAM: a difficulty rolled (low, moderate or high), then members drawn at random from its monsters while one
        // fits what's left of that budget and the enemy cap allows another. Never empty: a team whose every monster is
        // over the budget fields its cheapest one. The members are gathered by monster, in the type's order
        public static IReadOnlyList<Mustered> Team(IReadOnlyList<Band> pool, Func<string, Monster> find, int level,
                                                   IRng rng, out EncounterDifficulty aimed)
        {
            var difficulties = Enum.GetValues<EncounterDifficulty>();
            aimed = difficulties[rng.Roll(difficulties.Length) - 1];

            var priced = pool.Select(b => (b.Monster, Xp: find(b.Monster) is Monster m ? FightBudget.Xp(m) : 0))
                             .Where(p => p.Xp > 0)
                             .ToList();

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            if (priced.Count == 0) return Array.Empty<Mustered>();

            int left = FightBudget.BudgetFor(level, aimed);
            int cap = FightBudget.Cap(level);

            for (int members = 0; members < cap; members++)
            {
                var affordable = priced.Where(p => p.Xp <= left).ToList();

                if (affordable.Count == 0) break;

                var pick = affordable[rng.Roll(affordable.Count) - 1];

                counts[pick.Monster] = counts.GetValueOrDefault(pick.Monster) + 1;
                left -= pick.Xp;
            }

            if (counts.Count == 0) counts[priced.OrderBy(p => p.Xp).First().Monster] = 1;

            return priced.Select(p => p.Monster).Distinct()
                         .Where(counts.ContainsKey)
                         .Select(m => new Mustered(m, counts[m]))
                         .ToList();
        }
    }
}
