using System.Collections.Generic;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Words;

namespace Content.Spells
{
    // AN EFFECT'S WORDS: every enum-valued setting (aim, save, duration, lands, affects, ...), read
    // and checked together, so SpellReader.ReadEffect is the order of the steps and not their detail
    // (cc_task_godfiles-dupes-efficiency.md #14)
    readonly record struct EffectWords(AimKind Aim, OnSave OnSave, Duration Duration, Ability? Save, bool AttackRoll,
                                       Lands Lands, CantripGrowth Growth, Affects Affects, Obscurement Obscures,
                                       Command Command, Size? MaxSize, List<DiceRoll> ExtraTiers, Pulses Pulses)
    {
        public static EffectWords Read(JsonElement raw, string where, List<string> problems)
        {
                if (!EnumWords.TryParse(raw.Text("aim", "creature"), out AimKind aim))
                    problems.Add($"{where}: '{raw.Text("aim")}' is not an aim");

                if (!EnumWords.TryParse(raw.Text("on_save", "none"), out OnSave onSave))
                    problems.Add($"{where}: '{raw.Text("on_save")}' is not a save outcome");

                if (!EnumWords.TryParse(raw.Text("duration", "instant"), out Duration duration))
                    problems.Add($"{where}: '{raw.Text("duration")}' is not a duration");

                Ability? save = raw.Ability("save", problems, where);
                bool attackRoll = raw.Flag("attack_roll");

                if (attackRoll && save.HasValue)
                    problems.Add($"{where}: an effect rolls to hit or calls for a save, never both");

                if (onSave != OnSave.None && !save.HasValue)
                    problems.Add($"{where}: 'on_save' is set but there is no save to make");

                if (raw.Text("damage_type") == "chosen")
                    problems.Add($"{where}: a damage type the caster picks is the list it may be, " +
                                 "'damage_choices', with no 'damage_type'");

                if (!EnumWords.TryParse(raw.Text("lands", "now"), out Lands lands))
                    problems.Add($"{where}: 'lands' is now, next_turn_end, each_turn, on_end, " +
                                 "now_and_on_repeat or on_repeat");

                CantripGrowth growth = SpellReader.Word<CantripGrowth>(raw, "cantrip_growth", "none", problems,
                                                           $"{where}: 'cantrip_growth' is 'dice' or 'beams'");

                Affects affects = SpellReader.Word<Affects>(raw, "affects", "all", problems,
                                                $"{where}: 'affects' is all, not_caster, foes or allies");

                Obscurement obscures = SpellReader.Word<Obscurement>(raw, "obscures", "none", problems,
                                                         $"{where}: 'obscures' is light, heavy or magical_darkness");

                Command command = Command.None;

                if (!string.IsNullOrEmpty(raw.Text("command")) &&
                    !EnumWords.TryParse(raw.Text("command"), out command))
                    problems.Add($"{where}: '{raw.Text("command")}' is not approach, drop, flee, " +
                                 "grovel or halt");

                Size? maxSize = null;

                if (!string.IsNullOrEmpty(raw.Text("max_size")))
                {
                    if (EnumWords.TryParse(raw.Text("max_size"), out Size read)) maxSize = read;
                    else problems.Add($"{where}: '{raw.Text("max_size")}' is not a size");
                }

                // by cantrip tier; an empty string is "nothing yet"
                var extraTiers = new List<DiceRoll>();

                foreach (string die in raw.Strings("extra_tiers"))
                {
                    if (string.IsNullOrWhiteSpace(die)) extraTiers.Add(DiceRoll.None);
                    else if (DiceRoll.TryParse(die, out DiceRoll read, out string bad)) extraTiers.Add(read);
                    else problems.Add($"{where}: 'extra_tiers' - {bad}");
                }

                if (!EnumWords.TryParse(raw.Text("pulses", "none"), out Pulses pulses))
                    problems.Add($"{where}: '{raw.Text("pulses")}' is not a list of pulses " +
                                 "(appear|enter|start_turn|end_turn|each_square)");

            return new EffectWords(aim, onSave, duration, save, attackRoll, lands, growth, affects, obscures, command,
                                   maxSize, extraTiers, pulses);
        }
    }
}
