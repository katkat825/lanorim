using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Content.Monsters
{
    // a statblock's Spellcasting line: the ability, the DC and attack bonus it prints, and which
    // SRD spells at will and which so many a day
    public sealed class MonsterSpellcasting
    {
        public MonsterSpellcasting(Ability ability, int dc, int attackBonus,
                                   IReadOnlyList<string> atWill,
                                   IReadOnlyDictionary<string, int> uses)
        {
            Ability = ability;
            Dc = dc;
            AttackBonus = attackBonus;
            AtWill = atWill ?? Array.Empty<string>();
            Uses = uses ?? new Dictionary<string, int>();
        }

        public Ability Ability { get; }

        public int Dc { get; }

        public int AttackBonus { get; }

        public IReadOnlyList<string> AtWill { get; }

        // "3/day each": spell id to uses, back on a long rest
        public IReadOnlyDictionary<string, int> Uses { get; }

        public IEnumerable<string> SpellIds => AtWill.Concat(Uses.Keys);
    }
}
