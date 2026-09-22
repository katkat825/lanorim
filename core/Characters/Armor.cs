using System;
using Core.Localization;

namespace Core.Characters
{
    public enum ArmorWeight
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
        public ArmorProfile(ArmorWeight weight, int baseArmorClass, int strengthRequirement = 0,
                            bool stealthDisadvantage = false)
        {
            Weight = weight;
            BaseArmorClass = baseArmorClass;
            StrengthRequirement = strengthRequirement;
            StealthDisadvantage = stealthDisadvantage;
        }

        public ArmorWeight Weight { get; }

        public int BaseArmorClass { get; }

        public int StrengthRequirement { get; }

        public bool StealthDisadvantage { get; }

        public static readonly ArmorProfile Unarmored = new ArmorProfile(ArmorWeight.None, 10);

        public int DexterityAllowed(int dexterityModifier) => Weight switch
        {
            ArmorWeight.Heavy => 0,
            ArmorWeight.Medium => Math.Min(2, dexterityModifier),
            _ => dexterityModifier,
        };

        public int ArmorClass(int dexterityModifier) =>
            BaseArmorClass + DexterityAllowed(dexterityModifier);

        public override string ToString() =>
            $"{Weight.ToString().ToLowerInvariant()} armor, base {BaseArmorClass}" +
            (StrengthRequirement > 0 ? $", needs str {StrengthRequirement}" : "") +
            (StealthDisadvantage ? ", noisy" : "");
    }

    public static class ArmorWeights
    {
        public static string Id(this ArmorWeight weight) => weight.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out ArmorWeight weight)
        {
            foreach (ArmorWeight w in new[]
                     { ArmorWeight.None, ArmorWeight.Light, ArmorWeight.Medium, ArmorWeight.Heavy })
            {
                if (!string.Equals(w.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                weight = w;
                return true;
            }

            weight = ArmorWeight.None;
            return false;
        }

        public static string NameKey(this ArmorWeight weight) =>
            KeyConventions.Key(KeyConventions.ItemNs, "armor_" + weight.Id(), "name");

        // SRD shield: +2, and it does not care what you are wearing
        public const int ShieldBonus = 2;
    }
}
