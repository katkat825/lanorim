using System;
using Content.Sheet;
using Core.Tables;

namespace Content.Challengers
{
    // A WON BOUT'S XP (cc_task_cg Part 4): a quarter of the XP the hero needs to go from their level to the next,
    // times x3/4 for a low challenger, x1 moderate, x1 1/2 high - about four bouts a level at every level, three for
    // hard picks and five or six for easy ones. It REPLACES the monsters' own XP for that bout. The share and the three
    // multipliers are the pack's (ArenaRules). Nothing at level 20, which has no next level
    public static class BoutXp
    {
        public static int For(int level, EncounterDifficulty difficulty, ArenaRules rules)
        {
            rules ??= ArenaRules.Default;

            if (level >= Advancement.HighestLevel) return 0;

            int needs = Advancement.XpFor(level + 1) - Advancement.XpFor(level);

            return Math.Max(1, (int)Math.Round(needs * rules.Share * rules.Times(difficulty), MidpointRounding.AwayFromZero));
        }
    }
}
