using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;

namespace Content.Spells
{
    // reads a spell file into Spells. the file says which primitives the spell is made of and
    // with what numbers - there is no per-spell code anywhere, and a spell this reader cannot
    // express is one that needs a new primitive or a bounded approximation, not a special case.
    public static class SpellReader
    {
        public static bool TryRead(string text, out IReadOnlyList<Spell> spells,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<Spell>();
            var trouble = new List<string>();

            spells = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                IReadOnlyList<JsonElement> entries =
                    root.ValueKind == JsonValueKind.Array
                        ? ToList(root)
                        : root.Items("spells");

                if (entries.Count == 0)
                    trouble.Add("no spells in it - the file is an array, or an object with a " +
                                "'spells' array");

                foreach (JsonElement entry in entries)
                {
                    Spell spell = ReadOne(entry, trouble);

                    if (spell != null) found.Add(spell);
                }
            }

            return trouble.Count == 0;
        }

        static IReadOnlyList<JsonElement> ToList(JsonElement array)
        {
            var list = new List<JsonElement>();

            foreach (JsonElement item in array.EnumerateArray()) list.Add(item);

            return list;
        }

        // one spell object on its own - a statblock's special action is one, inline
        public static Spell ReadEntry(JsonElement entry, List<string> problems) =>
            ReadOne(entry, problems ?? new List<string>());

        static Spell ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a spell id - lowercase a-z, 0-9 and underscore only");
                return null;
            }

            int level = entry.Number("level", -1);

            if (level < 0 || level > 9)
            {
                problems.Add($"{id}: level {level} - a spell is level 0 (a cantrip) to 9");
                return null;
            }

            if (!Schools.TryParse(entry.Text("school", "evocation"), out School school))
                problems.Add($"{id}: '{entry.Text("school")}' is not a school of magic");

            var effects = new List<SpellEffect>();

            foreach (JsonElement raw in entry.Items("effects"))
            {
                SpellEffect effect = ReadEffect(raw, id, level, problems);

                if (effect != null) effects.Add(effect);
            }

            if (effects.Count == 0)
                problems.Add($"{id}: no effects - a spell that does nothing is a reference card, " +
                             "and reference cards are not loaded here");

            bool concentration = entry.Flag("concentration");

            if (!Primitives.TryParse(entry.Text("casting_time", "action"), out CastingTime time))
                problems.Add($"{id}: '{entry.Text("casting_time")}' is not a casting time " +
                             "(action, bonus_action, reaction)");

            Trigger? trigger = null;
            string triggerId = entry.Text("trigger");

            if (!string.IsNullOrEmpty(triggerId))
            {
                if (Triggers.TryParse(triggerId, out Trigger parsed)) trigger = parsed;
                else
                    problems.Add($"{id}: '{triggerId}' is not a trigger - it is " +
                                 string.Join(", ", Triggers.All.Select(t => t.Id())));
            }

            // a reaction spell is cast in answer to exactly one kind of moment, and a spell cast on
            // a turn answers nothing - either half without the other is a spell the fight can
            // never offer, or offers for no reason
            if (time == CastingTime.Reaction && !trigger.HasValue)
                problems.Add($"{id}: a reaction spell has to say what it answers with 'trigger'");

            // SRD 5.2.1's smites are the one thing answered with a bonus action: right after
            // your own hit. every other trigger is a reaction's
            if (trigger == Trigger.Struck && time != CastingTime.BonusAction)
                problems.Add($"{id}: 'struck' is answered with a bonus action - 'casting_time': " +
                             "'bonus_action'");

            if (trigger.HasValue && trigger != Trigger.Struck && time != CastingTime.Reaction)
                problems.Add($"{id}: a 'trigger' on a spell that is not cast as a reaction");

            CastingTime? repeat = null;
            string repeatId = entry.Text("repeat");

            if (!string.IsNullOrEmpty(repeatId))
            {
                if (Primitives.TryParse(repeatId, out CastingTime again) &&
                    again != CastingTime.Reaction)
                    repeat = again;
                else
                    problems.Add($"{id}: 'repeat' is 'action' or 'bonus_action', not '{repeatId}'");
            }

            if (!Schools.TryParse(entry.Text("lasts", "instant"), out Duration lasts))
                problems.Add($"{id}: '{entry.Text("lasts")}' is not a duration");

            // a repeat is done while the spell is held, or while it lasts (Produce Flame), so it
            // needs something to hold and something to repeat - either half alone is a spell
            // that says it does more than it can
            if (repeat.HasValue && !concentration && lasts == Duration.Instant)
                problems.Add($"{id}: a spell that repeats has to be held - mark it concentration, " +
                             "or say how long it 'lasts'");

            if (repeat.HasValue != effects.Any(e => e.Repeats))
                problems.Add($"{id}: 'repeat' on the spell and 'repeats' on an effect come together");

            if (effects.Count > 0 && effects[0].Follows)
                problems.Add($"{id}: the first effect has nothing before it to follow");

            // an effect that acts through the zone needs a zone to act through, and a zone with
            // nothing acting through it is only a marker - which is allowed, it is what a zone was
            bool hasZone = effects.Any(e => e.Kind == Primitive.Zone);

            if (effects.Any(e => e.Reach == Reach.Zone && e.Kind != Primitive.Shift) && !hasZone)
                problems.Add($"{id}: an effect reaches 'zone' but the spell makes no zone");

            // one zone to a spell - or to each of its modes: Forcecage is a cage or a box
            if (effects.Where(e => e.Kind == Primitive.Zone).GroupBy(e => e.Mode).Any(g => g.Count() > 1))
                problems.Add($"{id}: one zone to a spell, or to each of its modes");

            // stopping a spell needs a spell to stop, and only the cast window has one in hand
            if (effects.Any(e => e.Kind == Primitive.Counter) && trigger != Trigger.Cast)
                problems.Add($"{id}: a counter effect only works on a reaction to a cast " +
                             "('casting_time': 'reaction', 'trigger': 'cast')");

            // the two have to agree or the sheet's "concentrating" light lies: a spell marked for
            // concentration must hold something, and something held must be marked
            foreach (SpellEffect effect in effects)
                if (effect.Duration == Duration.Concentration && !concentration)
                    problems.Add($"{id}: an effect lasts for concentration but the spell is not " +
                                 "marked 'concentration: true'");

            // modes: every effect that names one belongs to it, and a spell with modes has at
            // least two - a single mode is not a choice
            List<string> modes = effects.Select(e => e.Mode).Where(m => m.Length > 0)
                                        .Distinct().ToList();

            if (modes.Count == 1)
                problems.Add($"{id}: one 'mode' is not a choice - give it two or none");

            if (entry.Strings("shapes").Any(s => s != "line" && s != "ring"))
                problems.Add($"{id}: a wall's 'shapes' are 'line' and 'ring'");

            if (effects.Count(e => e.OnEnd) > 0 && !concentration &&
                effects.All(e => e.Duration == Duration.Instant))
                problems.Add($"{id}: an 'on_end' effect on a spell that never lasts");

            return new Spell(id, level, school, effects,
                             entry.Number("range"),
                             concentration,
                             entry.Flag("ritual"),
                             entry.Flag("approximated"),
                             entry.Strings("classes"),
                             time,
                             trigger,
                             repeat)
            {
                Curse = entry.Flag("curse"),
                RangeScales = entry.Flag("range_scales"),
                Lasts = lasts,
                Shapes = entry.Strings("shapes"),
                NotInSrd = entry.Flag("not_in_srd"),
                AnswersSpell = entry.Text("answers_spell") ?? "",
                OutOfCombat = entry.Flag("out_of_combat"),
                ConcentrationBelow = entry.Number("concentration_below"),
                EndsPrevious = entry.Flag("ends_previous"),
                MovesWhenDown = entry.Flag("moves_when_down"),
                ForceCreation = entry.Flag("force_creation"),
                DcAbility = entry.Ability("dc_ability", problems, id),
            };
        }

        // Enhance Ability's five: the abilities a chosen ability may be
        static List<Ability> AbilityList(JsonElement raw, string name, string where, List<string> problems)
        {
            var list = new List<Ability>();

            foreach (string word in raw.Strings(name))
            {
                if (Abilities.TryParse(word, out Ability read)) list.Add(read);
                else problems.Add($"{where}: '{word}' in '{name}' is not an ability");
            }

            return list;
        }

        // Gaseous Form's "Immunity to the Prone condition"
        static List<Condition> Immunities(JsonElement raw, string where, List<string> problems)
        {
            var immune = new List<Condition>();

            foreach (string word in raw.Strings("immune"))
            {
                if (Conditions.TryParse(word, out Condition read) && read != Condition.None) immune.Add(read);
                else problems.Add($"{where}: '{word}' in 'immune' is not a condition");
            }

            return immune;
        }

        static SpellEffect ReadEffect(JsonElement raw, string spellId, int level,
                                      List<string> problems)
        {
            string where = spellId;

            if (!Primitives.TryParse(raw.Text("primitive"), out Primitive primitive))
            {
                problems.Add($"{where}: '{raw.Text("primitive")}' is not a primitive - the closed " +
                             "list is in core/Magic/Primitive.cs");
                return null;
            }

            if (!Primitives.TryParse(raw.Text("reach", "creature"), out Reach reach))
                problems.Add($"{where}: '{raw.Text("reach")}' is not a reach");

            if (!Primitives.TryParse(raw.Text("on_save", "none"), out OnSave onSave))
                problems.Add($"{where}: '{raw.Text("on_save")}' is not a save outcome");

            if (!Primitives.TryParse(raw.Text("touches", "none"), out Sways touches))
                problems.Add($"{where}: '{raw.Text("touches")}' is not a list of swayed rolls " +
                             "(attacks|saves|checks|damage|armor_class)");

            if (!Schools.TryParse(raw.Text("duration", "instant"), out Duration duration))
                problems.Add($"{where}: '{raw.Text("duration")}' is not a duration");

            Ability? save = raw.Ability("save", problems, where);
            bool attackRoll = raw.Flag("attack_roll");

            if (attackRoll && save.HasValue)
                problems.Add($"{where}: an effect rolls to hit or calls for a save, never both");

            if (onSave != OnSave.None && !save.HasValue)
                problems.Add($"{where}: 'on_save' is set but there is no save to make");

            if (!Primitives.TryParse(raw.Text("leans", "none"), out Leans leans))
                problems.Add($"{where}: '{raw.Text("leans")}' is not a list of leans " +
                             "(advantage_on_attacks|disadvantage_on_attacks|advantage_against|" +
                             "disadvantage_against|advantage_on_checks|disadvantage_on_checks)");

            if (!Primitives.TryParse(raw.Text("until"), out Until until))
                problems.Add($"{where}: 'until' is 'bearer' or 'caster', not '{raw.Text("until")}'");

            // "chosen" is the caster's pick at the moment of casting, so it is not a skill or an
            // ability the reader can look up - it is read off before they are
            bool chosenSkill = raw.Text("skill") == Chosen;
            bool chosenAbility = raw.Text("ability") == Chosen;

            Skill skill = chosenSkill ? Skill.None : raw.Skill("skill", problems, where);

            var resists = new List<DamageType>();

            foreach (string type in (raw.Text("resists") ?? "").Split('|',
                                                                  StringSplitOptions.RemoveEmptyEntries))
            {
                if (DamageTypes.TryParse(type.Trim(), out DamageType read) && read != DamageType.None)
                    resists.Add(read);
                else
                    problems.Add($"{where}: '{type}' in 'resists' is not a damage type");
            }

            Obscurement obscures = Obscurement.None;

            switch (raw.Text("obscures", "none"))
            {
                case "none": break;
                case "light": obscures = Obscurement.Light; break;
                case "heavy": obscures = Obscurement.Heavy; break;
                default:
                    problems.Add($"{where}: 'obscures' is 'light' or 'heavy', not " +
                                 $"'{raw.Text("obscures")}'");
                    break;
            }

            DamageEnds endsOnDamage = DamageEnds.None;

            switch (raw.Text("ends_on_damage", "none"))
            {
                case "none": break;
                case "any": endsOnDamage = DamageEnds.Any; break;
                case "caster_side": endsOnDamage = DamageEnds.CasterSide; break;
                default:
                    problems.Add($"{where}: 'ends_on_damage' is 'any' or 'caster_side'");
                    break;
            }

            SpeedChange speedChange = SpeedChange.None;

            switch (raw.Text("speed_change", "none"))
            {
                case "none": break;
                case "double": speedChange = SpeedChange.Double; break;
                case "half": speedChange = SpeedChange.Half; break;
                case "zero": speedChange = SpeedChange.Zero; break;
                default:
                    problems.Add($"{where}: 'speed_change' is 'double', 'half' or 'zero'");
                    break;
            }

            // "chosen" damage: the caster picks from 'damage_choices' at the cast
            bool chosenType = raw.Text("damage_type") == Chosen;
            var choices = new List<DamageType>();

            foreach (string type in raw.Strings("damage_choices"))
            {
                if (DamageTypes.TryParse(type, out DamageType read) && read != DamageType.None)
                    choices.Add(read);
                else
                    problems.Add($"{where}: '{type}' in 'damage_choices' is not a damage type");
            }

            if (chosenType != (choices.Count > 0))
                problems.Add($"{where}: a 'chosen' damage type and 'damage_choices' come together");

            var tiers = new List<DiceRoll>();

            foreach (string die in raw.Strings("rewrite_die_tiers"))
            {
                if (DiceRoll.TryParse(die, out DiceRoll read, out string bad)) tiers.Add(read);
                else problems.Add($"{where}: 'rewrite_die_tiers' - {bad}");
            }

            Command command = Command.None;

            if (!string.IsNullOrEmpty(raw.Text("command")) &&
                !Enum.TryParse(raw.Text("command"), true, out command))
                problems.Add($"{where}: '{raw.Text("command")}' is not approach, drop, flee, " +
                             "grovel or halt");

            Size? maxSize = null;

            if (!string.IsNullOrEmpty(raw.Text("max_size")))
            {
                if (Sizes.TryParse(raw.Text("max_size"), out Size read)) maxSize = read;
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

            if (!ZonePulses.TryParse(raw.Text("pulses", "none"), out Pulses pulses))
                problems.Add($"{where}: '{raw.Text("pulses")}' is not a list of pulses " +
                             "(appear|enter|start_turn|end_turn|each_square)");

            var effect = new SpellEffect(
                primitive,
                reach,
                raw.Dice("amount", problems, where),
                raw.Dice("per_extra_level", problems, where),
                chosenType ? DamageType.None : raw.Damage("damage_type", problems, where),
                raw.Condition("condition", problems, where),
                save,
                onSave,
                attackRoll,
                duration,
                raw.Number("radius"),
                raw.Number("targets", 1),
                raw.Number("extra_targets_per_level"),
                raw.Number("sway"),
                raw.Dice("sway_dice", problems, where),
                touches,
                skill,
                raw.Flag("cantrip_scaling"),
                raw.Text("note"),
                raw.Number("length"),
                raw.Number("width"))
            {
                AddsModifier = raw.Flag("add_modifier"),
                Leans = leans,
                Once = raw.Flag("once"),
                EndsOnAttack = raw.Flag("ends_on_attack"),
                Until = until,
                Mark = raw.Dice("mark", problems, where),
                ChosenSkill = chosenSkill,
                ChosenAbility = chosenAbility,
                UnarmoredBase = raw.Number("unarmored_base"),
                Beams = raw.Flag("cantrip_beams"),
                SameSave = raw.Flag("same_save"),
                Points = Math.Max(1, raw.Number("points", 1)),
                SlaysAtOrBelow = raw.Number("slays_at_or_below"),
                Follows = raw.Flag("follows"),
                Repeats = raw.Flag("repeats"),
                Pulses = pulses,
                Rough = raw.Flag("rough"),
                SparesAllies = raw.Flag("spares_allies"),
                Escape = raw.Ability("escape", problems, where),
                RepeatSave = raw.Flag("repeat_save"),
                EscapeSkill = string.IsNullOrEmpty(raw.Text("escape_skill"))
                    ? Skill.None
                    : raw.Skill("escape_skill", problems, where),
                EscapeDc = raw.Number("escape_dc"),
                FlySpeed = raw.Number("fly_speed"),
                NoAttacks = raw.Flag("no_attacks"),
                NoCasting = raw.Flag("no_casting"),
                Immune = Immunities(raw, where, problems),
                UpTo = raw.Number("up_to"),
                Banishes = raw.Flag("banishes"),
                GoneAfterRounds = raw.Number("gone_after_rounds"),
                GoneTags = raw.Strings("gone_tags"),
                Ground = raw.Flag("ground"),
                EndsAtZero = raw.Flag("ends_at_zero"),
                EndsOnAct = raw.Flag("ends_on_act"),
                WardsSpell = raw.Text("wards_spell") ?? "",
                SparesCaster = raw.Flag("spares_caster"),
                DimRadius = raw.Number("dim_radius"),
                IfSeen = raw.Flag("if_seen"),
                NotVsTruesight = raw.Flag("not_vs_truesight"),
                NearZone = raw.Number("near_zone"),
                RevertsShape = raw.Flag("reverts_shape"),
                DisadvantageTags = raw.Strings("disadvantage_tags"),
                AutoFailTags = raw.Strings("auto_fail_tags"),
                AbilityChoices = AbilityList(raw, "ability_choices", where, problems),
                WhileInZone = raw.Flag("while_in_zone"),
                NearFirst = raw.Number("near_first"),
                Dust = raw.Flag("dust"),
                EndsForce = raw.Flag("ends_force"),
                RaisesAs = raw.Text("raises_as") ?? "",
                RaisesTag = raw.Text("raises_tag") ?? "",
                Passenger = raw.Flag("passenger"),
                Unseen = raw.Flag("unseen"),
                PermanentAfterRounds = raw.Number("permanent_after_rounds"),
                Point = raw.Flag("point"),
                LeansOn = raw.Ability("leans_on", problems, where),
                Resists = resists,
                Speed = raw.Number("speed"),
                NoOpportunityAttacks = raw.Flag("no_opportunity_attacks"),
                Exposes = raw.Flag("exposes"),
                Obscures = obscures,
                MagicalDarkness = raw.Flag("magical_darkness"),
                DispelsDarkness = raw.Flag("dispels_darkness"),
                EndsOnDamage = endsOnDamage,
                SaveOnDamage = raw.Flag("save_on_damage"),
                Shakeable = raw.Flag("shakeable"),
                Worsens = raw.Condition("worsens", problems, where),
                WorsensAfter = Math.Max(1, raw.Number("worsens_after", 1)),
                EndsAfter = Math.Max(1, raw.Number("ends_after", 1)),
                OnlyTags = raw.Strings("only_tags"),
                ExceptTags = raw.Strings("except_tags"),
                ExtraAgainst = raw.Strings("extra_against"),
                ExtraAmount = raw.Dice("extra_amount", problems, where),
                AdvantageIfFought = raw.Flag("advantage_if_fought"),
                OnlyAtOrBelow = raw.Number("only_at_or_below"),
                OnlyAbove = raw.Number("only_above"),
                SaveIfUnwilling = raw.Flag("save_if_unwilling"),
                Mode = raw.Text("mode") ?? "",
                DamageChoices = choices,
                Delayed = raw.Flag("delayed"),
                Recurs = raw.Flag("recurs"),
                EndSave = raw.Ability("end_save", problems, where),
                BreaksConcentration = raw.Flag("breaks_concentration"),
                SpeedChange = speedChange,
                Truesight = raw.Flag("truesight"),
                RaisesMaximum = raw.Flag("raises_maximum"),
                DeathWard = raw.Flag("death_ward"),
                NoReactions = raw.Flag("no_reactions"),
                ActionOrBonus = raw.Flag("action_or_bonus"),
                NoActions = raw.Flag("no_actions"),
                LimitedAction = raw.Flag("limited_action"),
                WeaponDice = raw.Dice("weapon_dice", problems, where),
                WeaponDiceLess = raw.Flag("weapon_dice_less"),
                Decoys = raw.Number("decoys"),
                EasesPerLongRest = raw.Number("eases_per_long_rest"),
                OnEnd = raw.Flag("on_end"),
                Curses = raw.Flag("curses"),
                Weapons = raw.Strings("weapons"),
                RewriteDie = raw.Dice("rewrite_die", problems, where),
                RewriteDieTiers = tiers,
                RewriteDamageType = raw.Damage("rewrite_damage_type", problems, where),
                BlocksSpellsUpTo = raw.Number("blocks_spells_up_to"),
                NeedsSight = raw.Flag("needs_sight"),
                Revives = raw.Flag("revives"),
                Pinned = raw.Flag("pinned"),
                AutoSaveTags = raw.Strings("auto_save_tags"),
                RadiusPerExtraLevel = raw.Number("radius_per_extra_level"),
                Drifts = raw.Number("drifts"),
                SizeStep = raw.Number("size_step"),
                RestoresAbilities = raw.Flag("restores_abilities"),
                RepeatOnly = raw.Flag("repeat_only"),
                Leaps = raw.Number("leaps"),
                ExtraTiers = extraTiers,
                OnCaster = raw.Flag("on_caster"),
                WithinZone = raw.Flag("within_zone"),
                BonusIf = raw.Text("bonus_if") ?? "",
                BonusAmount = raw.Dice("bonus_amount", problems, where),
                Rams = raw.Flag("rams"),
                Unoccupied = raw.Flag("unoccupied"),
                Command = command,
                Flees = raw.Flag("flees"),
                Disarms = raw.Flag("disarms"),
                RepeatUnseen = raw.Flag("repeat_unseen"),
                Contest = raw.Ability("contest", problems, where),
                MaxSize = maxSize,
                Switches = raw.Flag("switches"),
                Item = raw.Text("item") ?? "",
                Count = raw.Number("count"),
                WhileInside = raw.Flag("while_inside"),
                AlliesOnly = raw.Flag("allies_only"),
                RingSize = raw.Number("ring_size"),
                Beside = raw.Number("beside"),
                CoreOnly = raw.Flag("core_only"),
                Cover = raw.Number("cover"),
                Encloses = raw.Text("encloses") switch
                {
                    "bars" => Core.Space.Edge.Bars,
                    "solid" => Core.Space.Edge.Wall,
                    _ => Core.Space.Edge.None,
                },
                Teleports = raw.Flag("teleports"),
                EachTime = raw.Flag("each_time"),
                ReactionFlee = raw.Flag("reaction_flee"),
                Push = raw.Number("push"),
            };

            if (!string.IsNullOrEmpty(raw.Text("ability")) && !chosenAbility)
                problems.Add($"{where}: 'ability' on an effect only ever says '{Chosen}'");

            Check(effect, spellId, level, problems);

            return effect;
        }

        const string Chosen = "chosen";

        // the per-primitive rules a data file has to obey. this is the only place that knows what
        // each primitive needs, and it is why a typo in a spell file is a load error with a name
        // on it rather than a spell that quietly does nothing.
        static void Check(SpellEffect effect, string spellId, int level, List<string> problems)
        {
            switch (effect.Kind)
            {
                case Primitive.Damage:
                    if (effect.Amount.IsNothing)
                        problems.Add($"{spellId}: a damage effect with no 'amount'");

                    if (effect.DamageType == DamageType.None && !effect.ChosenDamageType)
                        problems.Add($"{spellId}: damage with no 'damage_type' - every hit is " +
                                     "typed, because resistance is read off the type");
                    break;

                case Primitive.Heal:
                case Primitive.Ward:
                    if (effect.Amount.IsNothing)
                        problems.Add($"{spellId}: a {effect.Kind.Id()} effect with no 'amount'");
                    break;

                case Primitive.Relieve when effect.RestoresAbilities:
                    break;

                case Primitive.Afflict:
                case Primitive.Relieve:
                    if (effect.Condition == Condition.None)
                        problems.Add($"{spellId}: a {effect.Kind.Id()} effect with no 'condition'");
                    break;

                case Primitive.Sway:
                {
                    // a sway says how far it tips a roll, or which way, or what it adds when the
                    // caster hits, or what armor it stands in for - and it has to say one of them
                    bool numbers = effect.Sway != 0 || !effect.SwayDice.IsNothing;
                    bool something = numbers || effect.Leans != Leans.None ||
                                     !effect.Mark.IsNothing || effect.UnarmoredBase > 0 ||
                                     effect.Resists.Count > 0 || effect.Speed != 0 ||
                                     effect.NoOpportunityAttacks || effect.Exposes ||
                                     effect.SpeedChange != SpeedChange.None || effect.Truesight ||
                                     effect.RaisesMaximum || effect.DeathWard ||
                                     effect.NoReactions || effect.ActionOrBonus ||
                                     effect.NoActions || effect.LimitedAction ||
                                     !effect.WeaponDice.IsNothing || effect.Decoys > 0 ||
                                     effect.Weapons.Count > 0 || effect.SizeStep != 0 ||
                                     effect.FlySpeed > 0 || effect.NoAttacks || effect.NoCasting ||
                                     effect.Immune.Count > 0 || effect.Banishes ||
                                     effect.WardsSpell.Length > 0;

                    if (numbers && effect.Touches == Sways.None)
                        problems.Add($"{spellId}: a sway that touches nothing - say which rolls " +
                                     "it reaches with 'touches'");

                    if (!something)
                        problems.Add($"{spellId}: a sway of nothing - give it 'sway', " +
                                     "'sway_dice', 'leans', 'mark' or 'unarmored_base'");

                    if (!effect.Mark.IsNothing && effect.DamageType == DamageType.None)
                        problems.Add($"{spellId}: a mark with no 'damage_type'");
                    break;
                }

                case Primitive.Zone:
                case Primitive.Illuminate:
                    // a square zone is sized by its side; everything else by its radius
                    // a point is one square on purpose: Spiritual Weapon's force
                    if (effect.Reach != Reach.Square && effect.Reach != Reach.Wall && effect.Radius <= 0 &&
                        !effect.Point)
                        problems.Add($"{spellId}: a {effect.Kind.Id()} with no 'radius'");
                    break;
            }

            if ((effect.Reach == Reach.Burst || effect.Reach == Reach.Around) && effect.Radius <= 0)
                problems.Add($"{spellId}: a burst needs a 'radius' in squares");

            // the shapes that come out of the caster carry their own size, and none of them
            // borrows a radius: a 100-foot line is not a 100-foot burst
            if (effect.Reach.IsDirected() && effect.Length <= 0)
                problems.Add($"{spellId}: a {effect.Reach.Id()} needs a 'length' in squares");

            if (effect.Reach == Reach.Line && effect.Width <= 0)
                problems.Add($"{spellId}: a line needs a 'width' in squares - SRD lines are " +
                             "usually 1, five feet");

            if (effect.Reach != Reach.Line && effect.Width > 0)
                problems.Add($"{spellId}: 'width' only means something on a line - a cone's " +
                             "width is its distance from you, a cube's is its length");

            if (effect.Reach.IsDirected() && effect.Radius > 0)
                problems.Add($"{spellId}: a {effect.Reach.Id()} with a 'radius' - its size is " +
                             "'length'");

            // the fields that only mean something on one primitive, refused anywhere else so a
            // typo'd effect does not load as a spell that quietly ignores half its data
            if (effect.AddsModifier && effect.Kind != Primitive.Damage &&
                effect.Kind != Primitive.Heal)
                problems.Add($"{spellId}: 'add_modifier' on a {effect.Kind.Id()} - it adds to " +
                             "damage or healing");

            if ((effect.Leans != Leans.None || effect.Once || effect.EndsOnAttack ||
                 !effect.Mark.IsNothing || effect.UnarmoredBase > 0 || effect.ChosenAbility ||
                 effect.Resists.Count > 0 || effect.Speed != 0 || effect.NoOpportunityAttacks ||
                 effect.Exposes || effect.LeansOn.HasValue) &&
                effect.Kind != Primitive.Sway)
                problems.Add($"{spellId}: a lean, a mark, 'once', 'ends_on_attack', " +
                             "'unarmored_base' or a chosen ability belong on a sway");

            if (effect.Until == Until.Caster && effect.Duration != Duration.NextTurn &&
                effect.Duration != Duration.NextTurnEnd)
                problems.Add($"{spellId}: 'until' only counts a next_turn or next_turn_end " +
                             "duration");

            if (effect.Reach == Reach.Zone && effect.Pulses == Pulses.None &&
                effect.Kind != Primitive.Shift && !effect.WhileInside)
                problems.Add($"{spellId}: an effect that reaches 'zone' has to say when, with " +
                             "'pulses'");

            if (effect.Pulses != Pulses.None && effect.Reach != Reach.Zone)
                problems.Add($"{spellId}: 'pulses' on an effect that does not reach 'zone'");

            if (effect.Rough && effect.Kind != Primitive.Zone)
                problems.Add($"{spellId}: 'rough' belongs on the zone");

            if (effect.SparesAllies && effect.Kind != Primitive.Zone && !effect.Reach.IsArea())
                problems.Add($"{spellId}: 'spares_allies' is for a zone or an area");

            if ((effect.RadiusPerExtraLevel > 0 || effect.Drifts > 0) &&
                effect.Kind != Primitive.Zone)
                problems.Add($"{spellId}: 'radius_per_extra_level' and 'drifts' belong on the zone");

            if (effect.SizeStep != 0 && effect.Kind != Primitive.Sway)
                problems.Add($"{spellId}: 'size_step' belongs on a sway");

            if (effect.RepeatOnly && !effect.Repeats)
                problems.Add($"{spellId}: 'repeat_only' is a kind of 'repeats'");

            if (effect.Leaps > 0 && (effect.Kind != Primitive.Damage || !effect.AttackRoll))
                problems.Add($"{spellId}: 'leaps' is for damage with an attack roll");

            if ((effect.OnCaster || effect.Unoccupied) && effect.Kind != Primitive.Zone)
                problems.Add($"{spellId}: 'on_caster' and 'unoccupied' belong on the zone");

            if (effect.Rams && !(effect.Kind == Primitive.Shift && effect.Reach == Reach.Zone))
                problems.Add($"{spellId}: 'rams' is how a zone is moved - a shift that reaches it");

            if (!string.IsNullOrEmpty(effect.BonusIf) == effect.BonusAmount.IsNothing)
                problems.Add($"{spellId}: 'bonus_if' and 'bonus_amount' come together");

            if ((effect.Command != Command.None) != (effect.Kind == Primitive.Direct))
                problems.Add($"{spellId}: a direct says its 'command', and only a direct has one");

            if ((effect.Flees || effect.Disarms) && effect.Kind != Primitive.Afflict)
                problems.Add($"{spellId}: 'flees' and 'disarms' go with a condition");

            if (effect.RepeatUnseen && !effect.RepeatSave)
                problems.Add($"{spellId}: 'repeat_unseen' narrows a 'repeat_save'");

            if (effect.Contest.HasValue && effect.Kind != Primitive.Disarm)
                problems.Add($"{spellId}: 'contest' is for a disarm");

            if (effect.Reach == Reach.Wall && effect.Kind != Primitive.Zone)
                problems.Add($"{spellId}: only a zone is put down as a wall");

            if ((effect.RingSize > 0 || effect.Beside > 0 || effect.Cover > 0 ||
                 effect.Encloses != Core.Space.Edge.None) && effect.Kind != Primitive.Zone)
                problems.Add($"{spellId}: 'ring_size', 'beside', 'cover' and 'encloses' belong on the zone");

            if (effect.CoreOnly && effect.Reach != Reach.Zone)
                problems.Add($"{spellId}: 'core_only' narrows what reaches the zone");

            if ((effect.Teleports || effect.Push > 0) && effect.Kind != Primitive.Shift)
                problems.Add($"{spellId}: 'teleports' and 'push' are shifts");

            if ((effect.Kind == Primitive.Conjure) != (effect.Item.Length > 0))
                problems.Add($"{spellId}: a conjure names its 'item', and only a conjure does");

            if (effect.WhileInside && (effect.Kind != Primitive.Sway || effect.Reach != Reach.Zone))
                problems.Add($"{spellId}: 'while_inside' is a sway that reaches the zone");

            if (effect.AlliesOnly && effect.Kind != Primitive.Zone)
                problems.Add($"{spellId}: 'allies_only' belongs on the zone");

            if (effect.Kind == Primitive.Strike && effect.Reach != Reach.Creature)
                problems.Add($"{spellId}: a strike is one attack at one creature");

            if (effect.Revives && effect.Kind != Primitive.Heal)
                problems.Add($"{spellId}: 'revives' is a heal that works on the dead");

            if (effect.Pinned && effect.Kind != Primitive.Afflict)
                problems.Add($"{spellId}: 'pinned' holds a condition - it goes on an afflict");

            if (effect.OnSave == OnSave.OnSuccess && effect.Kind == Primitive.Damage)
                problems.Add($"{spellId}: damage that lands only on a successful save - did you " +
                             "mean 'half'?");

            if (effect.Kind == Primitive.Zone && effect.Reach == Reach.Square && effect.Length <= 0)
                problems.Add($"{spellId}: a square zone needs a 'length'");

            if (effect.Reach == Reach.Square && effect.Length <= 0)
                problems.Add($"{spellId}: a square needs a 'length' in squares");

            if (effect.Banishes && effect.Kind != Primitive.Sway)
                problems.Add($"{spellId}: 'banishes' rides on a sway");

            if (effect.Escape.HasValue && effect.Kind != Primitive.Afflict &&
                !(effect.Kind == Primitive.Sway && effect.Banishes))
                problems.Add($"{spellId}: 'escape' is for a condition a creature can break out of");

            // Slow: a sway can be saved against again at the end of each turn, like a condition
            if (effect.RepeatSave && (effect.Kind != Primitive.Afflict && effect.Kind != Primitive.Sway ||
                                      !effect.Save.HasValue))
                problems.Add($"{spellId}: 'repeat_save' needs an afflict or a sway with a save to repeat");

            if (effect.SlaysAtOrBelow > 0 && effect.Kind != Primitive.Damage)
                problems.Add($"{spellId}: 'slays_at_or_below' on something that is not damage");

            if (effect.Points > 1 && effect.Reach != Reach.Burst)
                problems.Add($"{spellId}: 'points' is how many bursts - it needs 'reach': 'burst'");

            if (effect.SameSave && !effect.Save.HasValue)
                problems.Add($"{spellId}: 'same_save' with no save to share");

            if (effect.Beams && level != 0)
                problems.Add($"{spellId}: 'cantrip_beams' on a level {level} spell");

            if (effect.Beams && effect.CantripScaling)
                problems.Add($"{spellId}: a cantrip grows by beams or by dice, not both");

            if (effect.CantripScaling && level != 0)
                problems.Add($"{spellId}: 'cantrip_scaling' on a level {level} spell - only " +
                             "cantrips scale with the caster's level");

            if (!effect.PerExtraLevel.IsNothing && level == 0)
                problems.Add($"{spellId}: a cantrip cannot be upcast; it scales with your level");

            // the 2026-09-24 fields, each refused where it would mean nothing
            bool afflict = effect.Kind == Primitive.Afflict;
            bool sway = effect.Kind == Primitive.Sway;
            bool zone = effect.Kind == Primitive.Zone;

            if ((effect.Obscures != Obscurement.None || effect.MagicalDarkness ||
                 effect.BlocksSpellsUpTo > 0) && !zone)
                problems.Add($"{spellId}: 'obscures', 'magical_darkness' and " +
                             "'blocks_spells_up_to' belong on the zone");

            if (effect.MagicalDarkness && effect.Obscures != Obscurement.Heavy)
                problems.Add($"{spellId}: magical darkness is heavily obscured - 'obscures': 'heavy'");

            if ((effect.EndsOnDamage != DamageEnds.None || effect.Shakeable ||
                 effect.Worsens != Condition.None) && !afflict)
                problems.Add($"{spellId}: 'ends_on_damage', 'shakeable' and 'worsens' are for " +
                             "a condition");

            if ((effect.SaveOnDamage || effect.Worsens != Condition.None || effect.EndsAfter > 1) &&
                !effect.RepeatSave)
                problems.Add($"{spellId}: 'save_on_damage', 'worsens' and 'ends_after' need a " +
                             "'repeat_save'");

            if ((effect.Delayed || effect.Recurs) && effect.Kind != Primitive.Damage)
                problems.Add($"{spellId}: 'delayed' and 'recurs' are for damage");

            if (effect.Delayed && effect.Recurs)
                problems.Add($"{spellId}: damage is 'delayed' (once, later) or 'recurs' (each " +
                             "turn), not both");

            if (effect.EndSave.HasValue && !effect.Recurs && !afflict)
                problems.Add($"{spellId}: 'end_save' ends a recurring damage or a condition");

            if (effect.Recurs && effect.Duration == Duration.Instant)
                problems.Add($"{spellId}: recurring damage needs a 'duration'");

            if ((effect.SpeedChange != SpeedChange.None || effect.Truesight ||
                 effect.RaisesMaximum || effect.DeathWard || effect.NoReactions ||
                 effect.ActionOrBonus || effect.NoActions || effect.LimitedAction ||
                 !effect.WeaponDice.IsNothing || effect.Decoys > 0 ||
                 effect.EasesPerLongRest > 0 || effect.Weapons.Count > 0) && !sway)
                problems.Add($"{spellId}: a speed change, truesight, a raised maximum, a death " +
                             "ward, an action limit, weapon dice, decoys, an easing penalty or a " +
                             "weapon rewrite belong on a sway");

            if (effect.RaisesMaximum && effect.Amount.IsNothing)
                problems.Add($"{spellId}: 'raises_maximum' by how much? give it an 'amount'");

            if (effect.Weapons.Count > 0 && effect.RewriteDie.IsNothing &&
                effect.RewriteDieTiers.Count == 0)
                problems.Add($"{spellId}: a weapon rewrite with no die");

            if (!effect.ExtraAmount.IsNothing != (effect.ExtraAgainst.Count > 0))
                problems.Add($"{spellId}: 'extra_amount' and 'extra_against' come together");

            if (effect.Curses && effect.Kind != Primitive.Dispel)
                problems.Add($"{spellId}: 'curses' narrows a dispel");

            if (effect.Kind == Primitive.Stabilize && effect.Reach != Reach.Creature)
                problems.Add($"{spellId}: stabilize is for one creature");
        }
    }
}
