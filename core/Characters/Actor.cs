using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Core.Space;

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
            SetHealth(new Health(1));
        }

        public string Id { get; }

        public Allegiance Side { get; }

        public int Level { get; private set; }

        public AbilityScores Scores { get; }

        public Health Health { get; private set; }

        // SRD 5.2.1 default; a species or an effect moves it. what is added only out of heavy
        // armor (the Barbarian's Fast Movement) rides on top, so "Speed += n" keeps working
        public int Speed
        {
            get => _speed + LightFootedBonus - ArmorDrag;
            set => _speed = value - LightFootedBonus + ArmorDrag;
        }

        int _speed = 30;

        // SRD 5.2.1 Fast Movement (p.30): "while you aren't wearing Heavy armor"
        public int SpeedOutOfHeavyArmor { get; set; }

        int LightFootedBonus => Armor.Weight == ArmorWeight.Heavy ? 0 : SpeedOutOfHeavyArmor;

        // SRD 5.2.1 armor (p.92): armor with a Strength score costs 10 feet of Speed to a wearer
        // below it. a statblock's armor names none, so a monster never pays it
        int ArmorDrag =>
            Armor.StrengthRequirement > 0 && Scores.Score(Ability.Strength) < Armor.StrengthRequirement ? 10 : 0;

        // what kind of creature it is and what it is by nature: "humanoid", "undead", "construct"
        // from a statblock, "sleepless" from an elf's Trance. a spell that only works on some
        // creatures reads these (Hold Person, Divine Smite's fiends and undead)
        readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> Tags => _tags;

        public bool Is(string tag) => !string.IsNullOrEmpty(tag) && _tags.Contains(tag);

        public void Tag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag)) _tags.Add(tag.Trim());
        }

        // SRD size category. every creature is one square on the v1 board whatever its size (the
        // grid has no multi-square creatures); size is read by the spells that limit what they
        // can move (Telekinesis' "Huge or smaller")
        public Size Size { get; set; } = Size.Medium;

        // --- what it holds -----------------------------------------------------------------------

        // it dropped what it was holding (Command's Drop, Fear) or had it pulled away
        // (Telekinesis): an attack with a held weapon is not there until it is picked up again.
        // natural weapons - a claw, a bite - cannot be dropped
        public bool Disarmed { get; private set; }

        // where the dropped weapon lies; picking it up is the free object interaction SRD gives a
        // turn, from its square or beside it
        public Cell? DroppedAt { get; private set; }

        public void Disarm(Cell? at)
        {
            Disarmed = true;
            DroppedAt = at;
        }

        public bool Rearm()
        {
            if (!Disarmed) return false;

            Disarmed = false;
            DroppedAt = null;
            return true;
        }

        // whether this attack is one it can make now
        public bool CanUse(Attack attack) =>
            attack != null && !(Disarmed && attack.Hand != Hand.None);

        // the size it is right now: Enlarge makes it one bigger, Reduce one smaller
        public Size CurrentSize =>
            (Size)Math.Clamp((int)Size + Boons.SizeStep, (int)Size.Tiny, (int)Size.Gargantuan);

        public int ProficiencyBonus => Proficiency.Bonus(Level);

        public string NameKey => KeyConventions.ActorName(Id);

        public void SetLevel(int level) => Level = Proficiency.Clamp(level);

        public void SetHealth(Health health)
        {
            Health = health ?? throw new ArgumentNullException(nameof(health));
            Health.RaisedBy(() => Boons.MaxHitPoints);
        }


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
            Boons.FlatOnSave(ability) +
            AuraBonus;


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
                // a borrowed shape's AC is the statblock's number. the armor stays on the sheet
                // untouched underneath, which is why taking the shape off needs to restore nothing
                if (Shape != null) return Shape.ArmorClass + Boons.ArmorClass;

                int dex = AbilityModifier(Ability.Dexterity);

                int from = UnarmoredDefense.HasValue && Armor.Weight == ArmorWeight.None
                    ? 10 + dex + AbilityModifier(UnarmoredDefense.Value)
                    : Armor.ArmorClass(dex);

                // Mage Armor's 13 + Dex. SRD lets a creature with two ways of working out its
                // unarmored armor class pick one; picking the better is the only sensible pick
                if (Armor.Weight == ArmorWeight.None && Boons.UnarmoredBase > 0)
                    from = Math.Max(from, Boons.UnarmoredBase + dex);

                return from + (HasShield ? ArmorWeights.ShieldBonus : 0) + ArmorClassBonus +
                       (Armor.Weight != ArmorWeight.None ? ArmoredArmorClassBonus : 0) +
                       Boons.ArmorClass;
            }
        }


        // --- conditions -----------------------------------------------------------------------

        readonly HashSet<Condition> _conditions = new();

        // who put a condition there, where it matters: Charmed is about the charmer, Frightened
        // about the source of the fear. a condition with nobody behind it (0 HP) has no entry
        readonly Dictionary<Condition, List<Actor>> _sources = new();

        // conditions this creature cannot have: a statblock's condition immunities, a Petrified
        // body's immunity to Poisoned
        readonly HashSet<Condition> _immunities = new();

        public IReadOnlyCollection<Condition> Conditions => _conditions;

        public bool Has(Condition condition) => _conditions.Contains(condition);

        // whether it has the condition *because of* this creature - "charmed by you"
        public bool HasFrom(Condition condition, Actor source) =>
            source != null && _sources.TryGetValue(condition, out List<Actor> from) &&
            from.Contains(source);

        public IReadOnlyList<Actor> SourcesOf(Condition condition) =>
            _sources.TryGetValue(condition, out List<Actor> from)
                ? from
                : (IReadOnlyList<Actor>)Array.Empty<Actor>();

        public bool IsImmuneTo(Condition condition) =>
            _immunities.Contains(condition) || Boons.Immune(condition) ||
            condition == Condition.Poisoned && _conditions.Contains(Condition.Petrified);

        public void MakeImmune(Condition condition)
        {
            if (condition != Condition.None) _immunities.Add(condition);
        }

        public IReadOnlyCollection<Condition> Immunities => _immunities;

        public bool Apply(Condition condition, Actor source = null)
        {
            if (condition == Condition.None || IsImmuneTo(condition)) return false;

            bool added = _conditions.Add(condition);

            if (source != null)
            {
                if (!_sources.TryGetValue(condition, out List<Actor> from))
                    _sources[condition] = from = new List<Actor>();

                if (!from.Contains(source)) from.Add(source);
            }

            // SRD 5.2.1: Unconscious includes Prone, and "when this condition ends, you remain
            // Prone" - so the prone is its own condition, not a part that leaves with it
            if (added && condition == Condition.Unconscious) _conditions.Add(Condition.Prone);

            // Petrified: immunity to Poisoned, which ends one already there
            if (added && condition == Condition.Petrified) _conditions.Remove(Condition.Poisoned);

            // SRD 5.2.1: a Wild Shape ends when its wearer is incapacitated. 0 hit points is
            // unconscious, which incapacitates, so this one line is also "drop to 0 and revert"
            if (added && Shape != null && condition.Incapacitates()) Revert();

            return added;
        }

        // conditions the creature cannot end itself while a spell holds them: Hideous Laughter's
        // Prone. standing up asks this
        readonly HashSet<Condition> _pinned = new();

        public void Pin(Condition condition)
        {
            if (Has(condition)) _pinned.Add(condition);
        }

        public bool IsPinned(Condition condition) => _pinned.Contains(condition);

        public bool Remove(Condition condition)
        {
            _sources.Remove(condition);
            _pinned.Remove(condition);

            return _conditions.Remove(condition);
        }

        public void ClearConditions()
        {
            _conditions.Clear();
            _sources.Clear();
            _pinned.Clear();
        }

        public bool IsIncapacitated => _conditions.Any(c => c.Incapacitates());

        public bool IsRooted => _conditions.Any(c => c.Roots());

        public bool IsDown => Health.IsDown;

        // down is not dead. a hero at 0 hit points is still in the fight until the single d20 >= 10
        // says otherwise, and the fight is not lost while that die is still to be thrown.
        public bool IsDead { get; private set; }

        public void Perish()
        {
            IsDead = true;
            Stable = false;
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

        // the same, for one particular check: a boon can lean on a single skill or a single
        // ability (Hex), and the blanket answer above cannot see that
        public Advantage CheckAdvantageFor(Ability ability, Skill skill) =>
            Advantages.Of(Boons.AdvantageOnCheck(ability, skill) ||
                          HasAdvantage("check:" + ability.Id()) ||
                          skill != Skill.None && HasAdvantage("skill:" + skill.Id()),
                          Boons.DisadvantageOnCheck(ability, skill) ||
                          _conditions.Any(c => c.ChecksAtDisadvantage()));

        // --- what a feature grants that isn't a number (2026-09-25, the SRD check) ----------------

        // standing advantages, keyed "save:dex", "check:str", "skill:athletics", "initiative":
        // Danger Sense, Remarkable Athlete. some hold only while the creature can act
        readonly Dictionary<string, bool> _advantages = new(StringComparer.OrdinalIgnoreCase);

        public void GrantAdvantage(string key, bool unlessIncapacitated = false)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            // "unless incapacitated" only narrows: a second grant without it widens it back
            _advantages[key] = _advantages.TryGetValue(key, out bool was) ? was && unlessIncapacitated
                                                                           : unlessIncapacitated;
        }

        public bool HasAdvantage(string key) =>
            _advantages.TryGetValue(key, out bool unlessIncapacitated) &&
            !(unlessIncapacitated && IsIncapacitated);

        public bool InitiativeAdvantage => HasAdvantage("initiative");

        // the lowest natural roll that crits with a weapon or an Unarmed Strike: Improved and
        // Superior Critical (SRD 5.2.1 p.49)
        public int CritOn { get; set; } = 20;

        // Aura of Protection: saves add this ability's modifier, at least +1, while the creature
        // can act (SRD 5.2.1 p.55)
        public Ability? AuraAbility { get; set; }

        public int AuraBonus =>
            AuraAbility.HasValue && !IsIncapacitated ? Math.Max(1, AbilityModifier(AuraAbility.Value)) : 0;

        // the Defense fighting style: armor class while wearing armor (SRD 5.2.1 p.88)
        public int ArmoredArmorClassBonus { get; set; }

        // Indomitable: rerolls of a failed save, with a bonus, until the next long rest
        public int SaveRerolls { get; set; }

        public int SaveRerollBonus { get; set; }

        // the one boon an attack is giving up the advantage of: Brutal Strike forgoes Reckless
        // Attack's (SRD 5.2.1 p.29)
        public string ForgoingAdvantageFrom { get; set; }

        // what attack rolls at it lean on, from within 5 feet. an outlined creature gets nothing
        // from being unseen - Faerie Fire's rule, and the only thing that makes a boon's own
        // "disadvantage against" not count
        public Advantage AdvantageAgainstMe => AdvantageAgainstMeFrom(close: true);

        // the two halves of an attack roll's lean, kept apart so that one advantage and one
        // disadvantage anywhere in the attack cancel however many sources each side has. SRD's
        // rule; Advantage.And across three already-cancelled answers could not keep it
        public (bool advantage, bool disadvantage) AttackLeans => AttackLeansWith(null);

        // the same, for one attack: a Strength-only advantage needs a Strength attack
        public (bool advantage, bool disadvantage) AttackLeansWith(Attack attack) =>
            (Boons.AdvantageOnAttackWith(attack?.AbilityFor(this), ForgoingAdvantageFrom),
             Boons.AnyDisadvantageOnAttacks || _conditions.Any(c => c.AttacksAtDisadvantage()) ||
             Unwieldy(attack));

        // SRD 5.2.1 Heavy (p.89): Strength below 13 for a Heavy melee weapon, Dexterity below 13
        // for a Heavy ranged one
        bool Unwieldy(Attack attack) =>
            attack != null && attack.Heavy &&
            Scores.Score(attack.IsRanged ? Ability.Dexterity : Ability.Strength) < 13;

        // the same, for one attacker that sees (or doesn't see) this creature
        public (bool advantage, bool disadvantage) LeansAgainstMe(bool close, Actor attacker,
                                                                 bool attackerSees) =>
            (Boons.AdvantageAgainstFrom(attackerSees) ||
             _conditions.Any(c => c.GrantsAdvantageToAttackers()) ||
             close && Has(Condition.Prone),
             Boons.DisadvantageAgainstFrom(attacker) && !Boons.Exposed ||
             !close && Has(Condition.Prone));

        public (bool advantage, bool disadvantage) LeansAgainstMe(bool close) =>
            (Boons.AnyAdvantageAgainst || _conditions.Any(c => c.GrantsAdvantageToAttackers()) ||
             close && Has(Condition.Prone),
             Boons.AnyDisadvantageAgainst && !Boons.Exposed || !close && Has(Condition.Prone));

        // SRD 5.2.1 Prone: advantage for an attacker within 5 feet, disadvantage for one further off
        public Advantage AdvantageAgainstMeFrom(bool close) =>
            Advantages.Of(Boons.AnyAdvantageAgainst ||
                          _conditions.Any(c => c.GrantsAdvantageToAttackers()) ||
                          close && Has(Condition.Prone),
                          Boons.AnyDisadvantageAgainst && !Boons.Exposed ||
                          !close && Has(Condition.Prone));

        public Advantage SaveAdvantage(Ability ability) =>
            Advantages.Of(Boons.AdvantageOnSave(ability) || HasAdvantage("save:" + ability.Id()),
                          Boons.DisadvantageOnSave(ability) ||
                          _conditions.Any(c => c.SavesAtDisadvantage(ability)));


        // --- sight ------------------------------------------------------------------------------

        // SRD 5.2.1's Blinded and Invisible, as the one question both come down to. what the board
        // adds - walls, heavily obscured squares - is the fight's to ask (Encounter.Sees)
        public bool CanSee(Actor other)
        {
            if (other == null || ReferenceEquals(other, this)) return true;

            if (Has(Condition.Blinded)) return false;

            if (!other.Has(Condition.Invisible)) return true;

            return other.Boons.Exposed || Boons.Truesight;
        }

        public bool IsInvisible => Has(Condition.Invisible);

        // Disintegrate's gray dust: dead, and past anything v1 has that brings the dead back
        public bool Dust { get; private set; }

        public void TurnToDust() => Dust = true;

        // flying: difficult ground and zones on the ground don't touch it (2026-09-25)
        public bool IsFlying => Boons.FlySpeed > 0;

        // what a turn actually gets to walk: the speed, and whatever is slowing or hastening it
        public int Moves
        {
            get
            {
                if (Boons.SpeedZero) return 0;

                // a Fly Speed is its speed for as long as it flies (Fly, Gaseous Form)
                int feet = IsFlying ? Boons.FlySpeed : Math.Max(0, Speed + Boons.Speed);

                // SRD doubling and halving; both at once is neither
                if (Boons.SpeedDoubled && !Boons.SpeedHalved) feet *= 2;
                else if (Boons.SpeedHalved && !Boons.SpeedDoubled) feet /= 2;

                return feet;
            }
        }

        // which of Dash, Disengage and Hide this actor may spend a bonus action on. empty for
        // nearly everybody; the Rogue's Cunning Action fills it
        public Manoeuvre QuickOnBonus { get; set; }


        // --- damage ---------------------------------------------------------------------------

        readonly Dictionary<DamageType, Defense> _defenses = new();

        // what the actor always has, and what a spell is lending it. a lent resistance never makes
        // things worse: it cancels a vulnerability, makes normal damage halved, and leaves an
        // immunity alone
        public Defense DefenseAgainst(DamageType type)
        {
            Defense own = _defenses.TryGetValue(type, out Defense d) ? d : Defense.Normal;

            // SRD 5.2.1 Petrified: resistance to all damage
            if (!Boons.Resist(type) && !Has(Condition.Petrified)) return own;

            return own switch
            {
                Defense.Vulnerable => Defense.Normal,
                Defense.Normal => Defense.Resistant,
                _ => own,
            };
        }

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

            // Death Ward: the first drop to 0 is a drop to 1, and the ward is spent
            Boon ward = Boons.DeathWard;

            if (ward != null && after > 0 && after >= Health.Current + Health.Temporary &&
                Health.Current > 0)
            {
                Boons.Remove(ward);

                int kept = Health.Current - 1;
                int soaked = Health.Temporary;

                Health.Take(soaked + kept);
                Stable = false;

                return kept;
            }

            int taken = Health.Take(after);

            // SRD 5.2.1: a Stable creature that takes damage is no longer Stable
            if (after > 0) Stable = false;

            if (Health.IsDown) Apply(Condition.Unconscious);

            return taken;
        }

        // SRD 5.2.1 Stable: at 0 hit points but no longer rolling death saves. Spare the Dying.
        // taking damage ends it; healing above 0 makes it moot
        public bool Stable { get; private set; }

        public bool Stabilize()
        {
            if (!IsDown || IsDead || Stable) return false;

            Stable = true;
            return true;
        }

        public int Mend(int amount)
        {
            // nothing heals the dead; a raise is a separate thing and it clears the flag itself
            if (IsDead) return 0;

            bool wasDown = Health.IsDown;
            int healed = Health.Heal(amount);

            // healing wakes the dying, not the sleeping - a Sleep spell's Unconscious is not the
            // 0-hit-points one and healing does not end it
            if (wasDown && !Health.IsDown) Remove(Condition.Unconscious);

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


        // --- borrowed shapes ------------------------------------------------------------------

        // the body the actor is wearing that is not its own, or null. while it is set the
        // physical scores, the armor class and the speed are the shape's; everything else is not
        public Shape Shape { get; private set; }

        public bool IsShifted => Shape != null;

        // what the shape covered up, kept so taking it off puts back exactly what was there
        readonly int[] _ownBody = new int[3];
        int _ownSpeed;

        public bool Assume(Shape shape)
        {
            if (shape == null) return false;

            // one borrowed body at a time: a second shape goes on over the actor, not the first
            Revert();

            for (int i = 0; i < Shape.Physical.Count; i++)
            {
                Ability ability = Shape.Physical[i];

                _ownBody[i] = Scores.Base(ability);
                Scores.SetBase(ability, shape.Score(ability));
            }

            _ownSpeed = Speed;
            Speed = shape.Speed;

            // the creature's training rides on as a boon rather than as Train, because Train never
            // demotes - and a thing that cannot be taken off cannot be worn. a skill the actor is
            // already trained in gains nothing: SRD 5.2.1 keeps the better of the two
            foreach (Skill skill in shape.Skills)
                if (TrainingIn(skill) == Training.Untrained)
                    Boons.Add(new Boon(shape.Source, shape.Source, Duration.Rest,
                                       ProficiencyBonus, checks: true, skill: skill));

            Shape = shape;
            return true;
        }

        // back to the actor's own body. returns the shape that came off, or null if none was on
        public Shape Revert()
        {
            Shape was = Shape;

            if (was == null) return null;

            Shape = null;

            for (int i = 0; i < Shape.Physical.Count; i++)
                Scores.SetBase(Shape.Physical[i], _ownBody[i]);

            Speed = _ownSpeed;

            Boons.EndFrom(was.Source);

            return was;
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
            // SRD 5.2.1: a rest needs at least 1 hit point to start
            if (IsDead || IsDown) return;

            Health.LongRest();
            Scores.Rested();
            ClearConditions();
            EndConcentration();
            Boons.LongRested();
        }

        public override string ToString() =>
            $"{Id} (level {Level} {Side.ToString().ToLowerInvariant()}) {Health}, ac {ArmorClass}" +
            (_conditions.Count > 0
                ? ", " + string.Join(" ", _conditions.Select(c => c.Id()))
                : "");
    }
}
