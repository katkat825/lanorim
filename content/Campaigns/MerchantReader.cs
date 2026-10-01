using System.Collections.Generic;
using System.Text.Json;
using Content.Schema;

namespace Content.Campaigns
{
    // A MERCHANTS FILE: { "merchants": [ { "id", "stock", "sell_percent" } ] } - a campaign's merchants/ folder, and the
    // SRD's own starting shop (content/srd/merchants/, cc_task_e-shop-species-and-ui-notes.md 1.4). Read by the
    // shared EntryList; whether each item in a stock exists is the caller's to ask, since a campaign's own items count
    public static class MerchantReader
    {
        // every key a merchant takes
        public static readonly IReadOnlyList<string> Keys = new[] { "id", "stock", "sell_percent" };

        // the file: { "merchants": [ ... ] }, each entry read by ReadOne (EntryList)
        public static readonly EntryList<MerchantDef> Entries =
            new EntryList<MerchantDef>("merchants", "merchant", Keys, ReadOne);

        public static bool TryRead(string text, out IReadOnlyList<MerchantDef> merchants,
                                   out IReadOnlyList<string> problems) =>
            Entries.TryRead(text, out merchants, out problems);

        static MerchantDef ReadOne(JsonElement entry, string id, List<string> problems) =>
            new MerchantDef(id, entry.Strings("stock"), entry.Has("sell_percent") ? entry.Number("sell_percent") : -1);

        // THE STARTING SHOP every campaign has unless it writes its own merchant with this id: the SRD's mundane
        // weapons and armor, potions of healing and the adventuring basics, at SRD prices. No ammunition: it is free
        public const string StartingShopId = "starting_shop";

        static MerchantDef _starting;

        public static MerchantDef StartingShop =>
            _starting ??= Schema.Srd.ReadAll<MerchantDef>("merchants", TryRead, null)
                                    .Find(m => m.Id == StartingShopId);
    }
}
