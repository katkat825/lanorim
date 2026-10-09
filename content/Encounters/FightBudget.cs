using System;
using System.Collections.Generic;
using System.Linq;
using Content.Monsters;
using Content.Schema;
using Core.Tables;

namespace Content.Encounters
{
    // THE SOLO FIGHT BUDGET (cc_task_h 3.5; docs/campaign_authoring_rules.md). A fight may say the level it's meant
    // for and how hard it's meant to be ("level": 1, "difficulty": "low"); the campaign check then holds it to SRD
    // 5.2.1's XP Budget per Character for ONE character - one hero, not a party - and to the solo enemy cap. It WARNS
    // (a ContentProblem caution) and never refuses: the author may mean it. A fight with no level gets a note that it
    // can't be checked. The monsters are counted at the most their dice can come to.
    public static class FightBudget
    {
        // SRD 5.2.1, Combat Encounter Difficulty: XP Budget per Character, by level - low, moderate, high
        static readonly int[,] Budget =
        {
            { 50, 75, 100 }, { 100, 150, 200 }, { 150, 225, 400 }, { 250, 375, 500 }, { 500, 750, 1100 },
            { 600, 1000, 1400 }, { 750, 1300, 1700 }, { 1000, 1700, 2100 }, { 1300, 2000, 2600 },
            { 1600, 2300, 3100 }, { 1900, 2900, 4100 }, { 2200, 3700, 4700 }, { 2600, 4200, 5400 },
            { 2900, 4900, 6200 }, { 3300, 5400, 7800 }, { 3800, 6100, 9800 }, { 4500, 7200, 11700 },
            { 5000, 8700, 14200 }, { 5500, 10700, 17200 }, { 6400, 13200, 22000 },
        };

        // SRD 5.2.1, Experience Points by Challenge Rating, by challenge times ten (the data's own number: 1/8 is 1,
        // 1/4 is 2, 1/2 is 5). CR 0 is "0 or 10": 10 here, so the budget errs toward a warning
        static readonly Dictionary<int, int> Fractional = new() { [0] = 10, [1] = 25, [2] = 50, [5] = 100 };

        static readonly int[] ByChallenge =
        {
            10, 200, 450, 700, 1100, 1800, 2300, 2900, 3900, 5000, 5900, 7200, 8400, 10000, 11500, 13000, 15000,
            18000, 20000, 22000, 25000, 33000, 41000, 50000, 62000, 75000, 90000, 105000, 120000, 135000, 155000,
        };

        public static int BudgetFor(int level, EncounterDifficulty difficulty) =>
            Budget[Math.Clamp(level, 1, 20) - 1, (int)difficulty];

        public static int Xp(Monster monster)
        {
            int tenths = (int)Math.Round(monster.Challenge * 10);

            if (Fractional.TryGetValue(tenths, out int xp)) return xp;

            return ByChallenge[Math.Clamp((int)Math.Round(monster.Challenge), 0, ByChallenge.Length - 1)];
        }

        // THE SOLO ENEMY CAP: no more than 2 at levels 1-2, 3 at 3-4, 4 at 5-10, 5 at 11 and up
        public static int Cap(int level) => level <= 2 ? 2 : level <= 4 ? 3 : level <= 10 ? 4 : 5;

        // what the check says about one fight: nothing when it's within both, a caution for each it's over, and a note
        // when it has no level to be checked against
        public static IEnumerable<ContentProblem> Check(EncounterEntry fight, Func<string, Monster> find, string file,
                                                       string where)
        {
            if (fight == null || fight.Kind != EntryKind.Fight) yield break;

            if (fight.Level <= 0)
            {
                yield return ContentProblem.Caution(file, where,
                    $"'{fight.Id}' says no level, so its solo budget can't be checked - give it \"level\" and \"difficulty\"");
                yield break;
            }

            var counted = fight.Monsters.Select(b => (Monster: find(b.Monster), Most: Math.Max(1, b.Count.Maximum)))
                               .Where(m => m.Monster != null)
                               .ToList();

            int xp = counted.Sum(m => Xp(m.Monster) * m.Most);
            int enemies = counted.Sum(m => m.Most);
            int budget = BudgetFor(fight.Level, fight.Difficulty);
            string named = fight.Difficulty.ToString().ToLowerInvariant();

            if (xp > budget)
                yield return ContentProblem.Caution(file, where,
                    $"'{fight.Id}' is {xp} XP, over a {named} fight's {budget} XP for one level {fight.Level} hero " +
                    "(the SRD's XP Budget per Character, solo)");

            if (enemies > Cap(fight.Level))
                yield return ContentProblem.Caution(file, where,
                    $"'{fight.Id}' can field {enemies} enemies, over the solo cap of {Cap(fight.Level)} at level {fight.Level}");
        }
    }
}
