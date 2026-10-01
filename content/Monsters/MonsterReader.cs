using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Items;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Content.Spells;
using Core.Words;

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
                Keyed.OnlyKnown(document.RootElement, new[] { "monsters" }, "the monsters file", trouble);

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

        // every key a statblock takes, and the keys of its attacks, their on-hit riders, its special
        // actions and its spellcasting
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "id", "hit_points", "armor_class", "speed", "challenge_times_ten", "hit_die", "mini", "size",
            "tags", "scores", "skills", "expertise", "saves", "instincts", "manoeuvres", "attacks",
            "multiattack", "defenses", "immune", "actions", "spellcasting", "not_in_srd", "traits",
        };

        // a statblock's attack: its name, whether it is held, and the SRD weapon it is if it is one
        // (cc_task_godfiles-dupes-efficiency.md #10); the rest is what every attack takes
        // (AttackReader.Keys), and with a weapon only what differs from it
        public static readonly IReadOnlyList<string> AttackKeys = new[] { "id", "held", "weapon" };

        public static readonly IReadOnlyList<string> ActionKeys = new[] { "spell", "recharge", "uses", "dc" };

        public static readonly IReadOnlyList<string> SpellcastingKeys =
            new[] { "ability", "dc", "attack_bonus", "at_will", "uses" };

        // inside an action's "recharge": {"d6": 5} is SRD's "Recharge 5-6"
        public static readonly IReadOnlyList<string> RechargeKeys = new[] { "d6" };

        static Monster ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a monster id");
                return null;
            }

            Keyed.OnlyKnown(entry, Keys, id, problems);

            var scores = new AbilityScores();

            foreach (KeyValuePair<Ability, int> score in entry.AbilityRecord("scores", problems, id))
                scores.SetBase(score.Key, score.Value);

            List<Attack> attacks = ReadAttacks(entry, id, problems);

            List<MonsterAction> actions = ReadActions(entry, id, problems);

            MonsterSpellcasting casting = ReadSpellcasting(entry, id, actions, problems);

            // the same record a boon's 'defenses' is, read by the same helper
            IReadOnlyDictionary<DamageType, Defense> defenses = entry.DefenseRecord("defenses", problems, id);

            var saves = entry.AbilityList("saves", problems, id).ToList();

            var skills = entry.SkillList("skills", problems, id).ToList();

            var expertise = entry.SkillList("expertise", problems, id).ToList();

            Instinct instinct = ReadInstincts(entry, id, problems);

            if (!DieExtensions.TryParse(entry.Text("hit_die", "d8"), out Die hitDie))
                problems.Add($"{id}: '{entry.Text("hit_die")}' is not a die");

            if (!EnumWords.TryParse(entry.Text("size", "medium"), out Size size))
                problems.Add($"{id}: '{entry.Text("size")}' is not a size");

            // the boon's word for conditions it can't have
            IReadOnlyList<Condition> immunities = entry.ConditionList("immune", problems, id);

            Multiattack multiattack = ReadMultiattack(entry, id, attacks, problems);

            // what its bonus action may be spent on: the goblin's Nimble Escape, in the word a
            // feature's Cunning Action uses
            Manoeuvre bonus = entry.ManoeuvreList("manoeuvres", problems, id);

            return new Monster(id,
                               entry.Number("hit_points", 1),
                               entry.Number("armor_class", 10),
                               scores, attacks,
                               ArmorCategory.Heavy,
                               entry.WalkingSpeed(id, problems),
                               entry.Number("challenge_times_ten") / 10.0,
                               multiattack,
                               instinct,
                               defenses, saves, skills,
                               entry.Text("mini"),
                               entry.Strings("tags"),
                               hitDie)
            {
                Size = size,
                ImmuneTo = immunities,
                Actions = actions,
                Spellcasting = casting,
                BonusManoeuvres = bonus,
                NotInSrd = entry.Flag("not_in_srd"),
                Knacks = entry.FlagList<Knack>("traits", "a trait v1 plays (pack_tactics, bloodied_fury, " +
                                                         "magic_resistance, undead_fortitude, sunlight_sensitivity)",
                                               problems, id),
                Expertise = expertise,
            };
        }

        static List<Attack> ReadAttacks(JsonElement entry, string id, List<string> problems)
        {
            var attacks = new List<Attack>();

            foreach (JsonElement raw in entry.Items("attacks"))
            {
                string weapon = raw.Text("weapon");
                string name = raw.Has("id") ? raw.Text("id") : weapon;

                if (!Json.IsId(name))
                {
                    problems.Add($"{id}: '{name}' is not an attack id");
                    continue;
                }

                Keyed.OnlyKnown(raw, AttackKeys.Concat(AttackReader.Keys), $"{id}/{name}", problems);

                // the SRD's weapons: ItemShelf's own copy, not the Library's, which reads this
                // reader while it loads
                Attack basis = null;

                if (weapon.Length > 0 && (basis = ItemShelf.Srd().Find(weapon)?.Attack) == null)
                    problems.Add($"{id}/{name}: 'weapon' is an SRD weapon's id - '{weapon}' isn't one");

                // a weapon in hand can be dropped; a claw cannot
                attacks.Add(AttackReader.Read(raw, name, raw.Flag("held") ? Hand.Main : Hand.None,
                                              $"{id}/{name}", problems, basis));
            }

            if (attacks.Count == 0) problems.Add($"{id}: a monster with nothing to attack with");

            return attacks;
        }

        // special actions: each an inline spell, with its limit and its DC
        static List<MonsterAction> ReadActions(JsonElement entry, string id, List<string> problems)
        {
            var actions = new List<MonsterAction>();

            foreach (JsonElement raw in entry.Items("actions"))
            {
                Keyed.OnlyKnown(raw, ActionKeys, $"{id} action", problems);

                if (!raw.Has("spell"))
                {
                    problems.Add($"{id}: an action is a 'spell' with a 'recharge' or 'uses'");
                    continue;
                }

                var spellProblems = new List<string>();
                Spell spell = SpellReader.ReadEntry(raw.GetProperty("spell"), spellProblems);

                foreach (string p in spellProblems) problems.Add($"{id}: {p}");

                if (spell == null) continue;

                int rechargeOn = 0;

                if (raw.Has("recharge"))
                {
                    JsonElement recharge = raw.GetProperty("recharge");

                    Keyed.OnlyKnown(recharge, RechargeKeys, $"{id} action recharge", problems);
                    rechargeOn = recharge.ValueKind == JsonValueKind.Object ? recharge.Number("d6") : 0;

                    if (rechargeOn < 2 || rechargeOn > 6)
                        problems.Add($"{id}: an action's 'recharge' is {{\"d6\": 5}} - the lowest d6 roll that brings it back");
                }

                actions.Add(new MonsterAction(spell, rechargeOn, raw.Number("uses"), raw.Number("dc")));
            }

            return actions;
        }

        // its spellcasting, and the one DC its actions share: their own if they say it, else the
        // spellcasting's
        static MonsterSpellcasting ReadSpellcasting(JsonElement entry, string id, List<MonsterAction> actions,
                                                    List<string> problems)
        {
            MonsterSpellcasting casting = null;

            if (entry.Has("spellcasting"))
            {
                JsonElement raw = entry.GetProperty("spellcasting");

                Keyed.OnlyKnown(raw, SpellcastingKeys, $"{id} spellcasting", problems);
                Ability? castWith = raw.Ability("ability", problems, id);

                // "uses": {"cure_wounds": 3} - SRD's "3/day each", back on a long rest
                var uses = new Dictionary<string, int>();

                if (raw.Has("uses"))
                    foreach (JsonProperty daily in raw.GetProperty("uses").EnumerateObject())
                        uses[daily.Name] = daily.Value.TryGetInt32(out int n) ? n : 1;

                casting = new MonsterSpellcasting(castWith ?? Ability.Charisma,
                                                  raw.Number("dc", 10),
                                                  raw.Number("attack_bonus", 2),
                                                  raw.Strings("at_will"), uses);
            }

            // an action's DC is the statblock's spellcasting DC unless it says its own
            int actionDc = actions.Select(a => a.Dc).FirstOrDefault(dc => dc > 0);

            if (actions.Select(a => a.Dc).Where(dc => dc > 0).Distinct().Count() > 1)
                problems.Add($"{id}: its actions' 'dc's differ - a statblock's actions share one DC");

            if (actionDc == 0) actionDc = casting?.Dc ?? 10;

            if (actions.Count > 0 && casting == null)
                casting = new MonsterSpellcasting(Ability.Charisma, actionDc, 2,
                                                  Array.Empty<string>(),
                                                  new Dictionary<string, int>());

            return casting;
        }

        static Instinct ReadInstincts(JsonElement entry, string id, List<string> problems)
        {
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

            return instinct;
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
