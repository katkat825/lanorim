using System;
using System.Collections.Generic;
using System.Linq;
using Content.Items;

namespace Content.Campaigns
{
    // a shop, as the campaign writes it: the id, the stock, and what it pays for what it buys.
    //
    // OR AN ARMORY (cc_task_i 3.3, "armory": true): a loadout, not a shop. its stock is lent, never sold - the hero
    // wears any of it for free, as often as they like, and it goes back when it comes off (Hero.Borrow). no gold
    // moves, and nothing lands in the pack. a consumable can't be lent, so the reader refuses one in its stock
    public sealed class MerchantDef
    {
        public MerchantDef(string id, IReadOnlyList<string> stock, int sellPercent = -1, bool armory = false)
        {
            Id = id ?? "";
            Stock = stock ?? Array.Empty<string>();
            SellPercent = sellPercent;
            Armory = armory;
        }

        public bool Armory { get; }

        // "stock": "all_mundane" on an armory (cc_task_ca Part 4; Kathleen: "every non-magical item that the hero can use
        // at their level"): every non-magical wearable or wieldable item in the library, so the Crucible needn't list
        // them. The racks still show each hero only what Item.UsableBy allows
        public const string AllMundane = "all_mundane";

        public bool StocksAllMundane { get; init; }

        // what an "all_mundane" armory lends: every item it can lend that is worn or held and isn't magical
        public static bool Mundane(Item item) => CanLend(item) && item.IsEquippable && !item.Magical;

        // WHAT AN ARMORY CAN LEND: anything but what's used up (a potion would be spent, not lent back). The campaign check
        // refuses one in an armory's stock, and the Workshop's item picker greys it out
        public static bool CanLend(Item item) => item != null && item.Kind != ItemKind.Consumable;

        public string Id { get; }

        public IReadOnlyList<string> Stock { get; }

        public int SellPercent { get; }

        public string NameKey => Core.Localization.KeyConventions.Key("merchant", Id, "name");

        public Content.Inventory.Merchant Open(ItemShelf shelf) =>
            new Content.Inventory.Merchant(Id, shelf,
                                           StocksAllMundane ? shelf.All.Where(Mundane).Select(i => i.Id) : Stock,
                                           SellPercent);
    }
}
