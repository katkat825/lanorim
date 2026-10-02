using System;

namespace Content.Inventory
{
    // MONEY IS COPPER (Kathleen, 2026-10-05: "make it the srd selling price"; cc_task_f 1.5). SRD 5.2.1 prices things in
    // gold, silver and copper (Coin Values: 1 GP is 10 SP is 100 CP), and equipment sells for half its cost, so a
    // purse of whole gold sold a 1 GP sickle for 0. every amount the game holds - the purse, an item's cost, a price,
    // a deal - is in copper pieces, and it is shown as gold, silver and copper (PackView.Money). what an author writes
    // stays gold: an item's cost (with fractions, 0.5 is 5 SP: Json.Price), a class's or background's gold, <<gold N>>,
    // a loot table's gold - each turned into copper here, where it is read
    public static class Coins
    {
        public const int PerGold = 100;

        public const int PerSilver = 10;

        public static int FromGold(int gold) => gold * PerGold;

        // the coins an amount is written in, the largest first: 150 is 1 gold, 5 silver, 0 copper
        public static (int Gold, int Silver, int Copper) Split(int copper)
        {
            copper = Math.Max(0, copper);

            return (copper / PerGold, copper % PerGold / PerSilver, copper % PerSilver);
        }
    }
}
