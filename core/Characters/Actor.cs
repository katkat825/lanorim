using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Core.Characters
{
    public enum Allegiance
    {
        Hero,
        Enemy,

        // an ally the campaign runs, and the companion - neither is controlled by the player
        Friendly,
    }

    // anything that can be rolled for: the hero, a goblin, a summoned spirit. core knows nothing
    // about classes, species or items - the content layer assembles one of these and hands it over.
    public sealed class Actor
    {
        public Actor(string id, int level = 1, AbilityScores scores = null, Allegiance side = Allegiance.Enemy)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Level = Proficiency.Clamp(level);
            Scores = scores ?? new AbilityScores();
            Side = side;
            Health = new Health(1);
        }

        public string Id { get; }

        public Allegiance Side { get; }

        public int Level { get; private set; }

        public AbilityScores Scores { get; }

        public Health Health { get; private set; }

        // SRD 5.2.1 default; a species or an effect moves it
        public int Speed { get; set; } = 30;

        public int ProficiencyBonus => Proficiency.Bonus(Level);

        public string NameKey => KeyConventions.ActorName(Id);

        public void SetLevel(int level) => Level = Proficiency.Clamp(level);

        public void SetHealth(Health health) =>
            Health = health ?? throw new ArgumentNullException(nameof(health));


        // --- training -------------------------------------------------------------------------

        readonly Dictionary<Skill, Training> _skills = new();
        readonly HashSet<Ability> _saves = new();

        public Training TrainingIn(Skill skill) =>
            _skills.TryGetValue(skill, out Training t) ? t : Training.Untrained;

        public void Train(Skill skill, Training training = Training.Proficient)
        {
            if (skill == Skill.None) return;

            // never demotes: a Rogue's Expertise landing after the background's proficiency is the
            // normal order, and the reverse order must not undo it
            if (TrainingIn(skill) < training) _skills[skill] = training;
        }

        public IEnumerable<Skill> TrainedSkills =>
            Skills.All.Where(s => TrainingIn(s) != Training.Untrained);

        public bool SavesWith(Ability ability) => _saves.Contains(ability);

        public void TrainSave(Ability ability) => _saves.Add(ability);

        public IEnumerable<Ability> SaveProficiencies => Abilities.All.Where(SavesWith);


        // --- modifiers ------------------------------------------------------------------------

        // a flat bonus that isn't armor, ability or proficiency: a Bless, a species trait, a ring
        readonly Dictionary<Skill, int> _skillBonus = new();
        readonly Dictionary<Ability, int> _saveBonus = new();

        public void AddSkillBonus(Skill skill, int bonus)
        {
            if (skill == Skill.None) return;

            _skillBonus[skill] = SkillBonus(skill) + bonus;
        }

        public int SkillBonus(Skill skill) =>
            _skillBonus.TryGetValue(skill, out int b) ? b : 0;

        public void AddSaveBonus(Ability ability, int bonus) =>
            _saveBonus[ability] = SaveBonus(ability) + bonus;

        public int SaveBonus(Ability ability) =>
            _saveBonus.TryGetValue(ability, out int b) ? b : 0;

        public int AbilityModifier(Ability ability) => Scores.Modifier(ability);

        // every timed modifier riding on this actor - a Bless, a potion, a class stance
        public Boons Boons { get; } = new Boons();

        public int CheckModifier(Skill skill) =>
            skill == Skill.None
                ? Boons.FlatOnCheck(skill)
                : Scores.Modifier(skill.Governs()) +
                  Proficiency.Applied(Level, TrainingIn(skill)) +
                  SkillBonus(skill) +
                  Boons.FlatOnCheck(skill);

        // a raw ability check - no skill named, so no proficiency
        public int CheckModifier(Ability ability) =>
            Scores.Modifier(ability) + Boons.FlatOnCheck(Skill.None);

        public int SaveModifier(Ability ability) =>
            Scores.Modifier(ability) +
            (SavesWith(ability) ? ProficiencyBonus : 0) +
            SaveBonus(ability) +
            Boons.FlatOnSave(ability);


        // --- armor ----------------------------------------------------------------------------

        public ArmorProfile Armor { get; set; } = ArmorProfile.Unarmored;

        public bool HasShield { get; set; }

        // Unarmored Defense: the Barbarian's CON, a Monk's WIS. null means "use the armor".
        public Ability? UnarmoredDefense { get; set; }

        public int ArmorClassBonus { get; set; }

        public int ArmorClass
        {
            get
            {
                int dex = AbilityModifier(Ability.Dexterity);

                int from = UnarmoredDefense.HasValue && Armor.Weight == ArmorWeight.None
                    ? 10 + dex + AbilityModifier(UnarmoredDefense.Value)
                    : Armor.ArmorClass(dex);

                return from + (HasShield ? ArmorWeights.ShieldBonus : 0) + ArmorClassBonus +
                       Boons.ArmorClass;
            }
        }


        // --- conditions -----------------------------------------------------------------------

        readonly HashSet<Condition> _conditions = new();

        public IReadOnlyCollection<Condition> Conditions => _conditions;

        public bool Has(Condition condition) => _conditions.Contains(condition);

        public bool Apply(Condition condition) =>
            condition != Condition.None && _conditions.Add(condition);

        public bool Remove(Condition condition) => _conditions.Remove(condition);

        public void ClearConditions() => _conditions.Clear();

        public bool IsIncapacitated => _conditions.Any(c => c.Incapacitates());

        public bool IsRooted => _conditions.Any(c => c.Roots());

        public bool IsDown => Health.IsDown;

        // down is not dead. a hero at 0 hit points is still in the fight until the single d20 >= 10
        // says otherwise, and the fight is not lost while that die is still to be thrown.
        public bool IsDead { get; private set; }

        public void Perish()
        {
            IsDead = true;
            Health.Kill();
            Apply(Condition.Unconscious);
        }

        public bool CanAct => !IsDown && !IsIncapacitated;

        public Advantage AttackAdvantage =>
            Advantages.Of(Boons.AnyAdvantageOnAttacks,
                          Boons.AnyDisadvantageOnAttacks ||
                          _conditions.Any(c => c.AttacksAtDisadvantage()));

        public Advantage CheckAdvantage =>
            Advantages.Of(Boons.AnyAdvantageOnChecks,
                          Boons.AnyDisadvantageOnChecks ||
                          _conditions.Any(c => c.ChecksAtDisadvantage()));

        public Advantage AdvantageAgainstMe =>
            Advantages.Of(_conditions.Any(c => c.GrantsAdvantageToAttackers()), false);


        // --- damage ---------------------------------------------------------------------------

        readonly Dictionary<DamageType, Defense> _defenses = new();

        public Defense DefenseAgainst(DamageType type) =>
            _defenses.TryGetValue(type, out Defense d) ? d : Defense.Normal;

        public void SetDefense(DamageType type, Defense defense)
        {
            if (type == DamageType.None) return;

            _defenses[type] = defense;
        }

        public IReadOnlyDictionary<DamageType, Defense> Defenses => _defenses;

        // the single path damage takes into an actor: the multiplier, then the hit points, then
        // the unconscious condition. nothing else may call Health.Take on its own.
        public int Suffer(int amount, DamageType type)
        {
            int after = DefenseAgainst(type).Apply(amount);
            int taken = Health.Take(after);

            if (Health.IsDown) Apply(Condition.Unconscious);

            return taken;
        }

        public int Mend(int amount)
        {
            // nothing heals the dead; a raise is a separate thing and it clears the flag itself
            if (IsDead) return 0;

            int healed = Health.Heal(amount);

            if (!Health.IsDown) Remove(Condition.Unconscious);

            return healed;
        }


        // --- magic ----------------------------------------------------------------------------

        // AN ACTOR NO LONGER HOLDS A SPELL RESOURCE. It used to carry one mana pool, because there
        // was only ever one way to pay; the resource is now the player's choice between slots and
        // points, so it lives on the Caster beside the spells it pays for (Core.Magic.Caster).
        // A goblin has neither and carries neither.

        // binary: held until you end it or you go down. no CON save on damage
        // (decisions_checklist.md section 6).
        public string Concentrating { get; private set; }

        public bool IsConcentrating => !string.IsNullOrEmpty(Concentrating);

        public string Concentrate(string spellId)
        {
            string dropped = Concentrating;
            Concentrating = spellId;
            return dropped;
        }

        public string EndConcentration()
        {
            string dropped = Concentrating;
            Concentrating = null;
            return dropped;
        }


        // --- rest -----------------------------------------------------------------------------

        public void ShortRest()
        {
            Health.ShortRest();
            Boons.Rested();
            Scores.Rested();
        }

        public void Raise(int at = 1)
        {
            IsDead = false;
            Health.Revive(at);
            Remove(Condition.Unconscious);
        }

        public void LongRest()
        {
            if (IsDead) return;

            Health.LongRest();
            Scores.Rested();
            ClearConditions();
            EndConcentration();
            Boons.Rested();
        }

        public override string ToString() =>
            $"{Id} (level {Level} {Side.ToString().ToLowerInvariant()}) {Health}, ac {ArmorClass}" +
            (_conditions.Count > 0
                ? ", " + string.Join(" ", _conditions.Select(c => c.Id()))
                : "");
    }
}
