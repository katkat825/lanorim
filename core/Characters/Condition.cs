using System;
using System.Collections.Generic;
using Core.Localization;

namespace Core.Characters
{
    // SRD 5.2.1's conditions, core effects only. decisions_checklist.md section 1 (2026-09-24) lets
    // the subset grow to whatever a faithful spell needs; exhaustion's six-level ladder stays
    // deferred. Unconscious is set by dropping to 0 HP, and a spell (Sleep) may set it too.
    public enum Condition
    {
        None = 0,

        Prone,
        Poisoned,
        Stunned,
        Frightened,
        Restrained,
        Grappled,

        // set by falling to 0 HP (cleared by healing above it), or by a spell
        Unconscious,

        // added 2026-09-24 so the spells that name them can keep their SRD names
        Blinded,
        Charmed,
        Deafened,
        Incapacitated,
        Invisible,
        Paralyzed,
        Petrified,
    }

    public static class Conditions
    {
        // what content may apply
        public static readonly IReadOnlyList<Condition> Appliable = new[]
        {
            Condition.Prone, Condition.Poisoned, Condition.Stunned,
            Condition.Frightened, Condition.Restrained, Condition.Grappled,
            Condition.Unconscious,
            Condition.Blinded, Condition.Charmed, Condition.Deafened, Condition.Incapacitated,
            Condition.Invisible, Condition.Paralyzed, Condition.Petrified,
        };

        public static readonly IReadOnlyList<Condition> All = Appliable;

        public static string Id(this Condition condition) =>
            condition == Condition.None ? "none" : condition.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out Condition condition)
        {
            foreach (Condition c in All)
            {
                if (!string.Equals(c.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                condition = c;
                return true;
            }

            condition = Condition.None;
            return false;
        }

        public static string NameKey(this Condition condition) =>
            KeyConventions.Key(KeyConventions.ConditionNs, condition.Id(), "name");

        public static string DescriptionKey(this Condition condition) =>
            KeyConventions.Key(KeyConventions.ConditionNs, condition.Id(), "description");


        // --- the core effects. one rule per question, asked by the combat engine ---

        // can it act at all? SRD 5.2.1: Incapacitated, and the four that include it
        public static bool Incapacitates(this Condition condition) =>
            condition == Condition.Incapacitated || condition == Condition.Stunned ||
            condition == Condition.Paralyzed || condition == Condition.Petrified ||
            condition == Condition.Unconscious;

        // speed 0: can it leave the square it is in? SRD 5.2.1's Stunned has no speed clause (SRD
        // p.189) - a stunned creature can't act but can move - so it isn't here (2026-09-25, run-log
        // question 2). Incapacitated alone does not stop a creature moving either, which is why
        // Hypnotic Pattern says "and a Speed of 0" separately
        public static bool Roots(this Condition condition) =>
            condition == Condition.Restrained || condition == Condition.Grappled ||
            condition == Condition.Paralyzed || condition == Condition.Petrified ||
            condition == Condition.Unconscious;

        // SRD: disadvantage on its own attack rolls. prone, poisoned and restrained always.
        // Frightened only while the source is in sight, and Grappled only against anyone but the
        // grappler - both need the fight, so Strike.Lean asks them. Blinded is the sight rule
        public static bool AttacksAtDisadvantage(this Condition condition) =>
            condition == Condition.Poisoned ||
            condition == Condition.Restrained || condition == Condition.Prone;

        // SRD: poisoned and frightened also sour ability checks
        public static bool ChecksAtDisadvantage(this Condition condition) =>
            condition == Condition.Poisoned || condition == Condition.Frightened;

        // attack rolls against it have advantage, from any distance. Prone is not here: it is
        // advantage from within 5 feet and disadvantage from further, which needs the distance
        // (Actor.AdvantageAgainstMe). Blinded is the sight rule
        public static bool GrantsAdvantageToAttackers(this Condition condition) =>
            condition == Condition.Restrained || condition == Condition.Stunned ||
            condition == Condition.Paralyzed || condition == Condition.Petrified ||
            condition == Condition.Unconscious;

        // SRD 5.2.1: a hit from within 5 feet is a critical hit
        public static bool CritsFromClose(this Condition condition) =>
            condition == Condition.Paralyzed || condition == Condition.Unconscious;

        // SRD: stunned, paralyzed, petrified and unconscious auto-fail STR and DEX saves
        public static bool AutoFailsSave(this Condition condition, Ability ability) =>
            (condition == Condition.Stunned || condition == Condition.Paralyzed ||
             condition == Condition.Petrified || condition == Condition.Unconscious) &&
            (ability == Ability.Strength || ability == Ability.Dexterity);

        // SRD: restrained has disadvantage on Dexterity saves
        public static bool SavesAtDisadvantage(this Condition condition, Ability ability) =>
            condition == Condition.Restrained && ability == Ability.Dexterity;

        // getting up off the floor costs half your movement; grappled and restrained cost nothing
        // to try but can't be shrugged off by moving
        public static bool StandingCostsMovement(this Condition condition) =>
            condition == Condition.Prone;
    }
}

namespace Core.Characters
{
    public enum Size
    {
        Tiny,
        Small,
        Medium,
        Large,
        Huge,
        Gargantuan,
    }

    public static class Sizes
    {
        public static string Id(this Size size) => size.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out Size size)
        {
            foreach (Size s in Enum.GetValues(typeof(Size)))
            {
                if (!string.Equals(s.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                size = s;
                return true;
            }

            size = Size.Medium;
            return false;
        }
    }
}
