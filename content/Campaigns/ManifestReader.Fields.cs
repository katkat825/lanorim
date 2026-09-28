using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Schema;
using Core.Words;

namespace Content.Campaigns
{
    public static partial class ManifestReader
    {
        static string Id(JsonElement root, string file, string folder, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    "a campaign needs an id, as a string - it is the namespace everything in the " +
                    "folder registers under"));
                return null;
            }

            string id = value.GetString() ?? "";

            if (!ContentId.IsCampaign(id))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    $"'{id}' is not a campaign id - lowercase a-z, 0-9 and underscore, and no " +
                    "dots. It becomes a segment of every key this campaign emits"));
                return null;
            }

            // folder and id must match; picking a winner would silently re-scope every key
            if (folder != null && !string.Equals(folder, id, StringComparison.Ordinal))
                problems.Add(new ContentProblem(
                    file, "id",
                    $"this campaign calls itself '{id}' and its folder is called '{folder}' - " +
                    "they have to match, because the folder is how a player finds it and the id " +
                    "is how every key in it is spelled"));

            return id;
        }

        // optional in campaign.json (defaults to campaign), required in pack.json
        static PackKind Kind(JsonElement root, string file, List<ContentProblem> problems)
        {
            bool isPackFile = string.Equals(file, PackFileName, StringComparison.Ordinal);

            if (!root.TryGetProperty("kind", out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null)
            {
                if (isPackFile)
                    problems.Add(new ContentProblem(
                        file, "kind",
                        $"a {PackFileName} says what kind of pack it is - it is " +
                        $"{Vocabulary.Offer<PackKind>()}. ({FileName} may leave it out and is " +
                        "taken to be a campaign)"));

                return PackKind.Campaign;
            }

            if (value.ValueKind == JsonValueKind.String &&
                EnumWords.TryName(value.GetString(), out PackKind kind))
            {
                return kind;
            }

            problems.Add(new ContentProblem(
                file, "kind",
                $"'{PackJson.Shown(value)}' is not a kind of pack - it is {Vocabulary.Offer<PackKind>()}"));

            return PackKind.Campaign;
        }

        static int Format(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("format", out JsonElement value) ||
                value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int format))
            {
                problems.Add(new ContentProblem(
                    file, "format",
                    "a campaign needs a content format version, as a whole number - this build " +
                    $"writes {ContentFormat.Current}"));
                return 0;
            }

            if (!ContentFormat.Range.CanRead(format))
            {
                problems.Add(new ContentProblem(file, "format", ContentFormat.WhyNot(format)));
                return 0;
            }

            return format;
        }

        static Version Engine(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("engine", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String ||
                !Version.TryParse(value.GetString(), out Version engine))
            {
                problems.Add(new ContentProblem(
                    file, "engine",
                    "a campaign needs the oldest engine that can play it, as a string like " +
                    $"\"{Core.EngineVersion.Current}\""));
                return null;
            }

            // refused rather than half-played: a missing rule would look right and be wrong mid-fight
            if (!Core.EngineVersion.Satisfies(engine))
                problems.Add(new ContentProblem(
                    file, "engine",
                    $"this campaign needs engine {engine} and this is {Core.EngineVersion.Current} " +
                    "- update the game"));

            return engine;
        }

        static string Text(JsonElement root, string field, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty(field, out JsonElement value)) return "";

            if (value.ValueKind == JsonValueKind.Null) return "";

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(file, field, $"'{PackJson.Shown(value)}' is not a string"));
                return "";
            }

            return value.GetString() ?? "";
        }
    }
}
