using System;
using System.Collections.Generic;
using Content.Items;

namespace Content.Campaigns
{
    // a shop, as the campaign writes it: the id, the stock, and what it pays for what it buys
    public sealed class MerchantDef
    {
        public MerchantDef(string id, IReadOnlyList<string> stock, int sellPercent = -1)
        {
            Id = id ?? "";
            Stock = stock ?? Array.Empty<string>();
            SellPercent = sellPercent;
        }

        public string Id { get; }

        public IReadOnlyList<string> Stock { get; }

        public int SellPercent { get; }

        public string NameKey => Core.Localization.KeyConventions.Key("merchant", Id, "name");

        public Content.Inventory.Merchant Open(ItemShelf shelf) =>
            new Content.Inventory.Merchant(Id, shelf, Stock, SellPercent);
    }
}
