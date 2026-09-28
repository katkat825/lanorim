using System;
using System.Collections.Generic;
using System.Text.Json;
using Core.Dice;
using Core.Tables;
using Core.Words;

namespace Content.Schema
{
    // WHAT EVERY GM TABLE FILE SHARES: a list of tables with no id twice, whether the roll is shown,
    // a list of weighted entries with no id twice, each entry's id and kind, and the counted things
    // an entry names. the encounter reader and the loot reader each had a copy
    // (cc_task_dedupe-leftovers.md #13, cc_task_dedupe-methods.md #5); what an entry is stays each
    // reader's own
    public static class TableReader
    {
        // { "tables": [ { "id": ... }, ... ] }: each table with a good id is read by the caller. two
        // tables of one id, or a file with none, is refused. null when the text isn't JSON; `file` is
        // what a problem calls it, "the loot file"
        public static List<T> Tables<T>(string text, string file, Func<JsonElement, string, T> readOne,
                                        List<string> problems)
            where T : class
        {
            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                problems.Add(bad);
                return null;
            }

            var found = new List<T>();

            using (document)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);

                Keyed.OnlyKnown(document.RootElement, new[] { "tables" }, file, problems);

                foreach (JsonElement table in document.RootElement.Items("tables"))
                {
                    string id = table.Text("id");

                    if (!Json.IsId(id))
                    {
                        problems.Add($"'{id}' is not a table id - lowercase a-z, 0-9 and underscore only");
                        continue;
                    }

                    T read = readOne(table, id);

                    if (!seen.Add(id))
                    {
                        problems.Add($"there are two tables called '{id}'");
                        continue;
                    }

                    found.Add(read);
                }

                if (found.Count == 0 && problems.Count == 0)
                    problems.Add("no tables in it - the file is an object with a 'tables' array");
            }

            return found;
        }

        // one entry's id, its keys and its kind: "where" it is ("road.goblins") and its kind, or null
        // when it can't be read further
        public static string Entry<TKind>(JsonElement entry, string table, string[] keys, List<string> problems,
                                          out TKind kind)
            where TKind : struct, Enum
        {
            kind = default;

            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"{table}: '{id}' is not an entry id - lowercase a-z, 0-9 and " +
                             "underscore only");
                return null;
            }

            string where = $"{table}.{id}";

            Keyed.OnlyKnown(entry, keys, where, problems);

            if (!EnumWords.TryParse(entry.Text("kind"), out kind))
            {
                problems.Add($"{where}: '{entry.Text("kind")}' is not a kind of entry " +
                             $"({string.Join(", ", EnumWords.Ids<TKind>())})");
                return null;
            }

            return where;
        }

        // "monsters": [ { "monster": "goblin", "count": "1d4" } ] - things, and how many of each.
        // `idOf` is what a bad id is said not to be, "a monster id"
        public static List<(string Id, DiceRoll Count)> Counted(JsonElement entry, string list, string key,
                                                                string idOf, string where, List<string> problems)
        {
            var counted = new List<(string, DiceRoll)>();

            foreach (JsonElement one in entry.Items(list))
            {
                Keyed.OnlyKnown(one, new[] { key, "count" }, where, problems);

                string id = one.Text(key);

                if (!Json.IsId(id))
                {
                    problems.Add($"{where}: '{id}' is not {idOf}");
                    continue;
                }

                DiceRoll count = one.Has("count") ? one.Dice("count", problems, where)
                                                  : DiceRoll.Flat(1);

                if (one.Has("count") && count.IsNothing) continue;

                // "1d4-1 goblins" can be no goblins, which is a fight with nobody in it, and "1d4-1
                // torches" an item card with nothing on it
                if (count.Minimum < 1)
                {
                    problems.Add($"{where}: {count} {id} can come to none - " +
                                 "a count is always at least one");
                    continue;
                }

                counted.Add((id, count));
            }

            return counted;
        }

        // "rolled": "hidden" (behind the screen, the default) or "shown"
        public static Visibility Rolled(JsonElement table, string id, List<string> problems)
        {
            string rolled = table.Text("rolled", "hidden");

            if (!EnumWords.TryParse(rolled, out Visibility visibility))
                problems.Add($"{id}: 'rolled' is '{rolled}', and it is 'hidden' or 'shown'");

            return visibility;
        }

        // the table's "entries", each read by the caller's reader (the entry, the table's id, the
        // problems); an id twice is refused, and a table with none is refused with what 'nothing'
        // would mean on this kind of table
        public static List<T> Entries<T>(JsonElement table, string id, Func<JsonElement, string, List<string>, T> read,
                                         string emptyMeans, List<string> problems)
            where T : class, ITableEntry
        {
            var entries = new List<T>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (JsonElement each in table.Items("entries"))
            {
                T entry = read(each, id, problems);

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
                             $"is an entry if {emptyMeans} is what you want");

            return entries;
        }
    }
}
