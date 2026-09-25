using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Magic;
using Core.Characters;
using Core.Combat;
using Core.Dice;

namespace Content.Classes
{
    public static class ClassReader
    {
        public static bool TryRead(string text, out IReadOnlyList<CharacterClass> classes,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<CharacterClass>();
            var trouble = new List<string>();

            classes = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                foreach (JsonElement entry in document.RootElement.Items("classes"))
                {
                    CharacterClass read = ReadOne(entry, trouble);

                    if (read != null) found.Add(read);
                }

                if (found.Count == 0 && trouble.Count == 0)
                    trouble.Add("no classes in it - the file is an object with a 'classes' array");
            }

            return trouble.Count == 0;
        }

        static CharacterClass ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a class id");
                return null;
            }

            if (!DieExtensions.TryParse(entry.Text("hit_die", "d8"), out Die hitDie))
            {
                problems.Add($"{id}: '{entry.Text("hit_die")}' is not a die");
                hitDie = Die.D8;
            }

            var saves = new List<Ability>();

            foreach (string save in entry.Strings("saves"))
            {
                if (Abilities.TryParse(save, out Ability ability)) saves.Add(ability);
                else problems.Add($"{id}: '{save}' is not an ability");
            }

            if (saves.Count != 2)
                problems.Add($"{id}: {saves.Count} save proficiencies - SRD gives every class two");

            var skills = new List<Skill>();

            foreach (string skill in entry.Strings("skill_choices"))
            {
                if (Skills.TryParse(skill, out Skill read)) skills.Add(read);
                else problems.Add($"{id}: '{skill}' is not one of the eighteen skills");
            }

            var armor = new List<ArmorWeight>();

            foreach (string weight in entry.Strings("armor"))
            {
                if (ArmorWeights.TryParse(weight, out ArmorWeight read)) armor.Add(read);
                else problems.Add($"{id}: '{weight}' is not an armor weight");
            }

            var priority = new List<Ability>();

            foreach (string ability in entry.Strings("priority"))
            {
                if (Abilities.TryParse(ability, out Ability read)) priority.Add(read);
                else problems.Add($"{id}: '{ability}' is not an ability");
            }

            var features = new List<Feature>();

            foreach (JsonElement raw in entry.Items("features"))
            {
                Feature feature = FeatureReader.ReadOne(raw, id, problems);

                if (feature != null) features.Add(feature);
            }

            int picks = entry.Number("skill_picks", 2);

            if (picks > skills.Count && skills.Count > 0)
                problems.Add($"{id}: {picks} skills to pick from a list of {skills.Count}");

            IReadOnlyList<int> improvements = CharacterClass.UsualImprovementLevels;

            if (entry.Has("improvement_levels"))
            {
                var levels = new List<int>();

                foreach (System.Text.Json.JsonElement level in entry.Items("improvement_levels"))
                    if (level.ValueKind == System.Text.Json.JsonValueKind.Number &&
                        level.TryGetInt32(out int at) && at >= 1 && at <= 20)
                        levels.Add(at);
                    else
                        problems.Add($"{id}: improvement_levels holds '{level}', which is not a level from 1 to 20");

                improvements = levels.Distinct().OrderBy(l => l).ToList();
            }

            return new CharacterClass(id, hitDie, saves, skills, picks, armor,
                                      entry.Flag("shields"),
                                      entry.Strings("starting_gear"),
                                      features,
                                      entry.Text("subclass"),
                                      entry.Text("companion"),
                                      priority)
            {
                ImprovementLevels = improvements,
                Gold = entry.Number("gold"),
                Tools = entry.Strings("tools"),
                Weapons = entry.Strings("weapons"),
            };
        }
    }

    // shared by classes and species: a feature is a feature wherever it came from
    public static class FeatureReader
    {
        public static Feature ReadOne(JsonElement raw, string owner, List<string> problems)
        {
            string id = raw.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"{owner}: '{id}' is not a feature id");
                return null;
            }

            if (!Traits.TryParse(raw.Text("trait"), out Trait trait))
            {
                problems.Add($"{owner}/{id}: '{raw.Text("trait")}' is not a trait - the closed " +
                             "list is in content/Classes/Feature.cs");
                return null;
            }

            if (!Traits.TryParse(raw.Text("when", "always"), out When when))
                problems.Add($"{owner}/{id}: '{raw.Text("when")}' is not a trigger");

            if (!Traits.TryParse(raw.Text("grants", "action"), out Grants grants))
                problems.Add($"{owner}/{id}: '{raw.Text("grants")}' is not an action, a " +
                             "bonus_action or a reaction");

            if (!Core.Magic.Schools.TryParse(raw.Text("duration", "encounter"), out Duration duration))
                problems.Add($"{owner}/{id}: '{raw.Text("duration")}' is not a duration");

            if (!Core.Magic.Primitives.TryParse(raw.Text("touches", "none"),
                                                out Core.Magic.Sways touches))
                problems.Add($"{owner}/{id}: '{raw.Text("touches")}' is not a list of swayed rolls");

            if (!DamageTypes.TryParse(raw.Text("defense", "resistant"), out Defense defense))
                defense = Defense.Resistant;

            // absent is None, which only a spellcasting feature is then refused for - every other
            // kind of feature has no progression and should not have to say so
            if (!Vocabulary.TryWord(raw.Text("progression", "none"),
                                    out CasterProgression progression))
                problems.Add($"{owner}/{id}: '{raw.Text("progression")}' is not a caster " +
                             "progression - it is " + Vocabulary.Offer<CasterProgression>());

            var skills = new List<Skill>();

            foreach (string skill in raw.Strings("skills"))
            {
                if (Skills.TryParse(skill, out Skill read)) skills.Add(read);
                else problems.Add($"{owner}/{id}: '{skill}' is not a skill");
            }

            var saves = new List<Ability>();

            foreach (string save in raw.Strings("saves"))
            {
                if (Abilities.TryParse(save, out Ability read)) saves.Add(read);
                else problems.Add($"{owner}/{id}: '{save}' is not an ability");
            }

            Manoeuvre manoeuvres = Manoeuvre.None;

            foreach (string word in raw.Strings("manoeuvres"))
            {
                if (Manoeuvres.TryParse(word, out Manoeuvre read)) manoeuvres |= read;
                else problems.Add($"{owner}/{id}: '{word}' is not dash, disengage or hide");
            }

            var feature = new Feature(id, trait,
                                      raw.Number("level", 1),
                                      raw.Dice("amount", problems, id),
                                      raw.Dice("per_level", problems, id),
                                      raw.Damage("damage_type", problems, id),
                                      raw.Condition("condition", problems, id),
                                      raw.Ability("ability", problems, id),
                                      defense,
                                      when,
                                      grants,
                                      raw.Number("flat"),
                                      raw.Number("uses"),
                                      raw.Number("count"),
                                      raw.Number("per_levels", 1),
                                      duration,
                                      skills,
                                      saves,
                                      touches,
                                      progression,
                                      raw.Text("note"),
                                      manoeuvres)
            {
                Tags = raw.Strings("tags"),
                OncePerTurn = raw.Flag("once_per_turn"),
                WhileStances = raw.Strings("while_stances"),
                Weapon = raw.Text("weapon") ?? "",
                Forgoes = raw.Text("forgoes") ?? "",
                UsesByLevel = Levels(raw, "uses_by_level", owner, id, problems),
                Recharge = Recharge(raw, owner, id, problems),
                Spends = raw.Text("spends") ?? "",
                AdvantageOn = raw.Strings("advantage_on"),
                UnlessIncapacitated = raw.Flag("unless_incapacitated"),
                CritOn = raw.Number("crit_on"),
                AuraAbility = raw.Ability("aura_ability", problems, id),
                ArmoredArmorClass = raw.Number("armored_ac"),
                Immune = raw.Strings("immune")
                            .Select(c => Conditions.TryParse(c, out Condition read) ? read : Condition.None)
                            .Where(c => c != Condition.None).ToList(),
                FlatByLevel = Levels(raw, "flat_by_level", owner, id, problems),
                AmountByLevel = Amounts(raw, "amount_by_level", owner, id, problems),
                FlatAbility = raw.Ability("flat_ability", problems, id),
                Resists = raw.Strings("resists")
                             .Select(d => DamageTypes.TryParse(d, out DamageType read) ? read : DamageType.None)
                             .Where(d => d != DamageType.None).ToList(),
                StanceAdvantage = raw.Strings("stance_advantage"),
                StrengthOnly = raw.Flag("strength_only"),
                AdvantageAgainst = raw.Flag("advantage_against"),
                Cost = Cost(raw, owner, id, problems),
                Reaction = raw.Text("reaction") ?? "",
                Spells = SpellLevels(raw),
                FreeCasts = Counts(raw, "free_casts"),
                CantripsByLevel = Levels(raw, "cantrips_by_level", owner, id, problems),
                KnownByLevel = Levels(raw, "known_by_level", owner, id, problems),
                SkillPicks = raw.Number("skill_picks"),
                ExpertiseFrom = raw.Strings("expertise_from")
                                   .Select(s => Skills.TryParse(s, out Skill read) ? read : Skill.None)
                                   .Where(s => s != Skill.None).ToList(),
                SaveDc = raw.Number("save_dc"),
                DcStep = raw.Number("dc_step"),
                HitPointsPerLevel = raw.Number("hp_per_level"),
                OnlyBloodied = raw.Flag("only_bloodied"),
                CapHalf = raw.Flag("cap_half"),
                MaxHitPointsPerLevel = raw.Number("max_hp_per_level"),
                NotInHeavyArmor = raw.Flag("not_in_heavy_armor"),
                InnateSpell = Innate(raw, owner, id, problems),
                SpellAbilities = raw.Strings("spell_abilities")
                                    .Select(s => Abilities.TryParse(s, out Ability read) ? read : (Ability?)null)
                                    .Where(a => a.HasValue).Select(a => a.Value).ToList(),
            };

            Check(feature, owner, problems);

            return feature;
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
            if (!Recharges.TryParse(raw.Text("recharge", "short"), out Recharge read))
                problems.Add($"{owner}/{id}: 'recharge' is short, short_one or long");

            return read;
        }

        static Spend Cost(JsonElement raw, string owner, string id, List<string> problems)
        {
            switch (raw.Text("cost", "bonus"))
            {
                case "bonus": return Spend.Bonus;
                case "action": return Spend.Action;
                case "free": return Spend.Free;
                default:
                    problems.Add($"{owner}/{id}: 'cost' is bonus, action or free");
                    return Spend.Bonus;
            }
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
                    if (feature.Touches == Core.Magic.Sways.None && feature.StanceAdvantage.Count == 0 &&
                        feature.Resists.Count == 0 && !feature.AdvantageAgainst)
                        problems.Add($"{where}: a stance that touches no roll");
                    break;

                case Trait.UnarmoredDefense:
                    if (!feature.Ability.HasValue)
                        problems.Add($"{where}: unarmored defence needs the ability it reads");
                    break;

                case Trait.Resistance:
                    if (feature.DamageType == DamageType.None)
                        problems.Add($"{where}: resistance to what?");
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
                    if (feature.Amount.IsNothing && feature.Flat == 0)
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
