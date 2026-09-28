using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Core.Characters
{
    // what a Shillelagh does to a weapon while it lasts: which weapons, which ability they swing
    // with, what die they roll, and a damage type the wielder may use instead of the weapon's.
    // the die grows with the caster's level the way a cantrip's does: a boon's spec holds the dice
    // by cantrip tier, and the cast picks the one for the caster (For)
    public sealed class WeaponRewrite
    {
        public WeaponRewrite(IReadOnlyList<string> weapons, Ability? ability = null,
                             DiceRoll die = default, DamageType damageType = DamageType.None,
                             IReadOnlyList<DiceRoll> dieTiers = null)
        {
            Weapons = weapons ?? Array.Empty<string>();
            Ability = ability;
            Die = die;
            DamageType = damageType;
            DieTiers = dieTiers ?? Array.Empty<DiceRoll>();
        }

        // weapon ids it applies to. empty is any weapon
        public IReadOnlyList<string> Weapons { get; }

        public Ability? Ability { get; }

        public DiceRoll Die { get; }

        // None leaves the weapon's own type
        public DamageType DamageType { get; }

        // the die by cantrip tier - none, then 5, 11, 17 - before a caster is known
        public IReadOnlyList<DiceRoll> DieTiers { get; }

        public bool Covers(string weaponId) =>
            Weapons.Count == 0 || Weapons.Contains(weaponId, StringComparer.OrdinalIgnoreCase);

        // the rewrite a caster of this ability and cantrip tier puts on: Shillelagh's d8 at 1st
        // level, 2d6 at 17th
        public WeaponRewrite For(Ability ability, int tier) =>
            new WeaponRewrite(Weapons, ability,
                              DieTiers.Count > 0 ? DieTiers[Math.Min(tier, DieTiers.Count - 1)] : Die,
                              DamageType);
    }
}
