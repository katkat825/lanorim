using System;
using System.Collections.Generic;
using Core.Localization;
using Core.Words;

namespace Core.Characters
{
    // the six SRD 5.2.1 ability scores. the order is the sheet's order and the point-buy order;
    // nothing reads the numeric value except the array index, so it may not be reordered. the
    // word a data file writes, and the key segment, is "str", not "strength" (EnumWords.Id)
    public enum Ability
    {
        [Word("str")] Strength = 0,
        [Word("dex")] Dexterity = 1,
        [Word("con")] Constitution = 2,
        [Word("int")] Intelligence = 3,
        [Word("wis")] Wisdom = 4,
        [Word("cha")] Charisma = 5,
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
