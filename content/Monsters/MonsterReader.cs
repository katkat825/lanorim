using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Rules;
using Core.Magic;
using Content.Spells;

namespace Content.Monsters
{
    public static class MonsterReader
    {
        public static bool TryRead(string text, out IReadOnlyList<Monster> monsters,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<Monster>();
            var trouble = new List<string>();

            monsters = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                foreach (JsonElement entry in document.RootElement.Items("monsters"))
                {
                    Monster monster = ReadOne(entry, trouble);

                    if (monster != null) found.Add(monster);
                }

                if (found.Count == 0 && trouble.Count == 0)
                    trouble.Add("no monsters in it - the file is an object with a 'monsters' array");
            }

            return trouble.Count == 0;
        }

        static Monster ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a monster id");
                return null;
            }

            var scores = new AbilityScores();

            if (entry.Has("scores"))
                foreach (JsonProperty score in entry.GetProperty("scores").EnumerateObject())
                {
                    if (Abilities.TryParse(score.Name, out Ability ability) &&
                        score.Value.TryGetInt32(out int value))
                        scores.SetBase(ability, value);
                    else
                        problems.Add($"{id}: '{score.Name}' is not an ability score");
                }

            var attacks = new List<Attack>();

            foreach (JsonElement raw in entry.Items("attacks"))
            {
                string name = raw.Text("id");

                if (!Json.IsId(name))
                {
                    problems.Add($"{id}: '{name}' is not an attack id");
                    continue;
                }

                DiceRoll damage = raw.Dice("damage", problems, id);

                if (damage.IsNothing) problems.Add($"{id}/{name}: an attack with no damage");

                DamageType type = raw.Damage("damage_type", problems, id);

                if (type == DamageType.None)
                    problems.Add($"{id}/{name}: an attack with no damage_type");

                Ability? ability = raw.Ability("ability", problems, id);

                var riders = new List<Rider>();

                if (raw.Has("on_hit"))
                {
                    JsonElement hit = raw.GetProperty("on_hit");
                    Size? maxSize = null;

                    if (!string.IsNullOrEmpty(hit.Text("max_size")))
                    {
                        if (Sizes.TryParse(hit.Text("max_size"), out Size read)) maxSize = read;
                        else problems.Add($"{id}/{name}: '{hit.Text("max_size")}' is not a size");
                    }

                    var rider = new Rider(name + "_hit", hit.Dice("damage", problems, id),
                                          hit.Damage("damage_type", problems, id),
                                          hit.Condition("condition", problems, id))
                    {
                        Save = hit.Ability("save", problems, id),
                        Dc = hit.Number("dc"),
                        MaxSize = maxSize,
                        ExceptTags = hit.Strings("except_tags"),
                        UntilTargetsNextTurn = hit.Flag("until_next_turn"),
                    };

                    if (rider.UntilTargetsNextTurn && rider.Condition == Condition.None)
                        problems.Add($"{id}/{name}: 'until_next_turn' needs a condition to end");

                    if (rider.Save.HasValue && rider.Dc <= 0)
                        problems.Add($"{id}/{name}: an on-hit save with no 'dc'");

                    if (rider.Damage.IsNothing && rider.Condition == Condition.None)
                        problems.Add($"{id}/{name}: an 'on_hit' that adds nothing");

                    riders.Add(rider);
                }

                attacks.Add(new Attack(name, damage, type,
                                       ability ?? Ability.Strength,
                                       true,
                                       raw.Number("reach", 1),
                                       raw.Number("range"),
                                       raw.Number("long_range"),
                                       // a weapon in hand can be dropped; a claw cannot
                                       raw.Flag("held") ? Hand.Main : Hand.None,
                                       raw.Flag("finesse"),
                                       raw.Number("attack_bonus"),
                                       raw.Number("damage_bonus"),
                                       // a flat roll, like a priest's Radiant Flame: 11 (2d10)
                                       raw.Flag("adds_ability", true))
                            {
                                OnHit = riders,
                                // "Melee or Ranged Attack Roll": melee within reach, thrown past it
                                Thrown = raw.Flag("thrown"),
                            });
            }

            if (attacks.Count == 0) problems.Add($"{id}: a monster with nothing to attack with");

            // special actions: each an inline spell, with its limit and its DC
            var actions = new List<MonsterAction>();

            foreach (JsonElement raw in entry.Items("actions"))
            {
                if (!raw.Has("spell"))
                {
                    problems.Add($"{id}: an action is a 'spell' with a 'recharge' or a 'per_day'");
                    continue;
                }

                var spellProblems = new List<string>();
                Spell spell = SpellReader.ReadEntry(raw.GetProperty("spell"), spellProblems);

                foreach (string p in spellProblems) problems.Add($"{id}: {p}");

                if (spell == null) continue;

                actions.Add(new MonsterAction(spell, raw.Number("recharge"), raw.Number("per_day"),
                                              raw.Number("dc")));
            }

            MonsterSpellcasting casting = null;

            if (entry.Has("spellcasting"))
            {
                JsonElement raw = entry.GetProperty("spellcasting");
                Ability? castWith = raw.Ability("ability", problems, id);

                var perDay = new Dictionary<string, int>();

                if (raw.Has("per_day"))
                    foreach (JsonProperty daily in raw.GetProperty("per_day").EnumerateObject())
                        perDay[daily.Name] = daily.Value.TryGetInt32(out int n) ? n : 1;

                casting = new MonsterSpellcasting(castWith ?? Ability.Charisma,
                                                  raw.Number("dc", 10),
                                                  raw.Number("attack_bonus", 2),
                                                  raw.Strings("at_will"), perDay);
            }

            // an action's DC is the statblock's spellcasting DC unless it says its own
            int actionDc = entry.Has("action_dc") ? entry.Number("action_dc") : casting?.Dc ?? 10;

            if (actions.Count > 0 && casting == null)
                casting = new MonsterSpellcasting(Ability.Charisma, actionDc, 2,
                                                  Array.Empty<string>(),
                                                  new Dictionary<string, int>());

            var defenses = new Dictionary<DamageType, Defense>();

            if (entry.Has("defenses"))
                foreach (JsonProperty defense in entry.GetProperty("defenses").EnumerateObject())
                {
                    if (!DamageTypes.TryParse(defense.Name, out DamageType type))
                    {
                        problems.Add($"{id}: '{defense.Name}' is not a damage type");
                        continue;
                    }

                    if (!DamageTypes.TryParse(defense.Value.GetString() ?? "", out Defense how))
                    {
                        problems.Add($"{id}: '{defense.Value}' is not normal, vulnerable, " +
                                     "resistant or immune");
                        continue;
                    }

                    defenses[type] = how;
                }

            var saves = new List<Ability>();

            foreach (string save in entry.Strings("saves"))
            {
                if (Abilities.TryParse(save, out Ability ability)) saves.Add(ability);
                else problems.Add($"{id}: '{save}' is not an ability");
            }

            var skills = new List<Skill>();

            foreach (string skill in entry.Strings("skills"))
            {
                if (Core.Characters.Skills.TryParse(skill, out Skill read)) skills.Add(read);
                else problems.Add($"{id}: '{skill}' is not a skill");
            }

            var expertise = new List<Skill>();

            foreach (string skill in entry.Strings("expertise"))
            {
                if (Core.Characters.Skills.TryParse(skill, out Skill read)) expertise.Add(read);
                else problems.Add($"{id}: '{skill}' is not a skill");
            }

            Instinct instinct = Instinct.None;

            foreach (string tag in entry.Strings("instincts"))
            {
                switch (tag.ToLowerInvariant())
                {
                    case "finisher": instinct |= Instinct.Finisher; break;
                    case "cautious": instinct |= Instinct.Cautious; break;
                    case "skirmisher": instinct |= Instinct.Skirmisher; break;
                    case "stubborn": instinct |= Instinct.Stubborn; break;
                    case "craven": instinct |= Instinct.Craven; break;
                    default: problems.Add($"{id}: '{tag}' is not an instinct"); break;
                }
            }

            if (!DieExtensions.TryParse(entry.Text("hit_die", "d8"), out Die hitDie))
                problems.Add($"{id}: '{entry.Text("hit_die")}' is not a die");

            if (!Sizes.TryParse(entry.Text("size", "medium"), out Size size))
                problems.Add($"{id}: '{entry.Text("size")}' is not a size");

            var immunities = new List<Condition>();

            foreach (string word in entry.Strings("condition_immunities"))
            {
                if (Conditions.TryParse(word, out Condition read)) immunities.Add(read);
                else problems.Add($"{id}: '{word}' in 'condition_immunities' is not a condition");
            }

            Multiattack multiattack = ReadMultiattack(entry, id, attacks, problems);

            Manoeuvre bonus = Manoeuvre.None;

            foreach (string word in entry.Strings("bonus_action"))
            {
                switch (word.ToLowerInvariant())
                {
                    case "dash": bonus |= Manoeuvre.Dash; break;
                    case "disengage": bonus |= Manoeuvre.Disengage; break;
                    case "hide": bonus |= Manoeuvre.Hide; break;
                    default:
                        problems.Add($"{id}: '{word}' in 'bonus_action' is not dash, disengage or hide");
                        break;
                }
            }

            return new Monster(id,
                               entry.Number("hit_points", 1),
                               entry.Number("armor_class", 10),
                               scores, attacks,
                               ArmorWeight.Heavy,
                               entry.Number("speed", 30),
                               entry.Number("challenge_times_ten") / 10.0,
                               multiattack,
                               instinct,
                               defenses, saves, skills,
                               entry.Text("mini"),
                               entry.Strings("tags"),
                               hitDie)
            {
                Size = size,
                ConditionImmunities = immunities,
                Actions = actions,
                Spellcasting = casting,
                BonusManoeuvres = bonus,
                Expertise = expertise,
            };
        }

        // "multiattack": 2 is two attacks with whatever it has ("using Scimitar or Shortbow in any
        // combination"). a list names each attack: ["bear_bite", "bear_claw"] is one Bite and one
        // Claw, and "claw|werewolf_bite" is a slot either may fill. no cap: a mage makes three
        static Multiattack ReadMultiattack(JsonElement entry, string id, IReadOnlyList<Attack> attacks,
                                           List<string> problems)
        {
            if (!entry.Has("multiattack")) return null;

            JsonElement raw = entry.GetProperty("multiattack");

            if (raw.ValueKind == JsonValueKind.Number)
            {
                if (!raw.TryGetInt32(out int count) || count < 1)
                {
                    problems.Add($"{id}: multiattack {raw} - a count of attacks, 1 or more");
                    return null;
                }

                return count > 1 ? Multiattack.Any(count) : null;
            }

            if (raw.ValueKind != JsonValueKind.Array)
            {
                problems.Add($"{id}: 'multiattack' is a count or a list of attacks");
                return null;
            }

            var slots = new List<string[]>();

            foreach (JsonElement slot in raw.EnumerateArray())
            {
                string[] ids = (slot.ValueKind == JsonValueKind.String ? slot.GetString() : "")
                               .Split('|', StringSplitOptions.TrimEntries |
                                           StringSplitOptions.RemoveEmptyEntries);

                if (ids.Length == 0) problems.Add($"{id}: an empty attack in 'multiattack'");

                foreach (string attack in ids)
                    if (!attacks.Any(a => a.Id == attack))
                        problems.Add($"{id}: multiattack names '{attack}', which is not one of its attacks");

                slots.Add(ids);
            }

            return slots.Count > 1 ? new Multiattack(slots) : null;
        }
    }
}
