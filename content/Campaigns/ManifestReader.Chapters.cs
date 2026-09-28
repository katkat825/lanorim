using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Schema;
using Core.Words;

namespace Content.Campaigns
{
    public static partial class ManifestReader
    {
        static IReadOnlyList<Chapter> Chapters(JsonElement root, PackKind kind, string file,
                                               List<ContentProblem> problems)
        {
            var chapters = new List<Chapter>();

            bool playable = kind == PackKind.Campaign || kind == PackKind.Mixed;

            if (!root.TryGetProperty("chapters", out JsonElement value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                if (playable)
                    problems.Add(new ContentProblem(
                        file, "chapters",
                        "a campaign needs chapters, as a list in the order they are played - " +
                        "[ { \"id\": \"the_yard\", \"maps\": [ \"ash_yard\" ] } ]"));

                return chapters;
            }

            if (!playable)
            {
                problems.Add(new ContentProblem(
                    file, "chapters",
                    $"a {EnumWords.Name(kind)} pack has nothing to play, so it has " +
                    "no chapters - say \"kind\": \"mixed\" if this is a campaign that also " +
                    "ships its own art"));

                return chapters;
            }

            if (value.GetArrayLength() == 0)
                problems.Add(new ContentProblem(
                    file, "chapters", "a campaign with no chapters in it has nothing to play"));

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"chapters[{at++}]";

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"a chapter is an object and this is a {EnumWords.Name(entry.ValueKind)}"));
                    continue;
                }

                Chapter chapter = OneChapter(entry, file, where, problems);

                if (chapter == null) continue;

                if (chapters.Exists(c => c.Id == chapter.Id))
                {
                    problems.Add(new ContentProblem(
                        file, where, $"there are two chapters called '{chapter.Id}'"));
                    continue;
                }

                chapters.Add(chapter);
            }

            return chapters;
        }

        static readonly string[] ChapterFields = { "id", "maps" };

        static Chapter OneChapter(JsonElement entry, string file, string where,
                                  List<ContentProblem> problems)
        {
            foreach (JsonProperty property in entry.EnumerateObject())
                if (Array.IndexOf(ChapterFields, property.Name) < 0)
                    problems.Add(new ContentProblem(
                        file, $"{where}.{property.Name}",
                        $"a chapter has no '{property.Name}' - it has " +
                        $"{Vocabulary.Offer(ChapterFields)}. Its title is a localized string and " +
                        "lives in this campaign's locale/ CSV"));

            if (!entry.TryGetProperty("id", out JsonElement idValue) ||
                idValue.ValueKind != JsonValueKind.String ||
                !ContentId.IsLocal(idValue.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.id",
                    "a chapter needs an id - lowercase a-z, 0-9 and underscore. It becomes a " +
                    "segment of the chapter's title key"));
                return null;
            }

            string id = idValue.GetString();
            var maps = new List<string>();

            if (!entry.TryGetProperty("maps", out JsonElement list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.maps",
                    "a chapter needs its maps, by id, in the order they are played"));
                return null;
            }

            int at = 0;

            foreach (JsonElement one in list.EnumerateArray())
            {
                string spot = $"{where}.maps[{at++}]";

                if (one.ValueKind != JsonValueKind.String || !ContentId.IsLocal(one.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, spot, $"'{PackJson.Shown(one)}' is not a map id"));
                    continue;
                }

                maps.Add(one.GetString());
            }

            if (maps.Count == 0)
                problems.Add(new ContentProblem(
                    file, $"{where}.maps", $"chapter '{id}' has nothing in it"));

            return new Chapter(id, maps);
        }

        static string Start(JsonElement root, IReadOnlyList<Chapter> chapters, string file,
                            List<ContentProblem> problems)
        {
            string first = chapters.Count > 0 ? chapters[0].Id : "";

            if (!root.TryGetProperty("start", out JsonElement value)) return first;

            if (value.ValueKind == JsonValueKind.Null) return first;

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(
                    file, "start", $"'{PackJson.Shown(value)}' is not a chapter id"));
                return first;
            }

            string start = value.GetString() ?? "";

            if (chapters.Count > 0 && !HasChapter(chapters, start))
            {
                problems.Add(new ContentProblem(
                    file, "start",
                    $"this campaign starts at '{start}' and has no such chapter - it has " +
                    $"{Vocabulary.Offer(Names(chapters))}"));
                return first;
            }

            return start;
        }

        static bool HasChapter(IReadOnlyList<Chapter> chapters, string id)
        {
            foreach (Chapter chapter in chapters)
                if (chapter.Id == id) return true;

            return false;
        }

        static string[] Names(IReadOnlyList<Chapter> chapters)
        {
            var names = new string[chapters.Count];

            for (int i = 0; i < chapters.Count; i++) names[i] = chapters[i].Id;

            return names;
        }
    }
}
