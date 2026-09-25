using System;
using System.Collections.Generic;
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
            var found = new List<LootTable>();
            var trouble = new List<string>();

            tables = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (JsonElement entry in document.RootElement.Items("tables"))
                {
                    LootTable table = ReadOne(entry, trouble);

                    if (table == null) continue;

                    if (!seen.Add(table.Id))
                    {
                        trouble.Add($"there are two tables called '{table.Id}'");
                        continue;
                    }

                    found.Add(table);
                }

                if (found.Count == 0 && trouble.Count == 0)
                    trouble.Add("no tables in it - the file is an object with a 'tables' array");
            }

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

        static LootTable ReadOne(JsonElement table, List<string> problems)
        {
            string id = table.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a table id - lowercase a-z, 0-9 and underscore only");
                return null;
            }

            // behind the screen unless the table says otherwise, the same as an encounter
            string rolled = table.Text("rolled", "hidden");

            Visibility visibility = rolled == "shown" ? Visibility.Shown : Visibility.Hidden;

            if (rolled != "hidden" && rolled != "shown")
                problems.Add($"{id}: 'rolled' is '{rolled}', and it is 'hidden' or 'shown'");

            var entries = new List<LootEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (JsonElement each in table.Items("entries"))
            {
                LootEntry entry = ReadEntry(each, id, problems);

                if (entry == null) continue;

                if (!seen.Add(entry.Id))
                {
                    problems.Add($"{id}: the entry '{entry.Id}' is in the table twice - " +
                                 "give it more weight instead");
                    continue;
                }

                entries.Add(entry);
            }

            if (entries.Count == 0)
                problems.Add($"{id}: an empty table - it needs at least one entry, and 'nothing' " +
                             "is an entry if an empty chest is what you want");

            return new LootTable(id, entries, visibility);
        }

        static LootEntry ReadEntry(JsonElement entry, string table, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"{table}: '{id}' is not an entry id - lowercase a-z, 0-9 and " +
                             "underscore only");
                return null;
            }

            string where = $"{table}.{id}";

            if (!TryKind(entry.Text("kind"), out LootKind kind))
            {
                problems.Add($"{where}: '{entry.Text("kind")}' is not a kind of entry " +
                             "(nothing, find, table)");
                return null;
            }

            int weight = entry.Number("weight", 1);

            // read with a fallback of 0 so a weight that is not a number is caught with one below 1
            if (entry.Has("weight") && entry.Number("weight", 0) < 1)
            {
                problems.Add($"{where}: a weight is a whole number, 1 or more");
                weight = 1;
            }

            var items = new List<Lot>();

            foreach (JsonElement one in entry.Items("items"))
            {
                string item = one.Text("item");

                if (!Json.IsId(item))
                {
                    problems.Add($"{where}: '{item}' is not an item id");
                    continue;
                }

                DiceRoll count = one.Has("count") ? one.Dice("count", problems, where)
                                                  : DiceRoll.Flat(1);

                if (one.Has("count") && count.IsNothing) continue;

                // "1d4-1 torches" can be no torches, which is an item card with nothing on it
                if (count.Minimum < 1)
                {
                    problems.Add($"{where}: {count} {item} can come to none - " +
                                 "a count is always at least one");
                    continue;
                }

                items.Add(new Lot(item, count));
            }

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

        static bool TryKind(string id, out LootKind kind)
        {
            switch ((id ?? "").ToLowerInvariant())
            {
                case "nothing": kind = LootKind.Nothing; return true;
                case "find": kind = LootKind.Find; return true;
                case "table": kind = LootKind.Table; return true;

                default: kind = LootKind.Nothing; return false;
            }
        }
    }
}
