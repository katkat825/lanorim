using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Schema;

namespace Content.Campaigns
{
    public static partial class ManifestReader
    {
        static IReadOnlyList<string> Tags(JsonElement root, string file,
                                          List<ContentProblem> problems)
        {
            var tags = new List<string>();

            if (!root.TryGetProperty("tags", out JsonElement value)) return tags;

            if (value.ValueKind == JsonValueKind.Null) return tags;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "tags", "tags is a list of words, like [ \"undead\", \"short\" ]"));
                return tags;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"tags[{at++}]";

                if (entry.ValueKind != JsonValueKind.String)
                {
                    problems.Add(new ContentProblem(file, where, $"'{PackJson.Shown(entry)}' is not a word"));
                    continue;
                }

                string tag = entry.GetString() ?? "";

                if (!ContentId.IsLocal(tag))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{tag}' is not a tag - lowercase a-z, 0-9 and underscore. A tag is a " +
                        "word a storefront filters on, not a phrase a player reads"));
                    continue;
                }

                if (tags.Contains(tag))
                {
                    problems.Add(new ContentProblem(file, where, $"'{tag}' is listed twice"));
                    continue;
                }

                tags.Add(tag);
            }

            return tags;
        }

        // pack ids only; whether they're installed is the shelf's question, not this reader's
        static IReadOnlyList<string> Dependencies(JsonElement root, string id, string file,
                                                  List<ContentProblem> problems)
        {
            var needs = new List<string>();

            if (!root.TryGetProperty("dependencies", out JsonElement value)) return needs;

            if (value.ValueKind == JsonValueKind.Null) return needs;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "dependencies",
                    "dependencies is a list of the ids of packs this one needs, like " +
                    "[ \"grimdark_minis\" ]"));
                return needs;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"dependencies[{at++}]";

                if (entry.ValueKind != JsonValueKind.String ||
                    !ContentId.IsCampaign(entry.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{PackJson.Shown(entry)}' is not a pack id - lowercase a-z, 0-9 and underscore, " +
                        "the same spelling as the pack's own id"));
                    continue;
                }

                string needed = entry.GetString();

                // self-dependency would loop the resolver; it's a typo
                if (string.Equals(needed, id, StringComparison.Ordinal))
                {
                    problems.Add(new ContentProblem(
                        file, where, $"'{needed}' is this pack - it cannot depend on itself"));
                    continue;
                }

                if (needs.Contains(needed))
                {
                    problems.Add(new ContentProblem(file, where, $"'{needed}' is listed twice"));
                    continue;
                }

                needs.Add(needed);
            }

            return needs;
        }
    }
}
