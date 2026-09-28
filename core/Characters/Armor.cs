using System;
using Core.Localization;
using Core.Words;

namespace Core.Characters
{
    public enum ArmorCategory
    {
        // no armor at all: AC 10 + Dex, and what Unarmored Defense replaces
        None = 0,
        Light,
        Medium,
        Heavy,
    }

    // SRD: light armor adds all of Dex, medium caps it at +2, heavy adds none. that cap is the
    // whole reason armor is a shape and not a number.
    public readonly struct ArmorProfile
    {
        public ArmorProfile(ArmorCategory category, int baseArmorClass, int strengthRequirement = 0,
                            bool stealthDisadvantage = false)
        {
            Category = category;
            BaseArmorClass = baseArmorClass;
            StrengthRequirement = strengthRequirement;
            StealthDisadvantage = stealthDisadvantage;
        }

        // light, medium or heavy: SRD's armor categories, which Armor Training names. 'category'
        // in the data, beside a weapon's; it was 'weight', which a loot table uses for odds
        public ArmorCategory Category { get; }

        public int BaseArmorClass { get; }

        public int StrengthRequirement { get; }

        public bool StealthDisadvantage { get; }

        public static readonly ArmorProfile Unarmored = new ArmorProfile(ArmorCategory.None, 10);

        public int DexterityAllowed(int dexterityModifier) => Category switch
        {
            ArmorCategory.Heavy => 0,
            ArmorCategory.Medium => Math.Min(2, dexterityModifier),
            _ => dexterityModifier,
        };

        public int ArmorClass(int dexterityModifier) =>
            BaseArmorClass + DexterityAllowed(dexterityModifier);

        public override string ToString() =>
            $"{EnumWords.Name(Category)} armor, base {BaseArmorClass}" +
            (StrengthRequirement > 0 ? $", needs str {StrengthRequirement}" : "") +
            (StealthDisadvantage ? ", noisy" : "");
    }

    public static class ArmorCategories
    {
        public static string NameKey(this ArmorCategory category) =>
            KeyConventions.Key(KeyConventions.ItemNs, "armor_" + category.Id(), "name");

        // SRD shield: +2, and it does not care what you are wearing
        public const int ShieldBonus = 2;
    }
}
