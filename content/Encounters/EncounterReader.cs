using Core.Words;
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

        public static readonly IReadOnlyList<string> EntryKeys =
            new[] { "id", "kind", "weight", "monsters", "map", "loot", "morale", "level", "difficulty", "defeat" };

        // a monster entry's flag: the leader whose fall breaks a "morale": "leader" fight (cc_task_h 3.4)
        const string LeaderFlag = "leader";

        // what a fight's monster entry takes: which, how many, and whether it leads (the vocabulary's registry)
        public static readonly IReadOnlyList<string> MonsterKeys = new[] { "monster", "count", LeaderFlag };

        static EncounterEntry ReadEntry(JsonElement entry, string table, List<string> problems)
        {
            string where = TableReader.Entry(entry, table, EntryKeys.ToArray(), problems, out EntryKind kind);

            if (where == null) return null;

            string id = entry.Text("id");
            int weight = entry.Weight(where, problems);

            List<Band> monsters = TableReader.Counted(entry, "monsters", "monster", "a monster id", where, problems,
                                                      new[] { LeaderFlag })
                                             .Select(m => new Band(m.Id, m.Count, m.Flags.Contains(LeaderFlag)))
                                             .ToList();

            // when its enemies break: none (the default), leader or half (cc_task_h 3.4)
            Core.Combat.Morale morale = Core.Combat.Morale.None;

            if (entry.Has("morale") && !EnumWords.TryName(entry.Text("morale"), out morale))
                problems.Add($"{where}: '{entry.Text("morale")}' is not a morale - none, leader or half");

            if (morale != Core.Combat.Morale.None && kind != EntryKind.Fight)
                problems.Add($"{where}: only a fight has a morale");

            // the level it's meant for and how hard, for the campaign check's solo budget (cc_task_h 3.5)
            int level = entry.Number("level", 0);

            if (entry.Has("level") && (level < 1 || level > 20))
                problems.Add($"{where}: 'level' is a hero's level, 1 to 20 - not {entry.Text("level")}");

            EncounterDifficulty difficulty = EncounterDifficulty.Moderate;

            if (entry.Has("difficulty") && !EnumWords.TryName(entry.Text("difficulty"), out difficulty))
                problems.Add($"{where}: '{entry.Text("difficulty")}' is not a difficulty - low, moderate or high");

            if ((entry.Has("level") || entry.Has("difficulty")) && kind != EntryKind.Fight)
                problems.Add($"{where}: only a fight has a level and a difficulty");

            // what losing it means, for this fight alone (cc_task_i 3.1)
            Core.Combat.Defeat defeat = Core.Combat.Defeat.Unsaid;

            if (entry.Has("defeat") && (!EnumWords.TryName(entry.Text("defeat"), out defeat) || defeat == Core.Combat.Defeat.Unsaid))
                problems.Add($"{where}: '{entry.Text("defeat")}' is not a defeat - death or continues");

            if (entry.Has("defeat") && kind != EntryKind.Fight)
                problems.Add($"{where}: only a fight has a defeat");

            if (monsters.Count(m => m.Leader) > 1)
                problems.Add($"{where}: one leader to a fight - {monsters.Count(m => m.Leader)} are marked");

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

            return new EncounterEntry(id, kind, weight, monsters, map, loot, morale, System.Math.Max(0, level), difficulty, defeat);
        }
    }
}
