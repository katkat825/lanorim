using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Core.Tables;
using Core.Words;

namespace Content.Workshop.Tables
{
    // A LOOT TABLE AS THE WORKSHOP EDITS IT (cc_task_n Part 3): the file, the id, whether the roll is heard, the entries.
    // LootReader's keys, as written
    public sealed class LootTableDraft : TableDraft<LootDraft>
    {
        static readonly string[] Keys = { "id", "rolled", "entries" };

        public static LootTableDraft Read(JsonObject table, string file, List<string> told)
        {
            string id = DraftJson.Text(table, "id");

            return new LootTableDraft
            {
                File = file,
                Id = id,
                Rolled = DraftJson.Text(table, "rolled"),
                Entries = DraftJson.Objects(table, "entries").Select(e => LootDraft.Read(e, $"{file} {id}", told)).ToList(),
                Kept = DraftJson.Kept(table, Keys, $"{file} {id}", told),
            };
        }

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["id"] = Id };
            DraftJson.PutText(o, "rolled", Rolled);
            o["entries"] = new JsonArray(Entries.Select(e => (JsonNode)e.ToJson()).ToArray());
            DraftJson.Put(o, Kept);
            return o;
        }

    }

    // ONE ENTRY: a find (items, coins, maybe a line), nothing, or another table rolled
    public sealed class LootDraft : IDraftEntry
    {
        public string Id { get; set; } = "";

        public LootKind Kind { get; set; } = LootKind.Find;

        public int Weight { get; set; } = 1;

        public List<LotDraft> Items { get; set; } = new();

        public CoinDraft Gold { get; set; } = new();

        public CoinDraft Silver { get; set; } = new();

        public CoinDraft Copper { get; set; } = new();

        // the table it rolls (a "table" entry)
        public string Table { get; set; } = "";

        // "line": true - the GM reads a line for it (encounter.<table>.loot.<entry>)
        public bool Speaks { get; set; }

        public string Line { get; set; } = "";

        public Dictionary<string, string> Kept { get; set; } = new();

        static readonly string[] Keys = { "id", "kind", "weight", "items", "gold", "silver", "copper", "table", "line" };

        public IEnumerable<(string Key, CoinDraft Coin)> Coins => new[] { ("gold", Gold), ("silver", Silver), ("copper", Copper) };

        public static LootDraft Read(JsonObject entry, string where, List<string> told)
        {
            string id = DraftJson.Text(entry, "id");
            EnumWords.TryParse(DraftJson.Text(entry, "kind"), out LootKind kind);

            return new LootDraft
            {
                Id = id,
                Kind = kind,
                Weight = DraftJson.Number(entry, "weight", 1),
                Items = DraftJson.Objects(entry, "items").Select(i => LotDraft.Read(i, $"{where}.{id}", told)).ToList(),
                Gold = CoinDraft.Read(entry["gold"]),
                Silver = CoinDraft.Read(entry["silver"]),
                Copper = CoinDraft.Read(entry["copper"]),
                Table = DraftJson.Text(entry, "table"),
                Speaks = DraftJson.Flag(entry, "line"),
                Kept = DraftJson.Kept(entry, Keys, $"{where}.{id}", told),
            };
        }

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["id"] = Id, ["kind"] = EnumWords.Name(Kind) };

            DraftJson.PutNumber(o, "weight", Weight, 1);

            foreach ((string key, CoinDraft coin) in Coins)
                if (!coin.IsEmpty) o[key] = coin.ToJson();

            if (Items.Count > 0) o["items"] = new JsonArray(Items.Select(i => (JsonNode)i.ToJson()).ToArray());

            DraftJson.PutText(o, "table", Table);
            DraftJson.PutFlag(o, "line", Speaks);
            DraftJson.Put(o, Kept);
            return o;
        }

        public override string ToString() => $"{Id} {Kind}";
    }

    public sealed class LotDraft
    {
        public string Item { get; set; } = "";

        public string Count { get; set; } = "";

        public Dictionary<string, string> Kept { get; set; } = new();

        public static LotDraft Read(JsonObject lot, string where, List<string> told) => new()
        {
            Item = DraftJson.Text(lot, "item"),
            Count = DraftJson.Text(lot, "count"),
            Kept = DraftJson.Kept(lot, new[] { "item", "count" }, $"{where} {DraftJson.Text(lot, "item")}", told),
        };

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["item"] = Item };
            DraftJson.PutText(o, "count", Count);
            DraftJson.Put(o, Kept);
            return o;
        }
    }

    // A PURSE OF ONE COIN: "2d6", or { "roll": "3d6", "times": 10 } (campaign_authoring_rules §9). Empty: none of it
    public sealed class CoinDraft
    {
        public string Roll { get; set; } = "";

        public int Times { get; set; } = 1;

        public bool IsEmpty => string.IsNullOrWhiteSpace(Roll);

        public static CoinDraft Read(JsonNode coin) => coin switch
        {
            JsonObject o => new CoinDraft { Roll = DraftJson.Text(o, "roll"), Times = DraftJson.Number(o, "times", 1) },
            JsonValue v when v.TryGetValue(out string s) => new CoinDraft { Roll = s },
            JsonValue v when v.TryGetValue(out int n) => new CoinDraft { Roll = n.ToString() },
            _ => new CoinDraft(),
        };

        public JsonNode ToJson() =>
            Times > 1 ? new JsonObject { ["roll"] = Roll, ["times"] = Times } : JsonValue.Create(Roll);

        public override string ToString() => IsEmpty ? "" : Times > 1 ? $"{Roll} x {Times}" : Roll;
    }
}
