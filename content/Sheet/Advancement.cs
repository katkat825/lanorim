using System;
using System.Collections.Generic;

namespace Content.Sheet
{
    // SRD 5.2.1, Character Advancement (Character Creation): the XP a character needs to reach each level. "When your
    // XP total equals or exceeds a number in the Experience Points column, you reach the corresponding level."
    // Read only in a campaign that levels by XP (Leveling.Xp, cc_task_i 3.4)
    public static class Advancement
    {
        static readonly int[] ToReach =
        {
            0, 300, 900, 2_700, 6_500, 14_000, 23_000, 34_000, 48_000, 64_000,
            85_000, 100_000, 120_000, 140_000, 165_000, 195_000, 225_000, 265_000, 305_000, 355_000,
        };

        public static IReadOnlyList<int> Table => ToReach;

        public const int HighestLevel = 20;

        // the XP to reach a level
        public static int XpFor(int level) => ToReach[Math.Clamp(level, 1, HighestLevel) - 1];

        // the level an XP total reaches
        public static int LevelFor(int xp)
        {
            int level = 1;

            while (level < HighestLevel && xp >= ToReach[level]) level++;

            return level;
        }
    }
}
