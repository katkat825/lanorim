using System;
using System.Collections.Generic;
using Core.Localization;

namespace Core.Characters
{
    // the v1 subset, core effects only - decisions_checklist.md section 6 defers the rest, and
    // exhaustion's six-level ladder with it. Unconscious is here because dropping to 0 HP has to
    // land somewhere; it isn't a condition content can apply.
    public enum Condition
    {
        None = 0,

        Prone,
        Poisoned,
        Stunned,
        Frightened,
        Restrained,
        Grappled,

        // engine-owned: set by falling to 0 HP, cleared by healing above it
        Unconscious,
    }

    public static class Conditions
    {
        // what content may apply. Unconscious is deliberately absent.
        public static readonly IReadOnlyList<Condition> Appliable = new[]
        {
            Condition.Prone, Condition.Poisoned, Condition.Stunned,
            Condition.Frightened, Condition.Restrained, Condition.Grappled,
        };

        public static readonly IReadOnlyList<Condition> All = new[]
        {
            Condition.Prone, Condition.Poisoned, Condition.Stunned,
            Condition.Frightened, Condition.Restrained, Condition.Grappled,
            Condition.Unconscious,
        };

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

        // can it act at all? SRD: stunned and unconscious are incapacitated
        public static bool Incapacitates(this Condition condition) =>
            condition == Condition.Stunned || condition == Condition.Unconscious;

        // can it leave the square it is in?
        public static bool Roots(this Condition condition) =>
            condition == Condition.Restrained || condition == Condition.Grappled ||
            condition.Incapacitates();

        // SRD: restrained gives disadvantage on its own attacks; poisoned and frightened too;
        // prone gives disadvantage on attacks at anything not adjacent, handled by the caller
        public static bool AttacksAtDisadvantage(this Condition condition) =>
            condition == Condition.Poisoned || condition == Condition.Frightened ||
            condition == Condition.Restrained || condition == Condition.Prone;

        // SRD: poisoned and frightened also sour ability checks
        public static bool ChecksAtDisadvantage(this Condition condition) =>
            condition == Condition.Poisoned || condition == Condition.Frightened;

        // attacks against it: restrained and prone-in-reach and unconscious grant advantage
        public static bool GrantsAdvantageToAttackers(this Condition condition) =>
            condition == Condition.Restrained || condition == Condition.Prone ||
            condition == Condition.Unconscious;

        // SRD: stunned and unconscious auto-fail STR and DEX saves
        public static bool AutoFailsSave(this Condition condition, Ability ability) =>
            condition.Incapacitates() &&
            (ability == Ability.Strength || ability == Ability.Dexterity);

        // getting up off the floor costs half your movement; grappled and restrained cost nothing
        // to try but can't be shrugged off by moving
        public static bool StandingCostsMovement(this Condition condition) =>
            condition == Condition.Prone;
    }
}
