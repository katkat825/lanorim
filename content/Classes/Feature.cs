using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Magic;
using Core.Dice;
using Core.Localization;
using Core.Rules;

namespace Content.Classes
{
    // what a class or species feature can be. a closed list, exactly like the spell primitives and
    // for the same reason: features are data, and data that can say anything is code.
    // decisions_checklist.md section 6 calls class features the second-biggest cost; this is the
    // cheap path - implement the shapes, not the features.
    public enum Trait
    {
        // extra damage or a condition on a hit. Sneak Attack, Divine Smite, Improved Critical
        Rider,

        // a boon on yourself you switch on. Rage, a Paladin's aura, Reckless Attack
        Stance,

        // hit points back, a limited number of times. Second Wind, Lay on Hands
        Recovery,

        // an extra action or reaction, each round or once per rest. Extra Attack is one every
        // round, Action Surge one per rest
        ActionGrant,

        // armor class from an ability instead of armor. the Barbarian's Constitution
        UnarmoredDefense,

        // double proficiency on a number of skills. the Rogue's
        Expertise,

        // resistance or immunity to a damage type. the Dwarf's poison, the Dragonborn's ancestry
        Resistance,

        // makes the character a caster: the ability, the progression, the spell list
        Spellcasting,

        // more squares per turn. the Wood Elf's
        Speed,

        // a trained skill, a trained save, or a permanent flat bonus
        Training,

        // at 0 hit points, stay up instead. the Orc's Relentless Endurance, once per rest
        DeathIntercept,

        // a curated shape to turn into. the Druid's Wild Shape, as form cards
        Shape,

        // authored: the campaign decides what it means. Trance, Stonecunning's secrets
        Narrate,

        // a failed saving throw rerolled, with a bonus, so many times a long rest: Indomitable
        Reroll,

        // a bonus action for a Dash and temporary hit points equal to the proficiency bonus, so
        // many times a rest: the Orc's Adrenaline Rush
        Boost,

        // lets a bonus action be spent on Dash, Disengage or Hide. the Rogue's Cunning Action -
        // what the bonus action can do, not how many there are
        Nimble,
    }

    // which part of the turn an ActionGrant adds to. the base two actions are solo compensation,
    // not a stand-in for Extra Attack, so a whole extra action every round is allowed - it is
    // exactly what Extra Attack ports as (v1_class_roster.md, corrected 2026-09-23). the one limit
    // is ActionBudget's guardrail on how many a single turn can hold.
    public enum Grants
    {
        Action,
        BonusAction,
        Reaction,
    }

    // when a Rider fires
    public enum When
    {
        Always,

        // you had advantage on the attack - the Rogue's setup, expressed as a rule rather than a
        // second "is anybody next to them" system
        WithAdvantage,

        OnCritical,

        // you spent the resource to make it happen. Divine Smite
        WhenSpent,
    }

    // what a rest gives back of a limited feature (SRD 5.2.1)
    public enum Recharge
    {
        // every use on a short or a long rest
        Short,

        // one use on a short rest, every use on a long rest: Rage, Second Wind, Channel Divinity,
        // Wild Shape
        ShortOne,

        // only a long rest: Lay on Hands, Indomitable
        Long,
    }

    public sealed class Feature
    {
        public Feature(string id, Trait trait, int level = 1,
                       DiceRoll amount = default, DiceRoll perLevel = default,
                       DamageType damageType = DamageType.None,
                       Condition condition = Condition.None,
                       Ability? ability = null,
                       Defense defense = Defense.Resistant,
                       When when = When.Always, Grants grants = Grants.Action,
                       int flat = 0, int uses = 0, int count = 0, int perLevels = 1,
                       Duration duration = Duration.Encounter,
                       IReadOnlyList<Skill> skills = null,
                       IReadOnlyList<Ability> saves = null,
                       Core.Magic.Sways touches = Core.Magic.Sways.None,
                       CasterProgression progression = CasterProgression.None,
                       string note = null,
                       Manoeuvre manoeuvres = Manoeuvre.None)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Trait = trait;
            Level = Math.Clamp(level, 1, 20);
            Amount = amount;
            PerLevel = perLevel;
            DamageType = damageType;
            Condition = condition;
            Ability = ability;
            Defense = defense;
            When = when;
            Grants = grants;
            Flat = flat;
            Uses = uses;
            Count = count;
            PerLevels = Math.Max(1, perLevels);
            Duration = duration;
            Skills = skills ?? Array.Empty<Skill>();
            Saves = saves ?? Array.Empty<Ability>();
            Touches = touches;
            Progression = progression;
            Note = note ?? "";
            Manoeuvres = manoeuvres;
        }

        public string Id { get; }

        // what owning it makes the creature, as far as a spell can tell: an elf's Trance is
        // "sleepless" (magic can't put it to sleep). the engine also reads a few as rules:
        // evasion, reliable_talent, potent_cantrip, empowered_evocation, disciple_of_life,
        // blessed_healer, supreme_healing, indomitable_might
        public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

        // --- 2026-09-25, the SRD check ----------------------------------------------------------------

        // a rider that fires once on each of your turns, on a hit: Sneak Attack, Divine Strike
        public bool OncePerTurn { get; init; }

        // a rider or an intercept that works only while these stances are up: Frenzy while
        // raging and reckless, Relentless Rage while raging
        public IReadOnlyList<string> WhileStances { get; init; } = Array.Empty<string>();

        // the weapons a rider rides on: "finesse_or_ranged" (Sneak Attack), "melee" (Radiant
        // Strikes). empty is any
        public string Weapon { get; init; } = "";

        // a rider that gives up this stance's advantage on the attack it rides: Brutal Strike
        // forgoes Reckless Attack's
        public string Forgoes { get; init; } = "";

        // uses by level, where the SRD's table grows: Rage 2, 3, 4, 5, 6
        public IReadOnlyDictionary<int, int> UsesByLevel { get; init; } = new Dictionary<int, int>();

        public Recharge Recharge { get; init; } = Recharge.Short;

        // the pool a use comes out of: Sacred Weapon and Preserve Life spend Channel Divinity
        public string Spends { get; init; } = "";

        // standing advantages: "save:dex", "check:str", "skill:athletics", "initiative"
        public IReadOnlyList<string> AdvantageOn { get; init; } = Array.Empty<string>();

        public bool UnlessIncapacitated { get; init; }

        // a critical hit on this natural roll or higher: Improved Critical's 19
        public int CritOn { get; init; }

        // Aura of Protection: saves add this ability's modifier, at least +1
        public Ability? AuraAbility { get; init; }

        // the Defense fighting style's armor class, while wearing armor
        public int ArmoredArmorClass { get; init; }

        // conditions it makes the creature immune to: Aura of Courage's Frightened
        public IReadOnlyList<Condition> Immune { get; init; } = Array.Empty<Condition>();

        // a stance's flat bonus by level, where it doesn't grow evenly: Rage's +2, +3, +4
        public IReadOnlyDictionary<int, int> FlatByLevel { get; init; } = new Dictionary<int, int>();

        // an amount by level, where it doesn't grow evenly: Frenzy's 2d6, 3d6, 4d6
        public IReadOnlyDictionary<int, DiceRoll> AmountByLevel { get; init; } = new Dictionary<int, DiceRoll>();

        // a stance's flat bonus is this ability's modifier, at least +1: Sacred Weapon's Charisma
        public Ability? FlatAbility { get; init; }

        // a stance's resistances: Rage's Bludgeoning, Piercing and Slashing
        public IReadOnlyList<DamageType> Resists { get; init; } = Array.Empty<DamageType>();

        // a stance's advantages: "str_attacks", "str_checks", "str_saves"
        public IReadOnlyList<string> StanceAdvantage { get; init; } = Array.Empty<string>();

        // a stance's damage and attack advantage only for Strength attacks: Rage, Reckless Attack
        public bool StrengthOnly { get; init; }

        // a stance that gives attackers advantage against you: Reckless Attack
        public bool AdvantageAgainst { get; init; }

        // what switching it on costs: a bonus action (Rage, Second Wind), an action (Preserve
        // Life) or nothing (Reckless Attack, Sacred Weapon)
        public Core.Combat.Spend Cost { get; init; } = Core.Combat.Spend.Bonus;

        // a reaction it gives: "halve" is Uncanny Dodge
        public string Reaction { get; init; } = "";

        // spells always prepared, by the level they come at: a Life Domain's, Paladin's Smite's.
        // names the SRD gives that v1 doesn't build are left out (they are reference cards)
        public IReadOnlyDictionary<int, IReadOnlyList<string>> Spells { get; init; } =
            new Dictionary<int, IReadOnlyList<string>>();

        // casts each long rest that cost no slot: Paladin's Smite's one Divine Smite
        public IReadOnlyDictionary<string, int> FreeCasts { get; init; } = new Dictionary<string, int>();

        // cantrips and prepared spells by level, on a spellcasting feature (SRD's class tables)
        public IReadOnlyDictionary<int, int> CantripsByLevel { get; init; } = new Dictionary<int, int>();

        public IReadOnlyDictionary<int, int> KnownByLevel { get; init; } = new Dictionary<int, int>();

        // one more skill to pick from the class list: Primal Knowledge
        public int SkillPicks { get; init; }

        // the skills an Expertise may choose from: Scholar's six
        public IReadOnlyList<Skill> ExpertiseFrom { get; init; } = Array.Empty<Skill>();

        // an intercept's save: Relentless Rage's DC 10 Constitution, 5 more each time, back on 2 x
        // the level. 0 is no save (the Orc's Relentless Endurance)
        public int SaveDc { get; init; }

        public int DcStep { get; init; }

        public int HitPointsPerLevel { get; init; }

        // a recovery that only a Bloodied creature can take, up to half its hit points: Preserve
        // Life
        public bool OnlyBloodied { get; init; }

        public bool CapHalf { get; init; }

        // the hit point maximum rises this much each level: Dwarven Toughness
        public int MaxHitPointsPerLevel { get; init; }

        // a Speed feature that only counts out of heavy armor: Fast Movement
        public bool NotInHeavyArmor { get; init; }

        // a spell of the species' own, inline in its data (the Dragonborn's Breath Weapon), cast
        // uses_by_level times a long rest
        public Spell InnateSpell { get; init; }

        // the ability a species' spells are cast with: the highest of these three, when it names
        // them (SRD 5.2.1 lets the player choose Intelligence, Wisdom or Charisma)
        public IReadOnlyList<Ability> SpellAbilities { get; init; } = Array.Empty<Ability>();

        // the value a table holds at this level: the entry for the highest level at or below it
        public static T At<T>(IReadOnlyDictionary<int, T> table, int level, T otherwise)
        {
            T found = otherwise;
            int best = int.MinValue;

            foreach (KeyValuePair<int, T> entry in table)
                if (entry.Key <= level && entry.Key > best)
                {
                    best = entry.Key;
                    found = entry.Value;
                }

            return found;
        }

        public int UsesAt(int level) => At(UsesByLevel, level, Uses);

        public int FlatAt(int level) => At(FlatByLevel, level, Flat);

        public int CantripsAt(int level) => At(CantripsByLevel, level, -1);

        public int KnownAt(int level) => At(KnownByLevel, level, -1);

        public IEnumerable<string> SpellsAt(int level) =>
            Spells.Where(s => s.Key <= level).SelectMany(s => s.Value);

        // for a Nimble feature, which of Dash, Disengage and Hide the bonus action may do
        public Manoeuvre Manoeuvres { get; }

        public Trait Trait { get; }

        // the class level it arrives at. milestone levelling turns it on and never off
        public int Level { get; }

        public DiceRoll Amount { get; }

        // what each step past Level adds; Sneak Attack's ladder in two fields
        public DiceRoll PerLevel { get; }

        // how many class levels one step takes. Sneak Attack is a d6 every two levels, so the
        // interval is the difference between a rogue and a cleric's divine strike
        public int PerLevels { get; }

        public DamageType DamageType { get; }

        public Condition Condition { get; }

        public Ability? Ability { get; }

        public Defense Defense { get; }

        public When When { get; }

        public Grants Grants { get; }

        public int Flat { get; }

        // how many times between rests; 0 is unlimited
        public int Uses { get; }

        // how many skills an Expertise picks, how many forms a Shape has
        public int Count { get; }

        public Duration Duration { get; }

        public IReadOnlyList<Skill> Skills { get; }

        public IReadOnlyList<Ability> Saves { get; }

        public Core.Magic.Sways Touches { get; }

        // HOW FAST THIS CASTER CLIMBS THE SPELL LEVELS, and the only spellcasting number a class
        // data file carries now. It used to be two - mana_per_level and mana_flat - because there
        // was one pool and its size was the whole resource. A named progression serves BOTH modes:
        // the slot table reads it directly, and the point pool derives from it.
        public CasterProgression Progression { get; }

        public string Note { get; }

        public string NameKey => KeyConventions.FeatureName(Id);

        public string DescriptionKey => KeyConventions.FeatureDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;

            // a species' own spell (the Dragonborn's breath) has its own card
            if (InnateSpell != null)
                foreach (string key in InnateSpell.Keys()) yield return key;
        }

        // a feature that does something when the player asks, rather than sitting on the sheet
        public bool IsActive =>
            Trait == Trait.Stance || Trait == Trait.Recovery || Trait == Trait.Shape || Trait == Trait.Boost ||
            (Trait == Trait.Rider && When == When.WhenSpent);

        public DiceRoll AmountAt(int level)
        {
            if (AmountByLevel.Count > 0) return At(AmountByLevel, level, Amount);

            if (PerLevel.IsNothing || level <= Level) return Amount;

            int steps = (level - Level) / PerLevels;

            if (steps <= 0) return Amount;

            return new DiceRoll(Amount.Count + PerLevel.Count * steps,
                                Amount.RollsAnything ? Amount.Die : PerLevel.Die,
                                Amount.Modifier + PerLevel.Modifier * steps);
        }

        // the passive half: everything a feature does the moment it is owned
        public void Grant(Actor actor, int level)
        {
            if (actor == null || level < Level) return;

            foreach (string tag in Tags) actor.Tag(tag);

            foreach (string key in AdvantageOn) actor.GrantAdvantage(key, UnlessIncapacitated);

            if (CritOn > 0) actor.CritOn = Math.Min(actor.CritOn, CritOn);

            if (AuraAbility.HasValue) actor.AuraAbility = AuraAbility;

            if (ArmoredArmorClass != 0) actor.ArmoredArmorClassBonus += ArmoredArmorClass;

            foreach (Condition immune in Immune) actor.MakeImmune(immune);

            switch (Trait)
            {
                case Trait.UnarmoredDefense:
                    actor.UnarmoredDefense = Ability;
                    break;

                case Trait.Resistance:
                    actor.SetDefense(DamageType, Defense);
                    break;

                case Trait.Speed:
                    if (NotInHeavyArmor) actor.SpeedOutOfHeavyArmor += Flat;
                    else actor.Speed += Flat;
                    break;

                case Trait.Nimble:
                    actor.QuickOnBonus |= Manoeuvres;
                    break;

                case Trait.Training:
                    foreach (Skill skill in Skills) actor.Train(skill);
                    foreach (Ability save in Saves) actor.TrainSave(save);

                    if (Flat != 0 && Skills.Count > 0)
                        foreach (Skill skill in Skills) actor.AddSkillBonus(skill, Flat);

                    if (Flat != 0 && Saves.Count > 0)
                        foreach (Ability save in Saves) actor.AddSaveBonus(save, Flat);
                    break;

                case Trait.Expertise:
                    // which skills the Rogue doubles is the player's choice at creation; the
                    // feature only says how many, so the sheet asks and Train records it
                    foreach (Skill skill in Skills) actor.Train(skill, Training.Expert);
                    break;
            }
        }

        // the rider a hit carries, or null when this feature is not one or its condition is unmet
        public Rider RiderFor(int level, bool hadAdvantage, bool spent)
        {
            if (Trait != Trait.Rider || level < Level) return null;

            bool fires = When switch
            {
                When.Always => true,
                When.WithAdvantage => hadAdvantage,
                When.WhenSpent => spent,
                When.OnCritical => true,
                _ => false,
            };

            if (!fires) return null;

            return new Rider(Id, AmountAt(level), DamageType, Condition,
                             When == When.OnCritical);
        }

        public Boon BoonFor(int level, Actor actor = null)
        {
            if (Trait != Trait.Stance || level < Level) return null;

            // Sacred Weapon's Charisma modifier, at least +1; Rage's +2, +3, +4
            int flat = FlatAbility.HasValue && actor != null
                ? Math.Max(1, actor.AbilityModifier(FlatAbility.Value))
                : FlatAt(level);

            bool strAttacks = StanceAdvantage.Contains("str_attacks");
            bool strChecks = StanceAdvantage.Contains("str_checks");
            bool strSaves = StanceAdvantage.Contains("str_saves");

            return new Boon(Id, Id, Duration, flat, AmountAt(level),
                            attacks: (Touches & Core.Magic.Sways.Attacks) != 0,
                            saves: (Touches & Core.Magic.Sways.Saves) != 0,
                            checks: (Touches & Core.Magic.Sways.Checks) != 0,
                            damage: (Touches & Core.Magic.Sways.Damage) != 0,
                            armorClass: (Touches & Core.Magic.Sways.ArmorClass) != 0 ? flat : 0,
                            save: strSaves ? Core.Characters.Ability.Strength : (Ability?)null,
                            advantageOnChecks: strChecks,
                            advantageOnAttacks: strAttacks,
                            advantageAgainst: AdvantageAgainst)
            {
                AdvantageOnSaves = strSaves,
                CheckAbility = strChecks ? Core.Characters.Ability.Strength : (Ability?)null,
                Resists = Resists,
                StrengthOnly = StrengthOnly,
            };
        }

        public override string ToString() =>
            $"{Id} [{Trait.ToString().ToLowerInvariant()}] level {Level}" +
            (Amount.IsNothing ? "" : $" {Amount}") +
            (PerLevel.IsNothing ? "" : $" +{PerLevel} every {PerLevels} levels") +
            (Flat != 0 ? $" {Flat:+0;-0}" : "") +
            (Uses > 0 ? $", {Uses} per rest" : "");
    }

    public static class Recharges
    {
        public static bool TryParse(string id, out Recharge recharge)
        {
            switch ((id ?? "short").ToLowerInvariant())
            {
                case "short": recharge = Recharge.Short; return true;
                case "short_one": recharge = Recharge.ShortOne; return true;
                case "long": recharge = Recharge.Long; return true;
                default: recharge = Recharge.Short; return false;
            }
        }
    }

    public static class Traits
    {
        public static readonly IReadOnlyList<Trait> All =
            Enum.GetValues(typeof(Trait)).Cast<Trait>().ToList();

        public static string Id(this Trait trait) => Snake(trait.ToString());

        public static bool TryParse(string id, out Trait trait)
        {
            foreach (Trait t in All)
            {
                if (!string.Equals(t.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                trait = t;
                return true;
            }

            trait = Trait.Narrate;
            return false;
        }

        public static string Id(this Grants grants) => Snake(grants.ToString());

        public static bool TryParse(string id, out Grants grants)
        {
            foreach (Grants g in Enum.GetValues(typeof(Grants)).Cast<Grants>())
            {
                if (!string.Equals(g.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                grants = g;
                return true;
            }

            grants = Grants.Action;
            return false;
        }

        public static string Id(this When when) => Snake(when.ToString());

        public static bool TryParse(string id, out When when)
        {
            foreach (When w in Enum.GetValues(typeof(When)).Cast<When>())
            {
                if (!string.Equals(w.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                when = w;
                return true;
            }

            when = When.Always;
            return false;
        }

        static string Snake(string pascal)
        {
            var s = new System.Text.StringBuilder();

            for (int i = 0; i < pascal.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascal[i])) s.Append('_');

                s.Append(char.ToLowerInvariant(pascal[i]));
            }

            return s.ToString();
        }
    }
}
