using System;

namespace Core.Characters
{
    // which way a boon tips a roll rather than how far: advantage and disadvantage, on the bearer's
    // own rolls or on the rolls made at it. Guiding Bolt is advantage against; Reckless Attack is
    // advantage on attacks and advantage against; Remarkable Athlete is advantage on initiative. one
    // set for spells, stances, items and a feature's standing advantages - it used to be eight
    // booleans on Boon, and string keys on Actor (cc_task_dedupe-effects.md Lead 2,
    // cc_task_dedupe-leftovers.md Part A)
    [Flags]
    public enum Leans
    {
        None = 0,
        AdvantageOnAttacks = 1 << 0,
        DisadvantageOnAttacks = 1 << 1,
        AdvantageAgainst = 1 << 2,
        DisadvantageAgainst = 1 << 3,
        AdvantageOnChecks = 1 << 4,
        DisadvantageOnChecks = 1 << 5,
        AdvantageOnSaves = 1 << 6,
        DisadvantageOnSaves = 1 << 7,

        // initiative is its own roll: a Dexterity check, but a check lean doesn't reach it
        AdvantageOnInitiative = 1 << 8,
    }
}
