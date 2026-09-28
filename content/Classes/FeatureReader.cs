using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Magic;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Words;

namespace Content.Classes
{
    // shared by classes and species: a feature is a feature wherever it came from
    public static class FeatureReader
    {
        // every key a feature takes. its boons are a list, each entry in the BoonSpec vocabulary
        // (BoonSpecReader): a stance's one boon, or what the feature gives for good
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "id", "trait", "level", "note", "tags", "amount", "per_level", "per_levels",
            "amount_by_level", "damage_type", "condition", "ability", "when", "grants", "boons",
            "flat_by_level", "flat_ability", "uses", "uses_by_level", "recharge", "spends",
            "count", "duration", "skills", "saves", "progression", "manoeuvres", "once_per_turn",
            "while_stances", "needs_weapon", "forgoes", "crit_on", "aura_ability", "armored_ac", "use_time",
            "reaction", "spells", "free_casts", "cantrips_by_level", "known_by_level", "skill_picks",
            "expertise_from", "dc", "dc_step", "stays_up_at", "speed_change", "only_bloodied",
            "cap_half", "max_hp_per_level", "not_in_heavy_armor", "spell", "spell_abilities",
        };

        // inside 'stays_up_at'
        public static readonly IReadOnlyList<string> StaysUpAtKeys = new[] { "hit_points", "per_level" };

        public static Feature ReadOne(JsonElement raw, string owner, List<string> problems) =>
            ReadOne(raw, owner, problems, SharedFeatures.Srd);

        public static Feature ReadOne(JsonElement raw, string owner, List<string> problems, SharedFeatures shared)
        {
            string id = raw.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"{owner}: '{id}' is not a feature id");
                return null;
            }

            if (!SharedFeatures.Names(raw)) return Read(raw, id, raw.Number("level", 1), owner, problems);

            // no trait of its own: a feature written once in srd/features/shared.json, at this level
            Keyed.OnlyKnown(raw, SharedFeatures.NamingKeys, $"{owner}/{id}", problems);

            if (shared.TryFind(id, out JsonElement written)) return Read(written, id, raw.Number("level", 1), owner, problems);

            problems.Add($"{owner}/{id}: no 'trait' - a feature without one is a shared feature, and " +
                         $"'{id}' is not in {SharedFeatures.File}");
            return null;
        }

        static Feature Read(JsonElement raw, string id, int level, string owner, List<string> problems)
        {
            if (!EnumWords.TryParse(raw.Text("trait"), out Trait trait))
            {
                problems.Add($"{owner}/{id}: '{raw.Text("trait")}' is not a trait - the closed " +
                             "list is in content/Classes/Feature.cs");
                return null;
            }

            // a boon's key on the feature itself: it goes in the feature's boons
            IEnumerable<string> boonKeys = BoonSpecReader.Keys.Where(k => !Keys.Contains(k));

            foreach (string key in boonKeys.Where(k => raw.Has(k)))
                problems.Add($"{owner}/{id}: '{key}' is what a boon is - it goes in the feature's 'boons'");

            Keyed.OnlyKnown(raw, Keys.Concat(boonKeys), $"{owner}/{id}", problems);

            if (!EnumWords.TryParse(raw.Text("when", "always"), out When when))
                problems.Add($"{owner}/{id}: '{raw.Text("when")}' is not a trigger");

            if (!EnumWords.TryParse(raw.Text("grants", "action"), out Spend grants) ||
                grants != Spend.Action && grants != Spend.Bonus && grants != Spend.Reaction)
                problems.Add($"{owner}/{id}: '{raw.Text("grants")}' is not an action, a " +
                             "bonus_action or a reaction");

            if (!EnumWords.TryParse(raw.Text("duration", "encounter"), out Duration duration))
                problems.Add($"{owner}/{id}: '{raw.Text("duration")}' is not a duration");

            // absent is None, which only a spellcasting feature is then refused for - every other
            // kind of feature has no progression and should not have to say so
            if (!EnumWords.TryName(raw.Text("progression", "none"),
                                    out CasterProgression progression))
                problems.Add($"{owner}/{id}: '{raw.Text("progression")}' is not a caster " +
                             "progression - it is " + Vocabulary.Offer<CasterProgression>());

            var skills = raw.SkillList("skills", problems, $"{owner}/{id}").ToList();

            var saves = raw.AbilityList("saves", problems, $"{owner}/{id}").ToList();

            Manoeuvre manoeuvres = raw.ManoeuvreList("manoeuvres", problems, $"{owner}/{id}");

            var feature = new Feature(id, trait,
                                      level,
                                      raw.Dice("amount", problems, id),
                                      raw.Dice("per_level", problems, id),
                                      raw.Damage("damage_type", problems, id),
                                      raw.Condition("condition", problems, id),
                                      raw.Ability("ability", problems, id),
                                      when,
                                      grants,
                                      raw.Number("uses"),
                                      raw.Number("count"),
                                      raw.Number("per_levels", 1),
                                      duration,
                                      skills,
                                      saves,
                                      progression,
                                      raw.Text("note"),
                                      manoeuvres)
            {
                Tags = raw.Strings("tags"),
                OncePerTurn = raw.Flag("once_per_turn"),
                WhileStances = raw.Strings("while_stances"),
                NeedsWeapon = raw.Text("needs_weapon") ?? "",
                Forgoes = raw.Text("forgoes") ?? "",
                UsesByLevel = Levels(raw, "uses_by_level", owner, id, problems),
                Recharge = Recharge(raw, owner, id, problems),
                Spends = raw.Text("spends") ?? "",
                CritOn = raw.Number("crit_on"),
                AuraAbility = raw.Ability("aura_ability", problems, id),
                ArmoredArmorClass = raw.Number("armored_ac"),
                // what it gives, in the one boon vocabulary: a stance's boon, or its boons for good
                Boons = ReadBoons(raw, duration, trait == Trait.Stance, $"{owner}/{id}", problems),
                ExtraSpeed = raw.Number("speed_change"),
                StaysUpAt = ReadStaysUpAt(raw, $"{owner}/{id}", problems),
                FlatByLevel = Levels(raw, "flat_by_level", owner, id, problems),
                AmountByLevel = Amounts(raw, "amount_by_level", owner, id, problems),
                FlatAbility = raw.Ability("flat_ability", problems, id),
                UseTime = UseTime(raw, owner, id, problems),
                Reaction = raw.Text("reaction") ?? "",
                Spells = SpellLevels(raw),
                FreeCasts = Counts(raw, "free_casts"),
                CantripsByLevel = Levels(raw, "cantrips_by_level", owner, id, problems),
                KnownByLevel = Levels(raw, "known_by_level", owner, id, problems),
                SkillPicks = raw.Number("skill_picks"),
                ExpertiseFrom = raw.SkillList("expertise_from", problems, $"{owner}/{id}"),
                Dc = raw.Number("dc"),
                DcStep = raw.Number("dc_step"),
                OnlyBloodied = raw.Flag("only_bloodied"),
                CapHalf = raw.Flag("cap_half"),
                MaxHitPointsPerLevel = raw.Number("max_hp_per_level"),
                NotInHeavyArmor = raw.Flag("not_in_heavy_armor"),
                InnateSpell = Innate(raw, owner, id, problems),
                SpellAbilities = raw.AbilityList("spell_abilities", problems, $"{owner}/{id}"),
            };

            Check(feature, owner, problems);

            return feature;
        }

        // a feature's boons: a list of records in the BoonSpec vocabulary (Danger Sense's one boon, Remarkable
        // Athlete's two)
        static IReadOnlyList<BoonSpec> ReadBoons(JsonElement raw, Duration duration, bool stance, string where,
                                                 List<string> problems)
        {
            var boons = new List<BoonSpec>();

            foreach (JsonElement entry in raw.Items("boons"))
            {
                Keyed.OnlyKnown(entry, BoonSpecReader.Keys, $"{where} boon", problems);

                BoonSpec boon = BoonSpecReader.Read(entry, duration, where, problems);

                // a stance's flat can come at play time (Sacred Weapon's Charisma), so only a boon
                // for good must do something as written
                BoonSpecReader.Check(boon, where, problems, mustDoSomething: !stance);
                boons.Add(boon);
            }

            return boons;
        }

        // "stays_up_at": {"hit_points": 1} or {"per_level": 2}
        static StaysUpAt ReadStaysUpAt(JsonElement raw, string where, List<string> problems)
        {
            if (!raw.Has("stays_up_at")) return null;

            JsonElement at = raw.GetProperty("stays_up_at");

            Keyed.OnlyKnown(at, StaysUpAtKeys, $"{where} stays_up_at", problems);

            return new StaysUpAt(at.Number("hit_points"), at.Number("per_level"));
        }

        static Spell Innate(JsonElement raw, string owner, string id, List<string> problems)
        {
            if (!raw.Has("spell")) return null;

            var trouble = new List<string>();
            Spell spell = Content.Spells.SpellReader.ReadEntry(raw.GetProperty("spell"), trouble);

            foreach (string p in trouble) problems.Add($"{owner}/{id}: {p}");

            return spell;
        }

        // {"3": 2, "6": 4}: a number by the level it starts at
        static Dictionary<int, int> Levels(JsonElement raw, string name, string owner, string id,
                                           List<string> problems)
        {
            var table = new Dictionary<int, int>();

            if (!raw.Has(name)) return table;

            foreach (JsonProperty entry in raw.GetProperty(name).EnumerateObject())
            {
                if (int.TryParse(entry.Name, out int level) && entry.Value.TryGetInt32(out int value))
                    table[level] = value;
                else
                    problems.Add($"{owner}/{id}: '{name}' is levels to numbers - '{entry.Name}' isn't");
            }

            return table;
        }

        static Dictionary<int, DiceRoll> Amounts(JsonElement raw, string name, string owner, string id,
                                                 List<string> problems)
        {
            var table = new Dictionary<int, DiceRoll>();

            if (!raw.Has(name)) return table;

            foreach (JsonProperty entry in raw.GetProperty(name).EnumerateObject())
            {
                if (int.TryParse(entry.Name, out int level) &&
                    DiceRoll.TryParse(entry.Value.GetString() ?? "", out DiceRoll dice, out _))
                    table[level] = dice;
                else
                    problems.Add($"{owner}/{id}: '{name}' is levels to dice - '{entry.Name}' isn't");
            }

            return table;
        }

        static Dictionary<int, IReadOnlyList<string>> SpellLevels(JsonElement raw)
        {
            var table = new Dictionary<int, IReadOnlyList<string>>();

            if (!raw.Has("spells")) return table;

            foreach (JsonProperty entry in raw.GetProperty("spells").EnumerateObject())
                if (int.TryParse(entry.Name, out int level))
                    table[level] = entry.Value.EnumerateArray().Select(v => v.GetString())
                                        .Where(s => !string.IsNullOrEmpty(s)).ToList();

            return table;
        }

        static Dictionary<string, int> Counts(JsonElement raw, string name)
        {
            var table = new Dictionary<string, int>(StringComparer.Ordinal);

            if (!raw.Has(name)) return table;

            foreach (JsonProperty entry in raw.GetProperty(name).EnumerateObject())
                if (entry.Value.TryGetInt32(out int n)) table[entry.Name] = n;

            return table;
        }

        static Recharge Recharge(JsonElement raw, string owner, string id, List<string> problems)
        {
            if (!EnumWords.TryParse(raw.Text("recharge", "short_rest"), out Recharge read))
                problems.Add($"{owner}/{id}: 'recharge' is short_rest, one_per_short_rest or long_rest");

            return read;
        }

        // what switching a stance on takes out of the turn: the item's word and Spend's words
        static Spend UseTime(JsonElement raw, string owner, string id, List<string> problems)
        {
            if (EnumWords.TryParse(raw.Text("use_time", "bonus_action"), out Spend spend) &&
                (spend == Spend.Bonus || spend == Spend.Action || spend == Spend.Free))
                return spend;

            problems.Add($"{owner}/{id}: 'use_time' is bonus_action, action or free");
            return Spend.Bonus;
        }

        static void Check(Feature feature, string owner, List<string> problems)
        {
            string where = $"{owner}/{feature.Id}";

            switch (feature.Trait)
            {
                case Trait.Rider:
                    if (feature.Amount.IsNothing && feature.Condition == Condition.None)
                        problems.Add($"{where}: a rider that adds nothing and applies nothing");

                    // no damage_type is the normal case: a Sneak Attack is not its own kind of
                    // damage, it is more of the weapon's
                    break;

                case Trait.Stance:
                    if (feature.Boons.Count != 1)
                        problems.Add($"{where}: a stance puts on one boon - 'boons' holds exactly one");
                    else if (feature.Boons[0].Touches == Sways.None && feature.Boons[0].Leans == Leans.None &&
                             feature.Boons[0].Defenses.Count == 0)
                        problems.Add($"{where}: a stance that touches no roll");
                    break;

                case Trait.DeathIntercept:
                    if (feature.StaysUpAt == null)
                        problems.Add($"{where}: a death intercept says what it 'stays_up_at'");
                    break;

                case Trait.Speed:
                    if (feature.ExtraSpeed == 0)
                        problems.Add($"{where}: a speed feature with no 'speed_change' in feet");
                    break;

                case Trait.UnarmoredDefense:
                    if (!feature.Ability.HasValue)
                        problems.Add($"{where}: unarmored defence needs the ability it reads");
                    break;


                case Trait.Spellcasting:
                    if (!feature.Ability.HasValue)
                        problems.Add($"{where}: spellcasting needs its ability");

                    if (feature.Progression == CasterProgression.None)
                        problems.Add($"{where}: spellcasting needs a progression - it is " +
                                     Vocabulary.Offer<CasterProgression>() +
                                     ", and it is what both the slot table and the point pool " +
                                     "are read from");
                    break;

                case Trait.Recovery:
                    if (feature.Amount.IsNothing)
                        problems.Add($"{where}: a recovery that heals nothing");
                    break;

                case Trait.Expertise:
                    if (feature.Count <= 0 && feature.Skills.Count == 0)
                        problems.Add($"{where}: expertise in how many skills?");
                    break;

                case Trait.ActionGrant:
                    // an extra action every round is allowed - it is Extra Attack, ported
                    // literally (decisions_checklist.md section 1, corrected 2026-09-23). what is
                    // refused is a grant so large no single turn could ever hold it
                    if (Math.Max(1, feature.Count) >
                        ActionBudget.MostActionsInATurn - ActionBudget.BaseActions)
                        problems.Add($"{where}: grants {feature.Count} actions, and no turn holds " +
                                     $"more than {ActionBudget.MostActionsInATurn}");
                    break;

                case Trait.Nimble:
                    if (feature.Manoeuvres == Manoeuvre.None)
                        problems.Add($"{where}: a nimble feature that frees no manoeuvre - say " +
                                     "which of dash, disengage and hide with 'manoeuvres'");
                    break;
            }
        }
    }
}
