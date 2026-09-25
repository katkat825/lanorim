using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Rules;
using Core.Magic;
using Content.Spells;

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

        // SRD size category; one square on the v1 board whatever it is
        public Size Size { get; init; } = Size.Medium;

        // conditions it cannot have: a skeleton's Poisoned, a zombie's
        public IReadOnlyList<Condition> ConditionImmunities { get; init; } = Array.Empty<Condition>();

        // SPECIAL ACTIONS: a breath, a web, a frightful roar - each one a spell made of the same
        // primitives a hero's spells are, cast with the statblock's DC, and limited by a recharge
        // or so many a day (decisions_checklist.md section 6: save-or-condition and recharge)
        public IReadOnlyList<MonsterAction> Actions { get; init; } = Array.Empty<MonsterAction>();

        // SPELLCASTING: the statblock's list, cast through the ordinary spell engine
        public MonsterSpellcasting Spellcasting { get; init; }

        public bool Casts => Actions.Count > 0 || Spellcasting != null;

        // everything it can cast, ready to go: null for a monster that casts nothing. the SRD's
        // own spells come off the book; a special action is its own spell
        public Caster CasterFor(Actor actor, SpellBook book)
        {
            if (!Casts || actor == null) return null;

            Ability ability = Spellcasting?.Ability ?? Ability.Charisma;

            var caster = new Caster(actor, ability, new AtWill())
            {
                FixedDc = Spellcasting?.Dc,
                FixedAttack = Spellcasting?.AttackBonus,
            };

            foreach (MonsterAction action in Actions)
            {
                caster.Learn(action.Spell);
                caster.Limit(action.Spell.Id, new SpellUse(action.Recharge, action.PerDay));
            }

            if (Spellcasting != null && book != null)
            {
                foreach (string id in Spellcasting.AtWill)
                    if (book.Find(id) is Spell spell) caster.Learn(spell);

                foreach (KeyValuePair<string, int> daily in Spellcasting.PerDay)
                    if (book.Find(daily.Key) is Spell spell)
                    {
                        caster.Learn(spell);
                        caster.Limit(spell.Id, new SpellUse(0, daily.Value));
                    }
            }

            return caster;
        }

        // a special action's DC is its own: the caster uses the spellcasting DC for spells, and
        // an action carries its printed DC through this
        public MonsterAction ActionFor(string spellId) =>
            Actions.FirstOrDefault(a => a.Spell.Id == spellId);

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

            // what kind of creature it is, for the spells that care (Hold Person, Divine Smite)
            foreach (string tag in Tags) actor.Tag(tag);

            actor.Size = Size;

            foreach (Condition immune in ConditionImmunities) actor.MakeImmune(immune);

            return actor;
        }

        public ITactics Brain() => new BasicTactics(Attacks, Instinct);

        // the full brain: attacks, and its special actions and spells when they are the better
        // use of a turn (content/Combat/MonsterTactics)
        public ITactics Brain(Caster caster, Incantation incantation) =>
            incantation == null
                ? Brain()
                : new Combat.MonsterTactics(this, caster, incantation);

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
                    };

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
                                       raw.Number("damage_bonus"))
                            {
                                OnHit = riders,
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
                               hitDie)
            {
                Size = size,
                ConditionImmunities = immunities,
                Actions = actions,
                Spellcasting = casting,
            };
        }
    }

    // one special action on a statblock: what it does (a spell of primitives), and how often
    public sealed class MonsterAction
    {
        public MonsterAction(Spell spell, int recharge = 0, int perDay = 0, int dc = 0)
        {
            Spell = spell ?? throw new ArgumentNullException(nameof(spell));
            Recharge = Math.Clamp(recharge, 0, 6);
            PerDay = Math.Max(0, perDay);
            Dc = Math.Max(0, dc);
        }

        // its own printed DC; 0 is the statblock's spellcasting DC
        public int Dc { get; }

        public Spell Spell { get; }

        // 5 is "Recharge 5-6"
        public int Recharge { get; }

        public int PerDay { get; }

        public override string ToString() =>
            Spell.Id + (Recharge > 0 ? $" (recharge {Recharge}-6)" : "") +
            (PerDay > 0 ? $" ({PerDay}/day)" : "");
    }

    // a statblock's Spellcasting line: the ability, the DC and attack bonus it prints, and which
    // SRD spells at will and which so many a day
    public sealed class MonsterSpellcasting
    {
        public MonsterSpellcasting(Ability ability, int dc, int attackBonus,
                                   IReadOnlyList<string> atWill,
                                   IReadOnlyDictionary<string, int> perDay)
        {
            Ability = ability;
            Dc = dc;
            AttackBonus = attackBonus;
            AtWill = atWill ?? Array.Empty<string>();
            PerDay = perDay ?? new Dictionary<string, int>();
        }

        public Ability Ability { get; }

        public int Dc { get; }

        public int AttackBonus { get; }

        public IReadOnlyList<string> AtWill { get; }

        public IReadOnlyDictionary<string, int> PerDay { get; }

        public IEnumerable<string> SpellIds => AtWill.Concat(PerDay.Keys);
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
