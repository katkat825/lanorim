using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Schema;
using Core.Words;

namespace Content.Campaigns
{
    public static partial class ManifestReader
    {
        // the campaign's play rules (CampaignRules), each one left out meaning the game's default
        static readonly string[] RuleFields = { "defeat", "leveling", "characters", "starting_shop", "consequences" };

        static CampaignRules Rules(JsonElement root, string file, List<ContentProblem> problems)
        {
            CampaignRules rules = CampaignRules.Default;

            Core.Combat.Defeat defeat = Word(root, "defeat", rules.Defeat, file, problems);

            if (defeat == Core.Combat.Defeat.Unsaid)
            {
                problems.Add(new ContentProblem(file, "defeat", "'defeat' is death or continues"));
                defeat = rules.Defeat;
            }

            int characters = rules.Characters;

            if (root.TryGetProperty("characters", out JsonElement count) &&
                (count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out characters) ||
                 characters < 1 || characters > CampaignRules.MostCharacters))
            {
                problems.Add(new ContentProblem(file, "characters",
                    $"'{PackJson.Shown(count)}' is not a number of characters - 1 to {CampaignRules.MostCharacters}, " +
                    $"and {rules.Characters} when it's left out"));
                characters = rules.Characters;
            }

            bool shop = rules.StartingShop;

            if (root.TryGetProperty("starting_shop", out JsonElement opens))
            {
                if (opens.ValueKind is JsonValueKind.True or JsonValueKind.False) shop = opens.GetBoolean();
                else problems.Add(new ContentProblem(file, "starting_shop",
                    $"'{PackJson.Shown(opens)}' is not true or false - it says whether a new hero opens on the starting shop"));
            }

            return new CampaignRules
            {
                Defeat = defeat,
                Leveling = Word(root, "leveling", rules.Leveling, file, problems),
                Characters = characters,
                StartingShop = shop,
                Consequences = Word(root, "consequences", rules.Consequences, file, problems),
            };
        }

        static T Word<T>(JsonElement root, string field, T otherwise, string file, List<ContentProblem> problems)
            where T : struct, Enum
        {
            if (!root.TryGetProperty(field, out JsonElement value)) return otherwise;

            if (value.ValueKind == JsonValueKind.String && EnumWords.TryName(value.GetString(), out T read)) return read;

            problems.Add(new ContentProblem(file, field, $"'{PackJson.Shown(value)}' is not one of {Vocabulary.Offer<T>()}"));
            return otherwise;
        }
    }
}
