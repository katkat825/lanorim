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
            var trouble = new List<string>();
            List<EncounterTable> found = TableReader.Tables(text, "the encounters file",
                                                            (table, id) => ReadOne(table, id, trouble), trouble);

            tables = found ?? new List<EncounterTable>();
            problems = trouble;

            return trouble.Count == 0;
        }

        static EncounterTable ReadOne(JsonElement table, string id, List<string> problems)
        {
            Keyed.OnlyKnown(table, new[] { "id", "trigger", "rolled", "entries" }, id, problems);

            Trigger trigger = ReadTrigger(table, id, problems);

            // the GM rolls behind the screen unless the table says otherwise - a tutorial might
            // want the player to watch the dice decide
            Visibility visibility = TableReader.Rolled(table, id, problems);

            // a table with nothing on it is a trigger that fires into silence, which no author means
            List<EncounterEntry> entries = TableReader.Entries(table, id, ReadEntry, "a quiet road", problems);

            return new EncounterTable(id, trigger, entries, visibility);
        }

        static Trigger ReadTrigger(JsonElement table, string id, List<string> problems)
        {
            if (!table.Has("trigger")) return Trigger.Always;

            JsonElement trigger = table.GetProperty("trigger");

            Keyed.OnlyKnown(trigger, new[] { "roll", "at_least" }, $"{id} trigger", problems);

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

        static readonly string[] EntryKeys = { "id", "kind", "weight", "monsters", "map", "loot" };

        static EncounterEntry ReadEntry(JsonElement entry, string table, List<string> problems)
        {
            string where = TableReader.Entry(entry, table, EntryKeys, problems, out EntryKind kind);

            if (where == null) return null;

            string id = entry.Text("id");
            int weight = entry.Weight(where, problems);

            List<Band> monsters = TableReader.Counted(entry, "monsters", "monster", "a monster id", where, problems)
                                             .Select(m => new Band(m.Id, m.Count)).ToList();

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
    }
}
