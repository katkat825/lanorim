using System;

namespace Core.Characters
{
    // which rolls a boon's number touches. a Bless is attacks and saves; a Guidance is one check; a
    // Shield is armor class. declared rather than guessed, so the boon is unambiguous. (moved from
    // Core.Magic on 2026-09-27: spells, stances and items all say it, so it is a boon's word)
    [Flags]
    public enum Sways
    {
        None = 0,
        Attacks = 1 << 0,
        Saves = 1 << 1,
        Checks = 1 << 2,
        Damage = 1 << 3,
        ArmorClass = 1 << 4,
    }
}
