using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Magic;
using Core.Words;

namespace Content.Spells
{
    // WHAT A SPELL LEAVES ON A CREATURE, read in one place: the way out, the save track, what damage
    // does to it, what holds it to a zone (cc_task_dedupe-effects.md, Phase 4). the keys sit on the
    // effect's own object, beside its other settings
    public static class LingerSpecReader
    {
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "escape", "repeat_save", "on_damage", "while_in_zone", "ends_on_act", "shakeable",
            "flees", "gone", "permanent_after_rounds",
        };

        public static readonly IReadOnlyList<string> EscapeKeys = new[] { "ability", "skill", "dc" };

        public static readonly IReadOnlyList<string> RepeatSaveKeys =
            new[] { "ability", "ends_after", "worsens", "worsens_after", "only_unseen" };

        public static readonly IReadOnlyList<string> GoneKeys = new[] { "after_rounds", "tag_rules" };

        public static LingerSpec Read(JsonElement raw, string where, List<string> problems)
        {
            if (!EnumWords.TryParse(raw.Text("on_damage", "nothing"), out OnDamage onDamage))
                problems.Add($"{where}: 'on_damage' is 'ends', 'ends_if_caster_side', 'ends_at_zero' " +
                             "or 'saves_again'");

            return new LingerSpec
            {
                Escape = ReadEscape(raw, where, problems),
                RepeatSave = ReadRepeatSave(raw, where, problems),
                OnDamage = onDamage,
                WhileInZone = raw.Flag("while_in_zone"),
                EndsOnAct = raw.Flag("ends_on_act"),
                Shakeable = raw.Flag("shakeable"),
                Flees = raw.Flag("flees"),
                Gone = ReadGone(raw, where, problems),
                PermanentAfterRounds = raw.Number("permanent_after_rounds"),
            };
        }

        // "escape": {"ability": "int", "skill": "investigation", "dc": 20}
        static Escape ReadEscape(JsonElement raw, string where, List<string> problems)
        {
            if (!Record(raw, "escape", where, problems, EscapeKeys, out JsonElement escape)) return null;

            Ability? ability = escape.Ability("ability", problems, where);

            if (!ability.HasValue)
            {
                problems.Add($"{where}: an 'escape' needs the 'ability' its check is made with");
                return null;
            }

            Skill skill = escape.Skill("skill", problems, where);

            return new Escape(ability.Value, skill, escape.Number("dc"));
        }

        // "repeat_save": {"ability": "wis", "worsens": "unconscious", "worsens_after": 2}
        static RepeatSave ReadRepeatSave(JsonElement raw, string where, List<string> problems)
        {
            if (!Record(raw, "repeat_save", where, problems, RepeatSaveKeys, out JsonElement save)) return null;

            Ability? ability = save.Ability("ability", problems, where);

            if (!ability.HasValue)
            {
                problems.Add($"{where}: a 'repeat_save' needs the 'ability' it is made with");
                return null;
            }

            return new RepeatSave(ability.Value)
            {
                EndsAfter = System.Math.Max(1, save.Number("ends_after", 1)),
                Worsens = save.Condition("worsens", problems, where),
                WorsensAfter = System.Math.Max(1, save.Number("worsens_after", 1)),
                OnlyUnseen = save.Flag("only_unseen"),
            };
        }

        // "gone": {"after_rounds": 10, "tag_rules": {"fiend": "only", ...}}
        static Gone ReadGone(JsonElement raw, string where, List<string> problems)
        {
            if (!Record(raw, "gone", where, problems, GoneKeys, out JsonElement gone)) return null;

            int rounds = gone.Number("after_rounds");
            IReadOnlyList<TagRule> rules = gone.TagRuleList("tag_rules", problems, where);

            if (rounds <= 0 || rules.Count == 0)
                problems.Add($"{where}: 'gone' needs 'after_rounds' and the 'tag_rules' of the ones that don't come back");

            if (rules.Any(r => r.Outcome != TagOutcome.Only))
                problems.Add($"{where}: a 'gone' record's tag rules are all 'only'");

            return new Gone(rounds, rules);
        }

        static bool Record(JsonElement raw, string name, string where, List<string> problems,
                           IReadOnlyList<string> keys, out JsonElement record)
        {
            record = default;

            if (!raw.Has(name)) return false;

            record = raw.GetProperty(name);

            if (record.ValueKind != JsonValueKind.Object)
            {
                problems.Add($"{where}: '{name}' is a record, {{...}}");
                return false;
            }

            Keyed.OnlyKnown(record, keys, $"{where} {name}", problems);
            return true;
        }
    }
}
