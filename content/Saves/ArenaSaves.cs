using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Challengers;
using Content.Schema;
using Core.Tables;
using Core.Words;

namespace Content.Saves
{
    // THE ARENA IN A SAVE (cc_task_cg 3.1), its own section beside the hero: reputation, the roster as drawn, the pick,
    // the specials beaten and each list's shuffle. Read as far as it can be, like the rest of a save
    //   "arena": { "reputation": 5, "roster": [ { "id", "type"|"entry", "shape", "name", "description", "difficulty",
    //              "xp", "group": [ { "monster", "count" } ] } ], "picked": { ... }, "beaten": [ ... ],
    //              "bags": { "goblin_warrior.names": { "last": 3, "left": [ 5, 1 ] } } }
    public static class ArenaSaves
    {
        public static void Write(Utf8JsonWriter json, Arena arena)
        {
            json.WriteStartObject();

            json.WriteNumber("reputation", arena.Reputation);
            if (arena.Drawn) json.WriteBoolean("drawn", true);

            json.WriteStartArray("roster");
            foreach (Challenger one in arena.Roster) WriteOne(json, one);
            json.WriteEndArray();

            if (arena.Picked != null)
            {
                json.WritePropertyName("picked");
                WriteOne(json, arena.Picked);
            }

            if (arena.Beaten.Count > 0)
            {
                json.WriteStartArray("beaten");
                foreach (string id in arena.Beaten) json.WriteStringValue(id);
                json.WriteEndArray();
            }

            json.WriteStartObject("bags");

            foreach (KeyValuePair<string, Arena.Bag> bag in arena.Bags)
            {
                json.WriteStartObject(bag.Key);
                json.WriteNumber("last", bag.Value.Last);
                json.WriteStartArray("left");
                foreach (int index in bag.Value.Left) json.WriteNumberValue(index);
                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndObject();

            json.WriteEndObject();
        }

        static void WriteOne(Utf8JsonWriter json, Challenger one)
        {
            json.WriteStartObject();
            json.WriteString("id", one.Id);

            if (one.Special) json.WriteString("entry", one.Entry);
            else json.WriteString("type", one.Type);

            json.WriteString("shape", EnumWords.Name(one.Shape));

            if (!one.Special)
            {
                json.WriteNumber("name", one.Name);
                json.WriteNumber("description", one.Description);
                json.WriteString("name_key", one.NameKey);
                json.WriteString("description_key", one.DescriptionKey);
            }

            json.WriteString("difficulty", EnumWords.Name(one.Difficulty));
            json.WriteNumber("xp", one.Xp);

            json.WriteStartArray("group");

            foreach (Mustered band in one.Group)
            {
                json.WriteStartObject();
                json.WriteString("monster", band.Monster);
                json.WriteNumber("count", band.Count);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        public static Arena Read(JsonElement entry, string file, List<ContentProblem> problems)
        {
            var arena = new Arena { Reputation = System.Math.Max(0, entry.Number("reputation", 0)), Drawn = entry.Flag("drawn") };

            foreach (JsonElement one in entry.Items("roster"))
                if (ReadOne(one, file, problems) is Challenger read) arena.Roster.Add(read);

            if (entry.Has("picked")) arena.Picked = ReadOne(entry.GetProperty("picked"), file, problems);

            foreach (string id in entry.Strings("beaten")) arena.Beaten.Add(id);

            if (entry.TryGetProperty("bags", out JsonElement bags) && bags.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty bag in bags.EnumerateObject())
                {
                    var kept = new Arena.Bag { Last = bag.Value.Number("last", 0) };

                    foreach (JsonElement index in bag.Value.Items("left"))
                        if (index.ValueKind == JsonValueKind.Number && index.TryGetInt32(out int i)) kept.Left.Add(i);

                    arena.Bags[bag.Name] = kept;
                }

            return arena;
        }

        static Challenger ReadOne(JsonElement one, string file, List<ContentProblem> problems)
        {
            string id = one.Text("id");

            if (id.Length == 0)
            {
                problems.Add(ContentProblem.Caution(file, "arena", "a challenger with no id was left off the roster"));
                return null;
            }

            EnumWords.TryName(one.Text("shape"), out ChallengerShape shape);
            EnumWords.TryName(one.Text("difficulty"), out EncounterDifficulty difficulty);

            return new Challenger
            {
                Id = id,
                Type = one.Text("type"),
                Entry = one.Text("entry"),
                Shape = shape,
                Name = one.Number("name", 0),
                Description = one.Number("description", 0),
                NameKey = one.Text("name_key"),
                DescriptionKey = one.Text("description_key"),
                Difficulty = difficulty,
                Xp = one.Number("xp", 0),
                Group = one.Items("group").Select(m => new Mustered(m.Text("monster"), System.Math.Max(1, m.Number("count", 1))))
                           .ToList(),
            };
        }
    }
}
