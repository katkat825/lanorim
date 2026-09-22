using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Core.Characters;
using Core.Dice;

namespace Content.Schema
{
    // a thin reading layer over System.Text.Json. every getter has a default and none of them
    // throw: a content file is read by a loader that collects problems and refuses the pack, not
    // by code that falls over on the first missing field.
    public static class Json
    {
        public static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static bool TryParse(string text, out JsonDocument document, out string problem)
        {
            document = null;
            problem = null;

            try
            {
                document = JsonDocument.Parse(text ?? "", Options);
                return true;
            }
            catch (JsonException bad)
            {
                problem = $"not valid json: {bad.Message}";
                return false;
            }
        }

        public static bool Has(this JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out JsonElement found) &&
            found.ValueKind != JsonValueKind.Null;

        public static string Text(this JsonElement element, string name, string fallback = "")
        {
            if (!element.Has(name)) return fallback;

            JsonElement found = element.GetProperty(name);

            return found.ValueKind == JsonValueKind.String ? found.GetString() : fallback;
        }

        public static int Number(this JsonElement element, string name, int fallback = 0)
        {
            if (!element.Has(name)) return fallback;

            JsonElement found = element.GetProperty(name);

            if (found.ValueKind == JsonValueKind.Number && found.TryGetInt32(out int value))
                return value;

            if (found.ValueKind == JsonValueKind.String &&
                int.TryParse(found.GetString(), NumberStyles.Integer,
                             CultureInfo.InvariantCulture, out int parsed))
                return parsed;

            return fallback;
        }

        public static bool Flag(this JsonElement element, string name, bool fallback = false)
        {
            if (!element.Has(name)) return fallback;

            JsonElement found = element.GetProperty(name);

            return found.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => fallback,
            };
        }

        public static IReadOnlyList<string> Strings(this JsonElement element, string name)
        {
            if (!element.Has(name)) return Array.Empty<string>();

            JsonElement found = element.GetProperty(name);

            if (found.ValueKind != JsonValueKind.Array) return Array.Empty<string>();

            var list = new List<string>();

            foreach (JsonElement item in found.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                    list.Add(item.GetString());

            return list;
        }

        public static IReadOnlyList<JsonElement> Items(this JsonElement element, string name)
        {
            if (!element.Has(name)) return Array.Empty<JsonElement>();

            JsonElement found = element.GetProperty(name);

            if (found.ValueKind != JsonValueKind.Array) return Array.Empty<JsonElement>();

            var list = new List<JsonElement>();

            foreach (JsonElement item in found.EnumerateArray()) list.Add(item);

            return list;
        }

        // dice are written the way the SRD writes them and parsed the same way everywhere
        public static DiceRoll Dice(this JsonElement element, string name, List<string> problems = null,
                                    string where = null)
        {
            string text = element.Text(name);

            if (string.IsNullOrWhiteSpace(text)) return DiceRoll.None;

            if (DiceRoll.TryParse(text, out DiceRoll dice, out string problem)) return dice;

            problems?.Add($"{where}: '{text}' in '{name}' is not dice - {problem}");

            return DiceRoll.None;
        }

        public static Ability? Ability(this JsonElement element, string name,
                                       List<string> problems = null, string where = null)
        {
            string text = element.Text(name);

            if (string.IsNullOrWhiteSpace(text)) return null;

            if (Abilities.TryParse(text, out Ability ability)) return ability;

            problems?.Add($"{where}: '{text}' is not an ability (str, dex, con, int, wis, cha)");

            return null;
        }

        public static Skill Skill(this JsonElement element, string name,
                                  List<string> problems = null, string where = null)
        {
            string text = element.Text(name);

            if (string.IsNullOrWhiteSpace(text)) return Core.Characters.Skill.None;

            if (Skills.TryParse(text, out Skill skill)) return skill;

            problems?.Add($"{where}: '{text}' is not one of the eighteen skills");

            return Core.Characters.Skill.None;
        }

        public static Condition Condition(this JsonElement element, string name,
                                          List<string> problems = null, string where = null)
        {
            string text = element.Text(name);

            if (string.IsNullOrWhiteSpace(text)) return Core.Characters.Condition.None;

            if (Conditions.TryParse(text, out Condition condition))
            {
                if (condition == Core.Characters.Condition.Unconscious)
                {
                    problems?.Add($"{where}: 'unconscious' is the engine's, not content's - " +
                                  "it comes from hitting 0 hit points and nothing else");

                    return Core.Characters.Condition.None;
                }

                return condition;
            }

            problems?.Add($"{where}: '{text}' is not a v1 condition " +
                          "(prone, poisoned, stunned, frightened, restrained, grappled)");

            return Core.Characters.Condition.None;
        }

        public static DamageType Damage(this JsonElement element, string name,
                                        List<string> problems = null, string where = null)
        {
            string text = element.Text(name);

            if (string.IsNullOrWhiteSpace(text)) return DamageType.None;

            if (DamageTypes.TryParse(text, out DamageType type)) return type;

            problems?.Add($"{where}: '{text}' is not an SRD damage type");

            return DamageType.None;
        }

        // ids are one key segment: lowercase, digits and underscore, because they end up in a
        // localization key and a bad one has to be refused at load, not in a later audit
        public static bool IsId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            foreach (char c in id)
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                    return false;

            return true;
        }
    }
}
