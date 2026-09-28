using System;

namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- armor ----------------------------------------------------------------------------

        public ArmorProfile Armor { get; set; } = ArmorProfile.Unarmored;

        public bool HasShield { get; set; }

        // Unarmored Defense: the Barbarian's CON, a Monk's WIS. null means "use the armor".
        public Ability? UnarmoredDefense { get; set; }

        public int ArmorClassBonus { get; set; }

        public int ArmorClass
        {
            get
            {
                // a borrowed shape's AC is the statblock's number. the armor stays on the sheet
                // untouched underneath, which is why taking the shape off needs to restore nothing
                if (Shape != null) return Shape.ArmorClass + Boons.ArmorClass;

                int dex = AbilityModifier(Ability.Dexterity);

                int from = UnarmoredDefense.HasValue && Armor.Category == ArmorCategory.None
                    ? 10 + dex + AbilityModifier(UnarmoredDefense.Value)
                    : Armor.ArmorClass(dex);

                // Mage Armor's 13 + Dex. SRD lets a creature with two ways of working out its
                // unarmored armor class pick one; picking the better is the only sensible pick
                if (Armor.Category == ArmorCategory.None && Boons.UnarmoredBase > 0)
                    from = Math.Max(from, Boons.UnarmoredBase + dex);

                return from + (HasShield ? ArmorCategories.ShieldBonus : 0) + ArmorClassBonus +
                       (Armor.Category != ArmorCategory.None ? ArmoredArmorClassBonus : 0) +
                       Boons.ArmorClass;
            }
        }
    }
}
