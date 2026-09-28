using System.Text.Json;
using Core.Words;

namespace Content.Schema
{
    // A PACK FILE'S JSON, read the way every pack reader reports on it: a manifest, a save, a bark
    // bank, a beats or hints file. one parse and one way of quoting a bad value back, where each
    // reader had its own (cc_task_dedupe-methods.md #4)
    public static class PackJson
    {
        // the file's text as a JSON object, or null and the problem that stops it: not JSON (with the
        // line it broke on), or JSON that isn't an object. `what` is what the file is meant to be -
        // "a save", "a beats file". the caller disposes the document
        public static JsonDocument ReadObject(string json, string file, string what, out ContentProblem problem)
        {
            problem = null;

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(json ?? "", Json.Options);
            }
            catch (JsonException bad)
            {
                problem = new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1);
                return null;
            }

            JsonValueKind kind = document.RootElement.ValueKind;

            if (kind == JsonValueKind.Object) return document;

            document.Dispose();
            problem = new ContentProblem(file, "", $"{what} is a JSON object and this is a {EnumWords.Name(kind)}");
            return null;
        }

        // a value as the file wrote it, for a problem to quote back: the string itself, not "\"x\""
        public static string Shown(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Undefined => "nothing",
            _ => value.ToString(),
        };
    }
}
