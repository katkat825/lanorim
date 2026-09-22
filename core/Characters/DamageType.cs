using System;
using System.Collections.Generic;
using Core.Localization;

namespace Core.Characters
{
    // the 13 SRD damage types. they are tagged everywhere; what a creature does about them is one
    // multiplier, not a matrix - decisions_checklist.md section 6.
    public enum DamageType
    {
        None = 0,

        Bludgeoning,
        Piercing,
        Slashing,

        Acid,
        Cold,
        Fire,
        Force,
        Lightning,
        Necrotic,
        Poison,
        Psychic,
        Radiant,
        Thunder,
    }

    // x2, x1, x0.5, x0 - the whole of v1's resistance rules
    public enum Defense
    {
        Normal = 0,
        Vulnerable,
        Resistant,
        Immune,
    }

    public static class DamageTypes
    {
        public static readonly IReadOnlyList<DamageType> All = new[]
        {
            DamageType.Bludgeoning, DamageType.Piercing, DamageType.Slashing,
            DamageType.Acid, DamageType.Cold, DamageType.Fire, DamageType.Force,
            DamageType.Lightning, DamageType.Necrotic, DamageType.Poison,
            DamageType.Psychic, DamageType.Radiant, DamageType.Thunder,
        };

        public static readonly IReadOnlyList<DamageType> Physical = new[]
        {
            DamageType.Bludgeoning, DamageType.Piercing, DamageType.Slashing,
        };

        public static bool IsPhysical(this DamageType type) =>
            type == DamageType.Bludgeoning || type == DamageType.Piercing ||
            type == DamageType.Slashing;

        public static string Id(this DamageType type) =>
            type == DamageType.None ? "none" : type.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out DamageType type)
        {
            foreach (DamageType t in All)
            {
                if (!string.Equals(t.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                type = t;
                return true;
            }

            type = DamageType.None;
            return false;
        }

        public static string NameKey(this DamageType type) =>
            KeyConventions.Key(KeyConventions.DamageNs, type.Id(), "name");

        public static string Id(this Defense defense) => defense.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out Defense defense)
        {
            foreach (Defense d in new[]
                     { Defense.Normal, Defense.Vulnerable, Defense.Resistant, Defense.Immune })
            {
                if (!string.Equals(d.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                defense = d;
                return true;
            }

            defense = Defense.Normal;
            return false;
        }

        // SRD halves after every other modifier and rounds down; immunity wins over everything, so
        // a creature both vulnerable and immune takes nothing
        public static int Apply(this Defense defense, int damage)
        {
            if (damage <= 0) return 0;

            return defense switch
            {
                Defense.Immune => 0,
                Defense.Resistant => damage / 2,
                Defense.Vulnerable => damage * 2,
                _ => damage,
            };
        }
    }
}
