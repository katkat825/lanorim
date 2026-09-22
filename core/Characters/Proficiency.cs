using System;

namespace Core.Characters
{
    // SRD scaling, +2 at level 1 up to +6 at 17. derived from the level, never listed in a table:
    // a listed table is a second description of the same thing and free to drift.
    public static class Proficiency
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 20;

        public static int Clamp(int level) => Math.Clamp(level, MinLevel, MaxLevel);

        public static int Bonus(int level) => 2 + (Clamp(level) - 1) / 4;

        // how much of the bonus a given training applies
        public static int Applied(int level, Training training) => training switch
        {
            Training.Untrained => 0,
            Training.Proficient => Bonus(level),
            Training.Expert => Bonus(level) * 2,
            _ => 0,
        };
    }

    // Expertise is the Rogue's, and doubling the bonus is exactly what SRD says it does - so it
    // belongs on the training level, not in a Rogue-shaped special case in the check code.
    public enum Training
    {
        Untrained = 0,
        Proficient = 1,
        Expert = 2,
    }
}
