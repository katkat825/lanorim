using System.Collections.Generic;
using System.Text.Json;
using Core.Characters;
using Core.Dice;
using Core.Words;

namespace Content.Schema
{
    // THE ONE READER FOR WHAT A BOON IS. a sway effect, a class stance and an item's boon all carry
    // these keys on their own object, and all three hand that object here - so Bless's dice, Rage's
    // leans and a ring's flat bonus are written the same way in every file
    // (cc_task_dedupe-effects.md, Phase 3). nothing else reads these keys (a test holds that).
    public static class BoonSpecReader
    {
        // every key a boon takes, in the order docs/spell_effect_reference.md lists them
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "flat", "dice", "touches", "skill", "skill_choices", "ability", "ability_choices",
            "leans", "against", "unless_incapacitated", "once", "mark", "unarmored_base", "defenses",
            "immune", "truesight", "exposes", "if_seen", "not_vs_truesight", "wards_spell",
            "weapon_dice", "forbids", "action_or_bonus", "limited_action", "speed_change",
            "fly_speed", "death_ward", "eases_per_long_rest", "decoys", "size_step", "rewrite",
        };

        // the keys inside a mark and a rewrite
        public static readonly IReadOnlyList<string> MarkKeys = new[] { "dice", "damage_type" };

        public static readonly IReadOnlyList<string> RewriteKeys = new[] { "weapons", "die_tiers", "damage_type" };

        // the duration is the owner's: a sway effect's, a stance's, an item boon's
        public static BoonSpec Read(JsonElement raw, Duration duration, string where, List<string> problems)
        {
            Shapes(raw, where, problems);

            if (!EnumWords.TryParse(raw.Text("touches", "none"), out Sways touches))
                problems.Add($"{where}: '{raw.Text("touches")}' is not a list of rolls " +
                             $"({string.Join("|", EnumWords.Ids<Sways>())})");

            if (!EnumWords.TryParse(raw.Text("leans", "none"), out Leans leans))
                problems.Add($"{where}: '{raw.Text("leans")}' is not a list of leans " +
                             $"({string.Join("|", EnumWords.Ids<Leans>())})");

            // "speed_change": feet more or less (Ray of Frost's -10), or "double", "half" or "zero"
            SpeedChange speedChange = SpeedChange.None;
            int feet = 0;

            if (raw.Has("speed_change") && raw.GetProperty("speed_change").ValueKind == JsonValueKind.Number)
                feet = raw.Number("speed_change");
            else if (!EnumWords.TryParse(raw.Text("speed_change", "none"), out speedChange))
                problems.Add($"{where}: 'speed_change' is feet more or less, or 'double', 'half' or 'zero'");

            Forbid forbids = Forbid.None;

            foreach (string word in raw.Strings("forbids"))
            {
                if (EnumWords.TryParse(word, out Forbid ban)) forbids |= ban;
                else problems.Add($"{where}: '{word}' in 'forbids' is not one of " +
                                  string.Join(", ", EnumWords.Ids<Forbid>()));
            }

            SignedDice weaponDice = SignedDice.None;
            string weaponText = raw.Text("weapon_dice");

            if (!string.IsNullOrWhiteSpace(weaponText) &&
                !SignedDice.TryParse(weaponText, out weaponDice, out string bad))
                problems.Add($"{where}: 'weapon_dice' - {bad}");

            return new BoonSpec
            {
                Duration = duration,
                Flat = raw.Number("flat"),
                Dice = raw.Dice("dice", problems, where),
                Touches = touches,
                Skill = raw.Skill("skill", problems, where),
                SkillChoices = raw.SkillList("skill_choices", problems, where),
                Ability = raw.Ability("ability", problems, where),
                AbilityChoices = raw.AbilityList("ability_choices", problems, where),
                Leans = leans,
                Against = raw.Condition("against", problems, where),
                UnlessIncapacitated = raw.Flag("unless_incapacitated"),
                Once = raw.Flag("once"),
                Mark = ReadMark(raw, where, problems),
                UnarmoredBase = raw.Number("unarmored_base"),
                Defenses = raw.DefenseRecord("defenses", problems, where),
                ImmuneTo = raw.ConditionList("immune", problems, where),
                Truesight = raw.Flag("truesight"),
                Exposed = raw.Flag("exposes"),
                IfSeen = raw.Flag("if_seen"),
                NotVsTruesight = raw.Flag("not_vs_truesight"),
                WardsSpell = raw.Text("wards_spell") ?? "",
                WeaponDice = weaponDice,
                Forbids = forbids,
                ActionOrBonus = raw.Flag("action_or_bonus"),
                LimitedAction = raw.Flag("limited_action"),
                ExtraSpeed = feet,
                SpeedChange = speedChange,
                FlySpeed = raw.Number("fly_speed"),
                DeathWard = raw.Flag("death_ward"),
                EasesPerLongRest = raw.Number("eases_per_long_rest"),
                Decoys = raw.Number("decoys"),
                SizeStep = raw.Number("size_step"),
                Rewrite = ReadRewrite(raw, where, problems),
            };
        }

        // the keys whose value is a list or a record, and the old one-word "chosen"
        static void Shapes(JsonElement raw, string where, List<string> problems)
        {
            foreach (string list in new[] { "skill_choices", "ability_choices", "immune", "forbids" })
                if (raw.Has(list) && raw.GetProperty(list).ValueKind != JsonValueKind.Array)
                    problems.Add($"{where}: '{list}' is a list, [\"...\", \"...\"]");

            foreach (string record in new[] { "mark", "rewrite", "defenses" })
                if (raw.Has(record) && raw.GetProperty(record).ValueKind != JsonValueKind.Object)
                    problems.Add($"{where}: '{record}' is a record, {{...}}");

            if (raw.Text("skill") == "chosen")
                problems.Add($"{where}: a skill the caster picks is a list of the ones it may be, " +
                             "'skill_choices'");

            if (raw.Text("ability") == "chosen")
                problems.Add($"{where}: an ability the caster picks is a list of the ones it may be, " +
                             "'ability_choices'");
        }

        // "mark": {"dice": "1d6", "damage_type": "necrotic"}
        static Mark ReadMark(JsonElement raw, string where, List<string> problems)
        {
            if (!raw.Has("mark")) return null;

            JsonElement mark = raw.GetProperty("mark");
            DiceRoll dice = mark.Dice("dice", problems, where);
            DamageType type = mark.Damage("damage_type", problems, where);

            if (dice.IsNothing) problems.Add($"{where}: a mark with no 'dice'");

            if (type == DamageType.None)
                problems.Add($"{where}: a mark with no 'damage_type' - every hit is typed");

            return new Mark(dice, type);
        }

        // "rewrite": {"weapons": [...], "die_tiers": [...], "damage_type": "force"}
        static WeaponRewrite ReadRewrite(JsonElement raw, string where, List<string> problems)
        {
            if (!raw.Has("rewrite")) return null;

            JsonElement rewrite = raw.GetProperty("rewrite");
            var tiers = new List<DiceRoll>();

            foreach (string die in rewrite.Strings("die_tiers"))
            {
                if (DiceRoll.TryParse(die, out DiceRoll read, out string bad)) tiers.Add(read);
                else problems.Add($"{where}: 'die_tiers' - {bad}");
            }

            if (tiers.Count == 0) problems.Add($"{where}: a weapon rewrite with no 'die_tiers'");

            return new WeaponRewrite(rewrite.Strings("weapons"), null, default,
                                     rewrite.Damage("damage_type", problems, where), tiers);
        }

        // what a boon has to be, wherever it came from: a number that says which rolls it reaches,
        // and - where the owner has nothing else to do (a stance, an item's boon) - something
        public static void Check(BoonSpec spec, string where, List<string> problems, bool mustDoSomething)
        {
            bool numbers = spec.Flat != 0 || !spec.Dice.IsNothing;

            if (numbers && spec.Touches == Sways.None)
                problems.Add($"{where}: a boon's number that touches nothing - say which rolls it " +
                             "reaches with 'touches'");

            if (mustDoSomething && !spec.DoesSomething)
                problems.Add($"{where}: a boon of nothing - give it 'flat', 'dice', 'leans', 'mark', " +
                             "'forbids' or another of its keys");

            if (spec.Skill != Skill.None && spec.SkillChoices.Count > 0)
                problems.Add($"{where}: a boon names its 'skill' or lists 'skill_choices', not both");

            if (spec.Ability.HasValue && spec.AbilityChoices.Count > 0)
                problems.Add($"{where}: a boon names its 'ability' or lists 'ability_choices', not both");
        }
    }
}
