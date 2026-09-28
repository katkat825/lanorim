using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;

namespace Content.Campaigns
{
    // READS pack.json, AND SAYS WHY WHEN IT WON'T.
    //
    // LIFTED from the old build, which is where the shape of every message here comes from: name
    // the file, name the field, say what was expected and what was found, and never throw. A
    // campaign pack is a Workshop author's file in a text editor, so "this is not JSON" with a line
    // number is the difference between a fixable mistake and a pack that silently does nothing.
    //
    // WHAT CHANGED FOR LANORIM. A chapter is a sequence of MAPS, not of places - lanorim has no
    // World layer, so the map is the somewhere. And the old reader carried a second spelling for
    // format 1 (`encounters`); lanorim starts at format 1, no campaign has ever been written, and
    // there is nothing to be compatible with yet.
    public static partial class ManifestReader
    {
        // both file names are read; a campaign.json written before the pack kinds existed keeps working
        public const string PackFileName = "pack.json";

        public const string FileName = "campaign.json";

        // best first; Package looks in this order and complains if a folder has both
        public static readonly string[] FileNames = { PackFileName, FileName };

        // folder null skips the folder-matches-id check (a loose file has no folder)
        public static Read<Manifest> Parse(string json, string file, string folder = null)
        {
            var problems = new List<ContentProblem>();

            JsonDocument document = PackJson.ReadObject(json, file, "a campaign manifest", out ContentProblem bad);

            if (document == null) return Read<Manifest>.Bad(bad);

            using (document)
            {
                JsonElement root = document.RootElement;

                string id = Id(root, file, folder, problems);
                PackKind kind = Kind(root, file, problems);
                int format = Format(root, file, problems);
                Version engine = Engine(root, file, problems);
                string author = Text(root, "author", file, problems);
                IReadOnlyList<string> tags = Tags(root, file, problems);
                string preview = Text(root, "preview", file, problems);
                IReadOnlyList<string> needs = Dependencies(root, id, file, problems);
                IReadOnlyList<Chapter> chapters = Chapters(root, kind, file, problems);
                string start = Start(root, chapters, file, problems);
                string screen = root.TryGetProperty("gm_screen", out JsonElement named) &&
                                named.ValueKind == JsonValueKind.String
                    ? named.GetString()
                    : "blank";

                if (!Manifest.GmScreens.Contains(screen))
                    problems.Add(new ContentProblem(file, "gm_screen",
                        $"'{screen}' is not a GM screen - it is {Vocabulary.Offer(Manifest.GmScreens)}"));

                Unknown(root, file, problems);

                if (problems.Count > 0) return Read<Manifest>.Bad(problems);

                return Read<Manifest>.Good(
                    new Manifest(id, kind, format, engine, author, tags, preview, needs,
                                 chapters, start)
                    {
                        GmScreen = screen,
                    });
            }
        }

        static readonly string[] Fields =
        {
            "id", "kind", "format", "engine", "author", "tags", "preview", "dependencies",
            "chapters", "start", "gm_screen",
        };

        static void Unknown(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (Array.IndexOf(Fields, property.Name) >= 0) continue;

                // title/name/description get their own sentence; it's the mistake every modder makes
                if (property.Name == "title" || property.Name == "name" ||
                    property.Name == "description")
                {
                    string aspect = property.Name == "description" ? "description" : "name";

                    problems.Add(new ContentProblem(
                        file, property.Name,
                        $"a campaign's {property.Name} is not written here - it is a localized " +
                        "string, so it lives in this campaign's locale/ CSV under " +
                        $"'campaign.<id>.{aspect}' and is derived from the id rather than listed."));
                    continue;
                }

                problems.Add(new ContentProblem(
                    file, property.Name,
                    $"a campaign has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));
            }
        }
    }
}
