using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Content.Workshop.Tables
{
    // READING A TABLE FILE INTO A DRAFT, AND A DRAFT BACK INTO JSON (cc_task_n). The drafts read what the real readers
    // read (EncounterReader, LootReader, MerchantReader, ConsequenceReader: those still judge the file), but tolerantly:
    // a value is kept as written, and a key the editor doesn't know is kept whole, its raw JSON in the draft's `kept`, and
    // written back where it was (JsonSplice). Each such key is told once, so the author knows it rides along unedited
    public static class DraftJson
    {
        public static string Text(JsonObject o, string key)
        {
            JsonNode node = o?[key];

            return node switch
            {
                null => "",
                JsonValue v when v.TryGetValue(out string s) => s,
                _ => node.ToJsonString(),
            };
        }

        public static int Number(JsonObject o, string key, int otherwise)
        {
            JsonNode node = o?[key];

            if (node is JsonValue v)
            {
                if (v.TryGetValue(out int n)) return n;
                if (v.TryGetValue(out double d)) return (int)d;
                if (v.TryGetValue(out string s) && int.TryParse(s, out int parsed)) return parsed;
            }

            return otherwise;
        }

        public static bool Flag(JsonObject o, string key) => o?[key] is JsonValue v && v.TryGetValue(out bool b) && b;

        public static List<string> Strings(JsonObject o, string key) =>
            o?[key] is JsonArray list ? list.Select(i => i is JsonValue v && v.TryGetValue(out string s) ? s : i?.ToJsonString() ?? "").ToList()
                                      : new List<string>();

        public static IEnumerable<JsonObject> Objects(JsonObject o, string key) =>
            o?[key] is JsonArray list ? list.OfType<JsonObject>() : Enumerable.Empty<JsonObject>();

        // every key the editor doesn't read, as written; `told` hears "where: key" for each
        public static Dictionary<string, string> Kept(JsonObject o, IEnumerable<string> known, string where, List<string> told)
        {
            var kept = new Dictionary<string, string>(StringComparer.Ordinal);

            if (o == null) return kept;

            foreach (KeyValuePair<string, JsonNode> field in o.Where(f => !known.Contains(f.Key)))
            {
                kept[field.Key] = field.Value?.ToJsonString() ?? "null";
                told?.Add($"{where}: '{field.Key}'");
            }

            return kept;
        }

        // the kept keys back into an object being written
        public static void Put(JsonObject o, Dictionary<string, string> kept)
        {
            foreach (KeyValuePair<string, string> field in kept ?? new Dictionary<string, string>())
                o[field.Key] = JsonNode.Parse(field.Value);
        }

        public static void PutText(JsonObject o, string key, string text)
        {
            if (!string.IsNullOrEmpty(text)) o[key] = text;
        }

        public static void PutNumber(JsonObject o, string key, int number, int unsaid)
        {
            if (number != unsaid) o[key] = number;
        }

        public static void PutFlag(JsonObject o, string key, bool on)
        {
            if (on) o[key] = true;
        }

        public static JsonObject Parse(string text, out string bad)
        {
            bad = null;

            try
            {
                if (JsonNode.Parse(text ?? "", documentOptions: new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                    }) is JsonObject o) return o;

                bad = "the file is not a JSON object";
            }
            catch (JsonException not)
            {
                bad = not.Message;
            }

            return null;
        }
    }
}
