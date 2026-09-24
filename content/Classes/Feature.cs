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

        // an extra action or reaction, each round or once per rest. Action Surge's descendant
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
    }

    // which part of the turn an ActionGrant adds to. a third *action* would be Extra Attack by
    // another name, which v1_class_roster.md says never to port - so a class's compression is a
    // bonus action doing an action's work, and the extra action is a per-rest thing.
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
                       string note = null)
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
        }

        public string Id { get; }

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
        }

        // a feature that does something when the player asks, rather than sitting on the sheet
        public bool IsActive =>
            Trait == Trait.Stance || Trait == Trait.Recovery || Trait == Trait.Shape ||
            (Trait == Trait.Rider && When == When.WhenSpent);

        public DiceRoll AmountAt(int level)
        {
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

            switch (Trait)
            {
                case Trait.UnarmoredDefense:
                    actor.UnarmoredDefense = Ability;
                    break;

                case Trait.Resistance:
                    actor.SetDefense(DamageType, Defense);
                    break;

                case Trait.Speed:
                    actor.Speed += Flat;
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

        public Boon BoonFor(int level) =>
            Trait != Trait.Stance || level < Level
                ? null
                : new Boon(Id, Id, Duration, Flat, AmountAt(level),
                           attacks: (Touches & Core.Magic.Sways.Attacks) != 0,
                           saves: (Touches & Core.Magic.Sways.Saves) != 0,
                           checks: (Touches & Core.Magic.Sways.Checks) != 0,
                           damage: (Touches & Core.Magic.Sways.Damage) != 0,
                           armorClass: (Touches & Core.Magic.Sways.ArmorClass) != 0 ? Flat : 0);

        public override string ToString() =>
            $"{Id} [{Trait.ToString().ToLowerInvariant()}] level {Level}" +
            (Amount.IsNothing ? "" : $" {Amount}") +
            (PerLevel.IsNothing ? "" : $" +{PerLevel} every {PerLevels} levels") +
            (Flat != 0 ? $" {Flat:+0;-0}" : "") +
            (Uses > 0 ? $", {Uses} per rest" : "");
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
