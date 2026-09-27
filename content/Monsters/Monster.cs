using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
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
                       Multiattack multiattack = null,
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
            Multiattack = multiattack != null && multiattack.Count > 1 ? multiattack : null;
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

        // SRD 5.2.1 Multiattack: the attacks its one Attack action makes, exactly as the statblock
        // lists them - "one Bite attack and one Claw attack", "three Arcane Burst attacks". null
        // for a monster without one: its Attack action is a single attack. a monster does NOT get
        // the hero's two actions (decisions_checklist.md section 1, 2026-09-25)
        public Multiattack Multiattack { get; }

        public int AttacksPerTurn => Multiattack?.Count ?? 1;

        // a bonus action its statblock gives it that is a Dash, Disengage or Hide - the goblin's
        // Nimble Escape is Disengage or Hide
        public Manoeuvre BonusManoeuvres { get; init; }

        // the turn it plays: one action, and a bonus action only if the statblock has one - a
        // manoeuvre, or a spell it casts as a bonus action (a mage's Misty Step)
        public ActionBudget Budget(Caster caster = null) =>
            ActionBudget.Statblock(Multiattack,
                                   BonusManoeuvres != Manoeuvre.None ||
                                   caster != null &&
                                   caster.Known.Any(s => s.CastingTime == CastingTime.BonusAction));

        public Instinct Instinct { get; }

        public IReadOnlyDictionary<DamageType, Defense> Defenses { get; }

        public IReadOnlyList<Ability> Saves { get; }

        public IReadOnlyList<Skill> Skills { get; }

        public IReadOnlyList<Skill> Expertise { get; init; } = Array.Empty<Skill>();

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
        public Actor Spawn(string id = null, Allegiance side = Allegiance.Enemy)
        {
            var actor = new Actor(id ?? Id, Math.Max(1, (int)Math.Ceiling(Challenge)),
                                  Scores.Copy(), side);

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

            // a statblock's doubled skill: the goblin's Stealth +6, the wolf's Perception +5
            foreach (Skill skill in Expertise) actor.Train(skill, Training.Expert);

            // what kind of creature it is, for the spells that care (Hold Person, Divine Smite)
            foreach (string tag in Tags) actor.Tag(tag);

            actor.Size = Size;

            foreach (Condition immune in ConditionImmunities) actor.MakeImmune(immune);

            actor.QuickOnBonus = BonusManoeuvres;

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
            (Multiattack != null ? $" multiattack {Multiattack}" : "") +
            (Instinct == Instinct.None ? "" : $", {Instinct}") +
            (Mini.Length > 0 ? $", mini {Mini}" : "");
    }
}
