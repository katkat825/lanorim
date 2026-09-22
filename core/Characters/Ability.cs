using System;
using System.Collections.Generic;
using Core.Localization;

namespace Core.Characters
{
    // the six SRD 5.2.1 ability scores. the order is the sheet's order and the point-buy order;
    // nothing reads the numeric value except the array index, so it may not be reordered.
    public enum Ability
    {
        Strength = 0,
        Dexterity = 1,
        Constitution = 2,
        Intelligence = 3,
        Wisdom = 4,
        Charisma = 5,
    }

    public static class Abilities
    {
        public static readonly IReadOnlyList<Ability> All = new[]
        {
            Ability.Strength, Ability.Dexterity, Ability.Constitution,
            Ability.Intelligence, Ability.Wisdom, Ability.Charisma,
        };

        public const int Count = 6;

        // SRD: point buy runs 8-15 before species bumps, and nothing takes a score past 20
        public const int PointBuyFloor = 8;
        public const int PointBuyCeiling = 15;
        public const int Ceiling = 20;
        public const int Floor = 1;

        // the id a data file writes, and the key segment - "str", not "Strength"
        public static string Id(this Ability ability) => ability switch
        {
            Ability.Strength => "str",
            Ability.Dexterity => "dex",
            Ability.Constitution => "con",
            Ability.Intelligence => "int",
            Ability.Wisdom => "wis",
            Ability.Charisma => "cha",
            _ => throw new ArgumentOutOfRangeException(nameof(ability), ability, null),
        };

        public static bool TryParse(string id, out Ability ability)
        {
            foreach (Ability a in All)
            {
                if (!string.Equals(a.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                ability = a;
                return true;
            }

            ability = Ability.Strength;
            return false;
        }

        public static string NameKey(this Ability ability) =>
            KeyConventions.Key(KeyConventions.AbilityNs, ability.Id(), "name");

        // the three-letter sheet label; a separate key because some languages abbreviate differently
        public static string ShortKey(this Ability ability) =>
            KeyConventions.Key(KeyConventions.AbilityNs, ability.Id(), "short");

        // SRD: (score - 10) / 2, rounded down - and C# integer division rounds toward zero, so a
        // score of 9 would give 0 instead of -1 without the explicit floor
        public static int Modifier(int score) =>
            (int)Math.Floor((score - 10) / 2.0);

        // SRD 27-point buy: 8 is free, 9-13 cost 1 each, 14 and 15 cost 2 each
        public const int PointBuyBudget = 27;

        public static int PointBuyCost(int score)
        {
            if (score < PointBuyFloor || score > PointBuyCeiling) return -1;

            int cost = score - PointBuyFloor;

            if (score >= 14) cost += score - 13;

            return cost;
        }
    }
}
