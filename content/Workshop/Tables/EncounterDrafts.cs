using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Content.Encounters;
using Core.Tables;
using Core.Words;

namespace Content.Workshop.Tables
{
    // AN ENCOUNTER TABLE AS THE WORKSHOP EDITS IT (cc_task_n Parts 1-2): the file it lives in, its id, how often it
    // fires, whether the roll is heard, and its entries. The same keys EncounterReader reads, as written: a value the
    // editor didn't touch is the file's own (JsonSplice), and a key it doesn't know is kept
    public sealed class EncounterTableDraft : TableDraft<EncounterDraft>
    {
        // null: always fires (no trigger)
        public TriggerDraft Trigger { get; set; }

        public static readonly string[] Keys = { "id", "trigger", "rolled", "entries" };

        public static EncounterTableDraft Read(JsonObject table, string file, List<string> told)
        {
            string id = DraftJson.Text(table, "id");
            JsonObject trigger = table["trigger"] as JsonObject;

            return new EncounterTableDraft
            {
                File = file,
                Id = id,
                Trigger = trigger == null ? null : new TriggerDraft
                {
                    Roll = DraftJson.Text(trigger, "roll"),
                    AtLeast = DraftJson.Number(trigger, "at_least", 0),
                    Kept = DraftJson.Kept(trigger, TriggerDraft.Keys, $"{file} {id} trigger", told),
                },
                Rolled = DraftJson.Text(table, "rolled"),
                Entries = DraftJson.Objects(table, "entries").Select(e => EncounterDraft.Read(e, $"{file} {id}", told)).ToList(),
                Kept = DraftJson.Kept(table, Keys, $"{file} {id}", told),
            };
        }

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["id"] = Id };

            if (Trigger != null)
            {
                var trigger = new JsonObject { ["roll"] = Trigger.Roll, ["at_least"] = Trigger.AtLeast };
                DraftJson.Put(trigger, Trigger.Kept);
                o["trigger"] = trigger;
            }

            DraftJson.PutText(o, "rolled", Rolled);
            o["entries"] = new JsonArray(Entries.Select(e => (JsonNode)e.ToJson()).ToArray());
            DraftJson.Put(o, Kept);
            return o;
        }

    }

    // WHAT AN ENCOUNTER TABLE AND A LOOT TABLE SHARE, as the Workshop edits them: the file it lives in, its id, whether
    // the roll is heard ("", hidden, quiet or shown: TableReader.Rolled), its entries, and the keys it doesn't know
    public abstract class TableDraft<T> where T : class, IDraftEntry
    {
        // the file it lives in, under the campaign: "encounters/encounters.json"
        public string File { get; set; } = "";

        public string Id { get; set; } = "";

        public string Rolled { get; set; } = "";

        public List<T> Entries { get; set; } = new();

        public Dictionary<string, string> Kept { get; set; } = new();

        public T Entry(string id) => Entries.FirstOrDefault(e => e.Id == id);

        public override string ToString() => $"{File} {Id}: {Entries.Count} entries";
    }

    public interface IDraftEntry
    {
        string Id { get; }
    }

    // "roll a d20, on 11 or more something happens" (HowOften says it in words)
    public sealed class TriggerDraft
    {
        public string Roll { get; set; } = "1d20";

        public int AtLeast { get; set; }

        public Dictionary<string, string> Kept { get; set; } = new();

        public static readonly string[] Keys = { "roll", "at_least" };
    }

    // ONE ENTRY: a fight, a moment (one line) or nothing. Its line - what the GM says when it comes up - is the locale's
    // (encounter.<table>.line.<entry>), and kept here with it, so the form writes the words and the key together
    public sealed class EncounterDraft : IDraftEntry
    {
        public string Id { get; set; } = "";

        public EntryKind Kind { get; set; } = EntryKind.Fight;

        // 1 is unsaid
        public int Weight { get; set; } = 1;

        public List<BandDraft> Monsters { get; set; } = new();

        public string Map { get; set; } = "";

        public string Loot { get; set; } = "";

        // "", none, leader or half
        public string Morale { get; set; } = "";

        // 0 is unsaid
        public int Level { get; set; }

        // "", low, moderate or high
        public string Difficulty { get; set; } = "";

        // "", death or continues
        public string Defeat { get; set; } = "";

        // what the GM says when it comes up (the locale's English)
        public string Line { get; set; } = "";

        public Dictionary<string, string> Kept { get; set; } = new();

        public static string[] Keys => EncounterReader.EntryKeys.ToArray();

        public static EncounterDraft Read(JsonObject entry, string where, List<string> told)
        {
            string id = DraftJson.Text(entry, "id");
            EnumWords.TryParse(DraftJson.Text(entry, "kind"), out EntryKind kind);

            return new EncounterDraft
            {
                Id = id,
                Kind = kind,
                Weight = DraftJson.Number(entry, "weight", 1),
                Monsters = DraftJson.Objects(entry, "monsters").Select(m => BandDraft.Read(m, $"{where}.{id}", told)).ToList(),
                Map = DraftJson.Text(entry, "map"),
                Loot = DraftJson.Text(entry, "loot"),
                Morale = DraftJson.Text(entry, "morale"),
                Level = DraftJson.Number(entry, "level", 0),
                Difficulty = DraftJson.Text(entry, "difficulty"),
                Defeat = DraftJson.Text(entry, "defeat"),
                Kept = DraftJson.Kept(entry, Keys, $"{where}.{id}", told),
            };
        }

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["id"] = Id };

            DraftJson.PutNumber(o, "level", Level, 0);
            DraftJson.PutText(o, "difficulty", Difficulty);
            o["kind"] = EnumWords.Name(Kind);
            DraftJson.PutNumber(o, "weight", Weight, 1);
            DraftJson.PutText(o, "map", Map);
            DraftJson.PutText(o, "loot", Loot);
            DraftJson.PutText(o, "morale", Morale);
            DraftJson.PutText(o, "defeat", Defeat);

            if (Monsters.Count > 0) o["monsters"] = new JsonArray(Monsters.Select(m => (JsonNode)m.ToJson()).ToArray());

            DraftJson.Put(o, Kept);
            return o;
        }

        public bool IsFight => Kind == EntryKind.Fight;

        public override string ToString() => $"{Id} {Kind}";
    }

    // which monster, how many ("2", "1d4"; "" is one) and whether it leads
    public sealed class BandDraft
    {
        public string Monster { get; set; } = "";

        public string Count { get; set; } = "";

        public bool Leader { get; set; }

        public Dictionary<string, string> Kept { get; set; } = new();

        public static BandDraft Read(JsonObject band, string where, List<string> told) => new()
        {
            Monster = DraftJson.Text(band, "monster"),
            Count = DraftJson.Text(band, "count"),
            Leader = DraftJson.Flag(band, "leader"),
            Kept = DraftJson.Kept(band, EncounterReader.MonsterKeys, $"{where} {DraftJson.Text(band, "monster")}", told),
        };

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["monster"] = Monster };
            DraftJson.PutText(o, "count", Count);
            DraftJson.PutFlag(o, "leader", Leader);
            DraftJson.Put(o, Kept);
            return o;
        }

        public override string ToString() => $"{(Count.Length == 0 ? "1" : Count)} {Monster}";
    }
}
