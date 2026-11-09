using System;
using System.Linq;
using Content.Challengers;
using Content.Sheet;
using Core.Dice;
using Core.Tables;

namespace Sim
{
    // HOW MANY WON BOUTS A LEVEL TAKES (cc_task_cg Part 4), under the arena's rule (BoutXp) and the SRD's thresholds
    // (Advancement), XP carried over a level the way the hero's is: always low, always moderate, always high, and a
    // challenger of a random difficulty each bout, averaged over many careers. Wins only: a lost bout gives nothing
    //   sim bouts [careers] [to-level]
    public static class Bouts
    {
        public static int Run(string[] args)
        {
            int careers = args.Length > 0 && int.TryParse(args[0], out int c) ? c : 2000;
            int top = args.Length > 1 && int.TryParse(args[1], out int t) ? Math.Clamp(t, 2, 20) : 11;
            ArenaRules rules = ArenaRules.Default;

            Console.WriteLine($"won bouts to go up each level, arena defaults ({rules}); mixed is a random difficulty " +
                              $"each bout, {careers} careers");
            Console.WriteLine();
            Console.WriteLine("| Level | XP to next | Bout XP (low / moderate / high) | Low | Moderate | High | Mixed |");
            Console.WriteLine("|---|---|---|---|---|---|---|");

            int[] low = Career(_ => EncounterDifficulty.Low, top, rules);
            int[] moderate = Career(_ => EncounterDifficulty.Moderate, top, rules);
            int[] high = Career(_ => EncounterDifficulty.High, top, rules);

            var rng = new SeededRng(1);
            var mixed = new double[top + 1];

            for (int i = 0; i < careers; i++)
            {
                int[] one = Career(_ => (EncounterDifficulty)(rng.Roll(3) - 1), top, rules);
                for (int level = 1; level < top; level++) mixed[level] += one[level];
            }

            for (int level = 1; level < top; level++)
            {
                int needs = Advancement.XpFor(level + 1) - Advancement.XpFor(level);
                string xp = string.Join(" / ", Enum.GetValues<EncounterDifficulty>().Select(d => BoutXp.For(level, d, rules)));

                Console.WriteLine($"| {level} to {level + 1} | {needs:N0} | {xp} | {low[level]} | {moderate[level]} | " +
                                  $"{high[level]} | {mixed[level] / careers:0.0} |");
            }

            return 0;
        }

        // bouts won at each level, from level 1 and 0 XP, until the top level
        static int[] Career(Func<int, EncounterDifficulty> pick, int top, ArenaRules rules)
        {
            var bouts = new int[top + 1];
            int xp = 0;
            int level = 1;

            while (level < top)
            {
                xp += BoutXp.For(level, pick(level), rules);
                bouts[level]++;
                level = Advancement.LevelFor(xp);
            }

            return bouts;
        }
    }
}
