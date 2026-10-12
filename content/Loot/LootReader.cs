using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Dice;
using Core.Tables;

namespace Content.Loot
{
    // loot tables, one or more to a file. like the EncounterReader this reads the SHAPE of a table;
    // whether the items it names exist, and whether a table it rolls is in another file, is the
    // package's question. a loop inside one file is caught here, because every link of it is here.
    public static class LootReader
    {
        public static bool TryRead(string text, out IReadOnlyList<LootTable> tables,
                                   out IReadOnlyList<string> problems)
        {
            var trouble = new List<string>();
            List<LootTable> found = TableReader.Tables(text, "the loot file",
                                                       (table, id) => ReadOne(table, id, trouble), trouble);

            tables = found ?? new List<LootTable>();
            problems = trouble;

            if (found == null) return false;

            string loop = Loop(found);

            if (loop.Length > 0) trouble.Add(loop);

            return trouble.Count == 0;
        }

        // the sentence for a table that ends up rolling itself, or empty. shared with the package,
        // which asks the same of every file's tables together
        public static string Loop(IEnumerable<LootTable> tables)
        {
            IReadOnlyList<string> cycle = new LootTables(tables).Cycle();

            if (cycle.Count == 0) return "";

            return cycle.Count == 2
                ? $"{cycle[0]}: it rolls itself, so it would never stop being opened"
                : $"{cycle[0]}: {string.Join(" rolls ", cycle)} - a loop, so it would never " +
                  "stop being opened";
        }

        static LootTable ReadOne(JsonElement table, string id, List<string> problems)
        {
            Keyed.OnlyKnown(table, new[] { "id", "rolled", "entries" }, id, problems);

            // behind the screen unless the table says otherwise, the same as an encounter
            Visibility visibility = TableReader.Rolled(table, id, problems);

            List<LootEntry> entries = TableReader.Entries(table, id, ReadEntry, "an empty chest", problems);

            return new LootTable(id, entries, visibility);
        }

        static readonly string[] EntryKeys = { "id", "kind", "weight", "items", "gold", "silver", "copper", "table", "line" };

        // the coins an author may write, each with what one is worth in copper (cc_task_k). the purse is copper
        // (Coins); a find says it in whichever coin reads right - "a few coppers", "a silver for your trouble"
        static readonly (string Key, int Worth)[] CoinKeys =
        {
            ("gold", Inventory.Coins.PerGold),
            ("silver", Inventory.Coins.PerSilver),
            ("copper", 1),
        };

        static bool HasCoin(JsonElement entry) => CoinKeys.Any(c => entry.Has(c.Key));

        static LootEntry ReadEntry(JsonElement entry, string table, List<string> problems)
        {
            string where = TableReader.Entry(entry, table, EntryKeys, problems, out LootKind kind);

            if (where == null) return null;

            string id = entry.Text("id");
            int weight = entry.Weight(where, problems);

            List<Lot> items = TableReader.Counted(entry, "items", "item", "an item id", where, problems)
                                         .Select(i => new Lot(i.Id, i.Count)).ToList();

            List<Purse> coins = CoinKeys.Select(c => ReadPurse(entry, c.Key, c.Worth, where, problems))
                                        .Where(p => !p.IsEmpty).ToList();

            string next = entry.Text("table");

            if (next.Length > 0 && !Json.IsId(next))
                problems.Add($"{where}: '{next}' is not a table id");

            switch (kind)
            {
                case LootKind.Find:
                    if (!entry.Has("items") && !HasCoin(entry))
                        problems.Add($"{where}: a find needs 'items', 'gold', 'silver' or 'copper' - what is in it");
                    if (next.Length > 0)
                        problems.Add($"{where}: a find does not roll a table - make it a " +
                                     "'table' entry");
                    break;

                case LootKind.Table:
                    if (next.Length == 0)
                        problems.Add($"{where}: a table entry needs 'table' - which one it rolls");
                    if (entry.Has("items") || HasCoin(entry))
                        problems.Add($"{where}: a table entry rolls a table and gives nothing " +
                                     "of its own - put the items in that table");
                    break;

                default:
                    if (entry.Has("items") || HasCoin(entry) || next.Length > 0)
                        problems.Add($"{where}: 'nothing' has nothing in it - no items, coin " +
                                     "or table");
                    break;
            }

            // the narrator only speaks when asked to, so a quiet find owes the locale nothing
            bool speaks = entry.Flag("line");

            return new LootEntry(id, kind, weight, items, coins, next, speaks);
        }

        // "gold": "2d6" or "gold": { "roll": "3d6", "times": 10 } - the multiplier is its own
        // field because dice notation has no "*10" and a purse is the only thing that needs one.
        // "silver" and "copper" are written the same way, and read here too: key is which coin, worth is one of it
        // in copper
        static Purse ReadPurse(JsonElement entry, string key, int worth, string where, List<string> problems)
        {
            if (!entry.Has(key)) return Purse.Empty;

            JsonElement coin = entry.GetProperty(key);

            Keyed.OnlyKnown(coin, new[] { "roll", "times" }, $"{where} {key}", problems);

            // so dice that did not parse are said once, by the parser, and not again below
            int said = problems.Count;

            DiceRoll dice;
            int times = 1;

            if (coin.ValueKind == JsonValueKind.Object)
            {
                dice = coin.Dice("roll", problems, where);

                if (coin.Has("times"))
                {
                    times = coin.Number("times", 0);

                    if (times < 1)
                    {
                        problems.Add($"{where}: 'times' is a whole number, 1 or more");
                        times = 1;
                    }
                }
            }
            else
            {
                dice = entry.Dice(key, problems, where);
            }

            if (dice.IsNothing)
            {
                if (coin.ValueKind == JsonValueKind.Object && !coin.Has("roll"))
                    problems.Add($"{where}: {key} needs 'roll' - write it like " +
                                 "{ \"roll\": \"3d6\", \"times\": 10 }, or \"2d6\" on its own");
                else if (problems.Count == said)
                    problems.Add($"{where}: no {key} - leave '{key}' out instead");

                return Purse.Empty;
            }

            var purse = new Purse(dice, times, worth);

            if (purse.Dice.Minimum < 0)
                problems.Add($"{where}: {dice} {key} can come to less than none - a purse is " +
                             "never in debt");

            return purse;
        }
    }
}
