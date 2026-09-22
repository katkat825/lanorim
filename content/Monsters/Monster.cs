using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;

namespace Content.Monsters
{
    // a statblock. the base is uniform and cheap; the cost is the special abilities, so v1 ships
    // four primitives and no more (decisions_checklist.md section 6): multiattack, save-or-
    // condition, recharge and resistance. legendary and lair actions are skipped outright.
    public sealed class Monster
    {
        public Monster(string id, int hitPoints, int armorClass, AbilityScores scores,
                       IReadOnlyList<Attack> attacks = null,
                       ArmorWeight armorWeight = ArmorWeight.None,
                       int speed = 30, double challenge = 0,
                       int multiattack = 1,
                       Instinct instinct = Instinct.None,
                       IReadOnlyDictionary<DamageType, Defense> defenses = null,
                       IReadOnlyList<Ability> saves = null,
                       IReadOnlyList<Skill> skills = null,
                       string mini = null, IReadOnlyList<string> tags = null,
                       Die hitDie = Die.D8)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            HitPoints = Math.Max(1, hitPoints);
            ArmorClass = armorClass;
            ArmorWeight = armorWeight;
            Scores = scores ?? new AbilityScores();
            Attacks = attacks ?? Array.Empty<Attack>();
            Speed = Math.Max(0, speed);
            Challenge = challenge;
            Multiattack = Math.Clamp(multiattack, 1, ActionBudget.BaseActions);
            Instinct = instinct;
            Defenses = defenses ?? new Dictionary<DamageType, Defense>();
            Saves = saves ?? Array.Empty<Ability>();
            Skills = skills ?? Array.Empty<Skill>();
            Mini = mini ?? "";
            Tags = tags ?? Array.Empty<string>();
            HitDie = hitDie;
        }

        public string Id { get; }

        public int HitPoints { get; }

        public int ArmorClass { get; }

        public ArmorWeight ArmorWeight { get; }

        public AbilityScores Scores { get; }

        public IReadOnlyList<Attack> Attacks { get; }

        public int Speed { get; }

        // SRD's challenge rating. v1 uses it to size an encounter, not to grant anything
        public double Challenge { get; }

        // how many of its two actions it spends attacking. the multiattack primitive: a monster
        // with 2 here swings twice, which is what SRD's Multiattack line usually amounts to
        public int Multiattack { get; }

        public Instinct Instinct { get; }

        public IReadOnlyDictionary<DamageType, Defense> Defenses { get; }

        public IReadOnlyList<Ability> Saves { get; }

        public IReadOnlyList<Skill> Skills { get; }

        // which model stands for it. v1_minis_map.md stretches a handful of Quaternius minis
        // across many statblocks, so several monsters share one
        public string Mini { get; }

        // "undead", "beast", "humanoid" - what a Turn Undead or a favoured enemy would read
        public IReadOnlyList<string> Tags { get; }

        public Die HitDie { get; }

        public string NameKey => KeyConventions.MonsterName(Id);

        public string DescriptionKey =>
            KeyConventions.Key(KeyConventions.MonsterNs, Id, "description");

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;
        }

        // a fresh one, ready to be put on the board. every call makes a new Actor, because two
        // goblins in the same fight are two goblins
        public Actor Spawn(string id = null)
        {
            var actor = new Actor(id ?? Id, Math.Max(1, (int)Math.Ceiling(Challenge)),
                                  Scores.Copy(), Allegiance.Enemy);

            actor.SetHealth(new Health(HitPoints, HitDie,
                                       Math.Max(1, (int)Math.Ceiling(Challenge))));

            // the statblock's AC is a number, not a suit of armor: giving it as heavy armor makes
            // it exactly that number whatever the creature's Dexterity is
            actor.Armor = new ArmorProfile(ArmorWeight.Heavy, ArmorClass);
            actor.Speed = Speed;

            foreach (KeyValuePair<DamageType, Defense> defense in Defenses)
                actor.SetDefense(defense.Key, defense.Value);

            foreach (Ability save in Saves) actor.TrainSave(save);

            foreach (Skill skill in Skills) actor.Train(skill);

            return actor;
        }

        public ITactics Brain() => new BasicTactics(Attacks, Instinct);

        // what it swings with when something runs out of its reach
        public Attack Opportunity =>
            Attacks.Where(a => !a.IsRanged)
                   .OrderByDescending(a => a.Damage.Average)
                   .FirstOrDefault();

        public override string ToString() =>
            $"{Id} cr {Challenge}: {HitPoints} hp, ac {ArmorClass}, " +
            $"{Attacks.Count} attacks" +
            (Multiattack > 1 ? $" x{Multiattack}" : "") +
            (Instinct == Instinct.None ? "" : $", {Instinct}") +
            (Mini.Length > 0 ? $", mini {Mini}" : "");
    }

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

                attacks.Add(new Attack(name, damage, type,
                                       ability ?? Ability.Strength,
                                       true,
                                       raw.Number("reach", 1),
                                       raw.Number("range"),
                                       raw.Number("long_range"),
                                       Hand.None,
                                       raw.Flag("finesse"),
                                       raw.Number("attack_bonus"),
                                       raw.Number("damage_bonus")));
            }

            if (attacks.Count == 0) problems.Add($"{id}: a monster with nothing to attack with");

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

            Instinct instinct = Instinct.None;

            foreach (string tag in entry.Strings("instincts"))
            {
                switch (tag.ToLowerInvariant())
                {
                    case "finisher": instinct |= Instinct.Finisher; break;
                    case "cautious": instinct |= Instinct.Cautious; break;
                    case "skirmisher": instinct |= Instinct.Skirmisher; break;
                    case "stubborn": instinct |= Instinct.Stubborn; break;
                    default: problems.Add($"{id}: '{tag}' is not an instinct"); break;
                }
            }

            if (!DieExtensions.TryParse(entry.Text("hit_die", "d8"), out Die hitDie))
                problems.Add($"{id}: '{entry.Text("hit_die")}' is not a die");

            int multiattack = entry.Number("multiattack", 1);

            if (multiattack > ActionBudget.BaseActions)
                problems.Add($"{id}: multiattack {multiattack} - a monster has " +
                             $"{ActionBudget.BaseActions} actions like everybody else");

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
                               hitDie);
        }
    }

    public sealed class Bestiary
    {
        readonly Dictionary<string, Monster> _byId;

        public Bestiary(IEnumerable<Monster> monsters, IEnumerable<string> problems = null)
        {
            _byId = new Dictionary<string, Monster>(StringComparer.Ordinal);

            foreach (Monster monster in monsters ?? Enumerable.Empty<Monster>())
                if (monster != null)
                    _byId[monster.Id] = monster;

            Problems = (problems ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> Problems { get; }

        public bool Sound => Problems.Count == 0;

        public int Count => _byId.Count;

        public IEnumerable<Monster> All =>
            _byId.Values.OrderBy(m => m.Challenge).ThenBy(m => m.Id, StringComparer.Ordinal);

        public Monster Find(string id) =>
            id != null && _byId.TryGetValue(id, out Monster monster) ? monster : null;

        public bool Has(string id) => Find(id) != null;

        public IEnumerable<Monster> Around(double challenge, double spread = 1) =>
            All.Where(m => Math.Abs(m.Challenge - challenge) <= spread);

        public IEnumerable<Monster> Tagged(string tag) =>
            All.Where(m => m.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

        public IEnumerable<string> Keys() => All.SelectMany(m => m.Keys());

        public Bestiary With(IEnumerable<Monster> more) =>
            new Bestiary(_byId.Values.Concat(more ?? Enumerable.Empty<Monster>()), Problems);

        public static Bestiary Srd()
        {
            var monsters = new List<Monster>();
            var problems = new List<string>();

            foreach ((string path, string text) in Schema.Srd.ReadFolder("monsters"))
            {
                MonsterReader.TryRead(text, out IReadOnlyList<Monster> read,
                                      out IReadOnlyList<string> trouble);

                monsters.AddRange(read);
                problems.AddRange(trouble.Select(t => $"{path}: {t}"));
            }

            return new Bestiary(monsters, problems);
        }

        public override string ToString() =>
            $"{Count} monsters" + (Problems.Count > 0 ? $", {Problems.Count} problems" : "");
    }
}
