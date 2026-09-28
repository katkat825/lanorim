using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Core.Characters;
using Core.Dice;
using Core.Words;

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

            if (EnumWords.TryParse(text, out Ability ability)) return ability;

            problems?.Add($"{where}: '{text}' is not an ability (str, dex, con, int, wis, cha)");

            return null;
        }

        public static Skill Skill(this JsonElement element, string name,
                                  List<string> problems = null, string where = null)
        {
            string text = element.Text(name);

            if (string.IsNullOrWhiteSpace(text)) return Core.Characters.Skill.None;

            if (EnumWords.TryParse(text, out Skill skill)) return skill;

            problems?.Add($"{where}: '{text}' is not one of the eighteen skills");

            return Core.Characters.Skill.None;
        }

        public static Condition Condition(this JsonElement element, string name,
                                          List<string> problems = null, string where = null)
        {
            string text = element.Text(name);

            if (string.IsNullOrWhiteSpace(text)) return Core.Characters.Condition.None;

            // Unconscious used to be refused here as the engine's own. Sleep puts a creature
            // Unconscious without it dropping to 0, so since 2026-09-24 content may name it
            if (EnumWords.TryParse(text, out Condition condition)) return condition;

            problems?.Add($"{where}: '{text}' is not a v1 condition (" +
                          string.Join(", ", Conditions.All.Select(c => c.Id())) + ")");

            return Core.Characters.Condition.None;
        }

        public static DamageType Damage(this JsonElement element, string name,
                                        List<string> problems = null, string where = null)
        {
            string text = element.Text(name);

            if (string.IsNullOrWhiteSpace(text)) return DamageType.None;

            if (EnumWords.TryParse(text, out DamageType type)) return type;

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

        // --- lists of one kind of word, each word checked -------------------------------------

        public static IReadOnlyList<Skill> SkillList(this JsonElement element, string name,
                                                     List<string> problems = null, string where = null) =>
            Words(element, name, problems, where, (string w, out Skill s) => EnumWords.TryParse(w, out s),
                  "one of the eighteen skills");

        public static IReadOnlyList<Ability> AbilityList(this JsonElement element, string name,
                                                         List<string> problems = null, string where = null) =>
            Words(element, name, problems, where, (string w, out Ability a) => EnumWords.TryParse(w, out a),
                  "an ability (str, dex, con, int, wis, cha)");

        public static IReadOnlyList<DamageType> DamageTypeList(this JsonElement element, string name,
                                                               List<string> problems = null, string where = null) =>
            Words(element, name, problems, where,
                  (string w, out DamageType d) => EnumWords.TryParse(w, out d) && d != DamageType.None,
                  "an SRD damage type");

        public static IReadOnlyList<Condition> ConditionList(this JsonElement element, string name,
                                                             List<string> problems = null, string where = null) =>
            Words(element, name, problems, where,
                  (string w, out Condition c) => EnumWords.TryParse(w, out c) && c != Core.Characters.Condition.None,
                  "a v1 condition");

        // "manoeuvres": ["dash", "disengage", "hide"] - what a bonus action may be spent on: a
        // rogue's Cunning Action, a goblin's Nimble Escape
        public static Manoeuvre ManoeuvreList(this JsonElement element, string name,
                                              List<string> problems = null, string where = null)
        {
            Manoeuvre all = Manoeuvre.None;

            foreach (Manoeuvre m in Words(element, name, problems, where,
                                          (string w, out Manoeuvre m) => EnumWords.TryParse(w, out m),
                                          "dash, disengage or hide"))
                all |= m;

            return all;
        }

        // "speed": a creature's walking speed in feet - a statblock's, a species', a form's. 30
        // when it isn't written; a climb, swim or fly speed has its own key
        public static int WalkingSpeed(this JsonElement entry, string where, List<string> problems)
        {
            int feet = entry.Number("speed", 30);

            if (feet < 0 || feet % 5 != 0)
                problems.Add($"{where}: 'speed' is walking feet, a multiple of 5");

            return feet;
        }

        // "weight": a draw's odds beside the others in its table - a loot table's, an encounter
        // table's, the consequence pool's. a whole number, 1 when it isn't written. read with a
        // fallback of 0 so a weight that is not a number is caught with one below 1
        public static int Weight(this JsonElement entry, string where, List<string> problems)
        {
            if (!entry.Has("weight")) return 1;

            int weight = entry.Number("weight", 0);

            if (weight >= 1) return weight;

            problems.Add($"{where}: a weight is a whole number, 1 or more");
            return 1;
        }

        // {"str": 14, "dex": 16} - a number for each ability: a statblock's scores, a form's, a
        // species' bumps. one reader where each of the three read it by hand
        public static Dictionary<Ability, int> AbilityRecord(this JsonElement element, string name,
                                                             List<string> problems = null, string where = null)
        {
            var record = new Dictionary<Ability, int>();

            if (!element.Has(name)) return record;

            foreach (JsonProperty entry in element.GetProperty(name).EnumerateObject())
            {
                if (EnumWords.TryParse(entry.Name, out Ability ability) && entry.Value.TryGetInt32(out int value))
                    record[ability] = value;
                else
                    problems?.Add($"{where}: '{entry.Name}' in '{name}' is not an ability with a number");
            }

            return record;
        }

        // "tag_rules": {"humanoid": "only", "sleepless": "untouched"} - what a creature's tags do to
        // an effect, the same words on a spell and on a statblock's on-hit rider
        public static IReadOnlyList<TagRule> TagRuleList(this JsonElement element, string name,
                                                         List<string> problems = null, string where = null)
        {
            if (!element.Has(name)) return Array.Empty<TagRule>();

            JsonElement rules = element.GetProperty(name);

            if (rules.ValueKind != JsonValueKind.Object)
            {
                problems?.Add($"{where}: '{name}' is a record of tag: outcome");
                return Array.Empty<TagRule>();
            }

            var list = new List<TagRule>();

            foreach (JsonProperty rule in rules.EnumerateObject())
            {
                if (rule.Value.ValueKind == JsonValueKind.String &&
                    EnumWords.TryParse(rule.Value.GetString(), out TagOutcome outcome))
                    list.Add(new TagRule(rule.Name, outcome));
                else
                    problems?.Add($"{where}: tag '{rule.Name}' in '{name}' - the outcome is only, " +
                                  "untouched, auto_save, auto_fail or save_disadvantage");
            }

            return list;
        }

        // "defenses": {"poison": "resistant", "fire": "immune"} - what damage of each type does to a
        // creature: a statblock's, a boon's
        public static IReadOnlyDictionary<DamageType, Defense> DefenseRecord(this JsonElement element, string name,
                                                                              List<string> problems = null,
                                                                              string where = null)
        {
            var table = new Dictionary<DamageType, Defense>();

            if (!element.Has(name) || element.GetProperty(name).ValueKind != JsonValueKind.Object) return table;

            foreach (JsonProperty entry in element.GetProperty(name).EnumerateObject())
            {
                if (EnumWords.TryParse(entry.Name, out DamageType type) && type != DamageType.None &&
                    entry.Value.ValueKind == JsonValueKind.String &&
                    EnumWords.TryParse(entry.Value.GetString(), out Defense defense))
                    table[type] = defense;
                else
                    problems?.Add($"{where}: '{entry.Name}' in '{name}' is not a damage type and a defense " +
                                  "(resistant, immune, vulnerable)");
            }

            return table;
        }

        delegate bool Parse<T>(string word, out T value);

        static IReadOnlyList<T> Words<T>(JsonElement element, string name, List<string> problems,
                                         string where, Parse<T> parse, string what)
        {
            var list = new List<T>();

            foreach (string word in element.Strings(name))
            {
                if (parse(word, out T read)) list.Add(read);
                else problems?.Add($"{where}: '{word}' in '{name}' is not {what}");
            }

            return list;
        }
    }
}
