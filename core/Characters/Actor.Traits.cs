using System;
using System.Linq;
using Core.Resolution;

namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- what a feature grants that isn't a number ------------------------------------------

        // a feature's standing advantages are boons for good, with the same leans as a spell's:
        // Danger Sense, Remarkable Athlete, Fey Ancestry. they used to be string keys ("save:dex",
        // "save_vs:charmed") in a table of their own (cc_task_dedupe-leftovers.md Part A)
        public bool InitiativeAdvantage => Boons.AdvantageOnInitiative;

        // advantage on a save against this condition: Fey Ancestry against Charmed
        public bool AdvantageOnSaveAgainst(Condition condition) => Boons.AdvantageOnSaveAgainst(condition);

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
            Advantages.Of(Boons.AdvantageOnSave(ability),
                          Boons.DisadvantageOnSave(ability) ||
                          _conditions.Any(c => c.SavesAtDisadvantage(ability)));
    }
}
