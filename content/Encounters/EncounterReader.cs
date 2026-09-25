using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Dice;
using Core.Tables;

namespace Content.Encounters
{
    // random-encounter tables, one or more to a file. this reads the SHAPE of a table; whether the
    // monsters and maps it names exist is the package's question, because only the package knows
    // what else the campaign ships.
    public static class EncounterReader
    {
        public static bool TryRead(string text, out IReadOnlyList<EncounterTable> tables,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<EncounterTable>();
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
                    EncounterTable table = ReadOne(entry, trouble);

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

            return trouble.Count == 0;
        }

        static EncounterTable ReadOne(JsonElement table, List<string> problems)
        {
            string id = table.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a table id - lowercase a-z, 0-9 and underscore only");
                return null;
            }

            Trigger trigger = ReadTrigger(table, id, problems);

            // the GM rolls behind the screen unless the table says otherwise - a tutorial might
            // want the player to watch the dice decide
            string rolled = table.Text("rolled", "hidden");

            Visibility visibility = rolled == "shown" ? Visibility.Shown : Visibility.Hidden;

            if (rolled != "hidden" && rolled != "shown")
                problems.Add($"{id}: 'rolled' is '{rolled}', and it is 'hidden' or 'shown'");

            var entries = new List<EncounterEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (JsonElement each in table.Items("entries"))
            {
                EncounterEntry entry = ReadEntry(each, id, problems);

                if (entry == null) continue;

                if (!seen.Add(entry.Id))
                {
                    problems.Add($"{id}: the entry '{entry.Id}' is in the table twice - " +
                                 "give it more weight instead");
                    continue;
                }

                entries.Add(entry);
            }

            // a table with nothing on it is a trigger that fires into silence, which no author means
            if (entries.Count == 0)
                problems.Add($"{id}: an empty table - it needs at least one entry, and 'nothing' " +
                             "is an entry if a quiet road is what you want");

            return new EncounterTable(id, trigger, entries, visibility);
        }

        static Trigger ReadTrigger(JsonElement table, string id, List<string> problems)
        {
            if (!table.Has("trigger")) return Trigger.Always;

            JsonElement trigger = table.GetProperty("trigger");

            DiceRoll dice = trigger.Dice("roll", problems, id);

            if (!dice.RollsAnything)
            {
                problems.Add($"{id}: a trigger rolls dice - write it like " +
                             "{ \"roll\": \"1d20\", \"at_least\": 15 }, or leave it out for a " +
                             "table that always fires");
                return Trigger.Always;
            }

            if (!trigger.Has("at_least"))
            {
                problems.Add($"{id}: a trigger needs 'at_least' - the roll that makes it happen");
                return Trigger.Always;
            }

            var read = new Trigger(dice, trigger.Number("at_least"));

            if (!read.CanFire)
                problems.Add($"{id}: {dice} never comes to {read.AtLeast}, so this table can " +
                             "never fire");
            else if (read.MustFire)
                problems.Add($"{id}: {dice} always comes to {read.AtLeast} or more - leave the " +
                             "trigger out if the table should always fire");

            return read;
        }

        static EncounterEntry ReadEntry(JsonElement entry, string table, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"{table}: '{id}' is not an entry id - lowercase a-z, 0-9 and " +
                             "underscore only");
                return null;
            }

            string where = $"{table}.{id}";

            if (!TryKind(entry.Text("kind"), out EntryKind kind))
            {
                problems.Add($"{where}: '{entry.Text("kind")}' is not a kind of entry " +
                             "(nothing, line, fight)");
                return null;
            }

            int weight = entry.Number("weight", 1);

            // read with a fallback of 0 so a weight that is not a number is caught with one below 1
            if (entry.Has("weight") && entry.Number("weight", 0) < 1)
            {
                problems.Add($"{where}: a weight is a whole number, 1 or more");
                weight = 1;
            }

            var monsters = new List<Band>();

            foreach (JsonElement one in entry.Items("monsters"))
            {
                string monster = one.Text("monster");

                if (!Json.IsId(monster))
                {
                    problems.Add($"{where}: '{monster}' is not a monster id");
                    continue;
                }

                DiceRoll count = one.Has("count") ? one.Dice("count", problems, where)
                                                  : DiceRoll.Flat(1);

                if (one.Has("count") && count.IsNothing) continue;

                // "1d4-1 goblins" can be no goblins, which is a fight with nobody in it
                if (count.Minimum < 1)
                {
                    problems.Add($"{where}: {count} {monster} can come to none - " +
                                 "a count is always at least one");
                    continue;
                }

                monsters.Add(new Band(monster, count));
            }

            string map = entry.Text("map");

            if (map.Length > 0 && !Json.IsId(map))
                problems.Add($"{where}: '{map}' is not a map id");

            string loot = entry.Text("loot");

            if (loot.Length > 0 && !Json.IsId(loot))
                problems.Add($"{where}: '{loot}' is not a loot table id");

            switch (kind)
            {
                case EntryKind.Fight:
                    if (monsters.Count == 0 && entry.Items("monsters").Count == 0)
                        problems.Add($"{where}: a fight needs 'monsters' - who turns up");
                    break;

                default:
                    if (entry.Has("monsters") || map.Length > 0 || loot.Length > 0)
                        problems.Add($"{where}: only a fight has monsters, a map or loot - this " +
                                     $"entry is a '{entry.Text("kind")}'");
                    break;
            }

            return new EncounterEntry(id, kind, weight, monsters, map, loot);
        }

        static bool TryKind(string id, out EntryKind kind)
        {
            switch ((id ?? "").ToLowerInvariant())
            {
                case "nothing": kind = EntryKind.Nothing; return true;
                case "line": kind = EntryKind.Line; return true;
                case "fight": kind = EntryKind.Fight; return true;

                default: kind = EntryKind.Nothing; return false;
            }
        }
    }
}
