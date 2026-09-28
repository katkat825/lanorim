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

        static readonly string[] EntryKeys = { "id", "kind", "weight", "items", "gold", "table", "line" };

        static LootEntry ReadEntry(JsonElement entry, string table, List<string> problems)
        {
            string where = TableReader.Entry(entry, table, EntryKeys, problems, out LootKind kind);

            if (where == null) return null;

            string id = entry.Text("id");
            int weight = entry.Weight(where, problems);

            List<Lot> items = TableReader.Counted(entry, "items", "item", "an item id", where, problems)
                                         .Select(i => new Lot(i.Id, i.Count)).ToList();

            Purse gold = ReadGold(entry, where, problems);

            string next = entry.Text("table");

            if (next.Length > 0 && !Json.IsId(next))
                problems.Add($"{where}: '{next}' is not a table id");

            switch (kind)
            {
                case LootKind.Find:
                    if (!entry.Has("items") && !entry.Has("gold"))
                        problems.Add($"{where}: a find needs 'items' or 'gold' - what is in it");
                    if (next.Length > 0)
                        problems.Add($"{where}: a find does not roll a table - make it a " +
                                     "'table' entry");
                    break;

                case LootKind.Table:
                    if (next.Length == 0)
                        problems.Add($"{where}: a table entry needs 'table' - which one it rolls");
                    if (entry.Has("items") || entry.Has("gold"))
                        problems.Add($"{where}: a table entry rolls a table and gives nothing " +
                                     "of its own - put the items in that table");
                    break;

                default:
                    if (entry.Has("items") || entry.Has("gold") || next.Length > 0)
                        problems.Add($"{where}: 'nothing' has nothing in it - no items, gold " +
                                     "or table");
                    break;
            }

            // the narrator only speaks when asked to, so a quiet find owes the locale nothing
            bool speaks = entry.Flag("line");

            return new LootEntry(id, kind, weight, items, gold, next, speaks);
        }

        // "gold": "2d6" or "gold": { "roll": "3d6", "times": 10 } - the multiplier is its own
        // field because dice notation has no "*10" and a purse is the only thing that needs one
        static Purse ReadGold(JsonElement entry, string where, List<string> problems)
        {
            if (!entry.Has("gold")) return Purse.Empty;

            JsonElement gold = entry.GetProperty("gold");

            Keyed.OnlyKnown(gold, new[] { "roll", "times" }, $"{where} gold", problems);

            // so dice that did not parse are said once, by the parser, and not again below
            int said = problems.Count;

            DiceRoll dice;
            int times = 1;

            if (gold.ValueKind == JsonValueKind.Object)
            {
                dice = gold.Dice("roll", problems, where);

                if (gold.Has("times"))
                {
                    times = gold.Number("times", 0);

                    if (times < 1)
                    {
                        problems.Add($"{where}: 'times' is a whole number, 1 or more");
                        times = 1;
                    }
                }
            }
            else
            {
                dice = entry.Dice("gold", problems, where);
            }

            if (dice.IsNothing)
            {
                if (gold.ValueKind == JsonValueKind.Object && !gold.Has("roll"))
                    problems.Add($"{where}: gold needs 'roll' - write it like " +
                                 "{ \"roll\": \"3d6\", \"times\": 10 }, or \"2d6\" on its own");
                else if (problems.Count == said)
                    problems.Add($"{where}: no gold - leave 'gold' out instead");

                return Purse.Empty;
            }

            var purse = new Purse(dice, times);

            if (purse.Dice.Minimum < 0)
                problems.Add($"{where}: {dice} gold can come to less than none - a purse is " +
                             "never in debt");

            return purse;
        }
    }
}
