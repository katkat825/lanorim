using System;
using System.Collections.Generic;
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

        public string Id { get; }

        public IReadOnlyList<string> Stock { get; }

        public int SellPercent { get; }

        public string NameKey => Core.Localization.KeyConventions.Key("merchant", Id, "name");

        public Content.Inventory.Merchant Open(ItemShelf shelf) =>
            new Content.Inventory.Merchant(Id, shelf, Stock, SellPercent);
    }
}
