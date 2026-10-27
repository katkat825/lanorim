using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Content.Campaigns;
using Content.Schema;

namespace Content.Workshop.Tables
{
    // A SHOP OR AN ARMORY AS THE WORKSHOP EDITS IT (cc_task_n Part 4): MerchantReader's keys, its name the locale's
    // (merchant.<id>.name)
    public sealed class ShopDraft
    {
        public string File { get; set; } = "";

        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public List<string> Stock { get; set; } = new();

        // what it pays for what it buys back, as a percentage; -1 is unsaid (the game's)
        public int SellPercent { get; set; } = -1;

        public bool Armory { get; set; }

        public Dictionary<string, string> Kept { get; set; } = new();

        public static ShopDraft Read(JsonObject shop, string file, List<string> told) => new()
        {
            File = file,
            Id = DraftJson.Text(shop, "id"),
            Stock = DraftJson.Strings(shop, "stock"),
            SellPercent = shop.ContainsKey("sell_percent") ? DraftJson.Number(shop, "sell_percent", -1) : -1,
            Armory = DraftJson.Flag(shop, "armory"),
            Kept = DraftJson.Kept(shop, MerchantReader.Keys, $"{file} {DraftJson.Text(shop, "id")}", told),
        };

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["id"] = Id };
            DraftJson.PutFlag(o, "armory", Armory);
            o["stock"] = new JsonArray(Stock.Select(s => (JsonNode)JsonValue.Create(s)).ToArray());
            if (!Armory) DraftJson.PutNumber(o, "sell_percent", SellPercent, -1);
            DraftJson.Put(o, Kept);
            return o;
        }

        public string NameKey => new MerchantDef(Id, null).NameKey;

        public override string ToString() => $"{Id}{(Armory ? " (armory)" : "")}: {Stock.Count} items";
    }

    // A LUCKY OR UNLUCKY MOMENT (cc_task_n Part 5): ConsequenceReader's keys, its name and the line the GM reads the
    // locale's (consequence.<id>.name, .line)
    public sealed class ConsequenceDraft
    {
        public string File { get; set; } = "";

        public string Id { get; set; } = "";

        // "bane" (the reader's default) or "boon"
        public string Polarity { get; set; } = "";

        // health, gold, ability_shift, condition, flavour
        public string Kind { get; set; } = "flavour";

        public string Ability { get; set; } = "";

        public string Condition { get; set; } = "";

        public string Amount { get; set; } = "";

        public bool ScalesWithLevel { get; set; }

        public int Weight { get; set; } = 1;

        public string Name { get; set; } = "";

        public string Line { get; set; } = "";

        public Dictionary<string, string> Kept { get; set; } = new();

        public bool Lucky => Polarity == "boon";

        public static ConsequenceDraft Read(JsonObject one, string file, List<string> told) => new()
        {
            File = file,
            Id = DraftJson.Text(one, "id"),
            Polarity = DraftJson.Text(one, "polarity"),
            Kind = DraftJson.Text(one, "kind"),
            Ability = DraftJson.Text(one, "ability"),
            Condition = DraftJson.Text(one, "condition"),
            Amount = DraftJson.Text(one, "amount"),
            ScalesWithLevel = DraftJson.Flag(one, "scales_with_level"),
            Weight = DraftJson.Number(one, "weight", 1),
            Kept = DraftJson.Kept(one, ConsequenceReader.Keys, $"{file} {DraftJson.Text(one, "id")}", told),
        };

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["id"] = Id };
            DraftJson.PutText(o, "polarity", Polarity);
            o["kind"] = Kind;
            DraftJson.PutText(o, "ability", Ability);
            DraftJson.PutText(o, "condition", Condition);
            DraftJson.PutText(o, "amount", Amount);
            DraftJson.PutFlag(o, "scales_with_level", ScalesWithLevel);
            DraftJson.PutNumber(o, "weight", Weight, 1);
            DraftJson.Put(o, Kept);
            return o;
        }

        public override string ToString() => $"{Id} {Polarity} {Kind}";
    }
}
