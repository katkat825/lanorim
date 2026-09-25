using System;
using System.Collections.Generic;
using Core.Dice;
using Core.Localization;

namespace Core.Characters
{
    public enum Hand
    {
        Main,
        Off,
        Two,

        // a monster's claw, a spell's ray - nothing is held, so nothing can be swapped out
        None,
    }

    // one way of hitting something. a weapon item makes one of these and so does a line on a
    // monster statblock, which is why it lives in core and neither of them does.
    public sealed class Attack
    {
        public Attack(string id, DiceRoll damage, DamageType damageType,
                      Ability ability = Ability.Strength, bool proficient = true,
                      int reach = 1, int range = 0, int longRange = 0,
                      Hand hand = Hand.Main, bool finesse = false,
                      int attackBonus = 0, int damageBonus = 0, bool addsAbilityToDamage = true)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Damage = damage;
            DamageType = damageType;
            Ability = ability;
            Proficient = proficient;
            Reach = Math.Max(1, reach);
            Range = Math.Max(0, range);
            LongRange = Math.Max(Range, longRange);
            Hand = hand;
            Finesse = finesse;
            AttackBonus = attackBonus;
            DamageBonus = damageBonus;
            AddsAbilityToDamage = addsAbilityToDamage;
        }

        public string Id { get; }

        // --- SRD 5.2.1 weapon properties (p.89-90), 2026-09-25 -------------------------------------

        // Thrown: a melee weapon that can also be thrown - a melee attack within its reach, a
        // ranged one past it. without this a dagger was ranged-only, and at disadvantage in melee
        public bool Thrown { get; init; }

        // Light: the second Light weapon's bonus-action attack (CombatSession)
        public bool Light { get; init; }

        // Heavy: disadvantage for a wielder with less than 13 Strength (melee) or Dexterity (ranged)
        public bool Heavy { get; init; }

        // Versatile: the damage with both hands on it
        public DiceRoll Versatile { get; init; }

        // "simple" or "martial": what a class's weapon training reads
        public string Category { get; init; } = "";

        // the same attack, trained or not, with other damage dice or none of the ability on the
        // damage (the Light bonus attack)
        public Attack With(bool? proficient = null, DiceRoll? damage = null, bool? addsAbility = null,
                           int? damageBonus = null) =>
            new Attack(Id, damage ?? Damage, DamageType, Ability, proficient ?? Proficient, Reach, Range,
                       LongRange, Hand, Finesse, AttackBonus, damageBonus ?? DamageBonus,
                       addsAbility ?? AddsAbilityToDamage)
            {
                Thrown = Thrown, Light = Light, Heavy = Heavy, Versatile = Versatile, Category = Category,
                OnHit = OnHit,
            };

        public DiceRoll Damage { get; }

        public DamageType DamageType { get; }

        // the ability it would use if it weren't finesse; finesse takes the better of it and Dex
        public Ability Ability { get; }

        public bool Proficient { get; }

        // squares, for a melee attack. 1 is adjacent, 2 is a glaive
        public int Reach { get; }

        // squares, for a thrown or ranged one. 0 means it is not a ranged attack at all
        public int Range { get; }

        // SRD: past the normal range and out to the long one, the shot is at disadvantage
        public int LongRange { get; }

        public Hand Hand { get; }

        public bool Finesse { get; }

        public int AttackBonus { get; }

        public int DamageBonus { get; }

        // a Magic Missile-shaped attack adds no ability modifier to its damage
        public bool AddsAbilityToDamage { get; }

        // a thrown weapon is a melee weapon that can also reach further
        public bool IsRanged => Range > 0 && !Thrown;

        // what a hit with it always carries: a statblock's poison, its knock-down. riders the
        // attacker's own features add (Sneak Attack) come with the swing instead
        public IReadOnlyList<Core.Rules.Rider> OnHit { get; init; } = Array.Empty<Core.Rules.Rider>();

        public string NameKey => KeyConventions.ItemName(Id);

        // finesse takes the better of the two, which is SRD's wording turned into arithmetic
        public Ability AbilityFor(Actor actor)
        {
            if (actor == null) return Ability;

            Ability own = Finesse &&
                          actor.AbilityModifier(Ability.Dexterity) > actor.AbilityModifier(Ability)
                ? Ability.Dexterity
                : Ability;

            // Shillelagh: this weapon MAY swing with the caster's spellcasting ability for now -
            // "can use", so the better of the two
            if (actor.Boons.RewriteFor(Id)?.Ability is Ability rewritten &&
                actor.AbilityModifier(rewritten) > actor.AbilityModifier(own))
                return rewritten;

            return own;
        }

        public int Modifier(Actor actor)
        {
            if (actor == null) return AttackBonus;

            return actor.AbilityModifier(AbilityFor(actor)) +
                   (Proficient ? actor.ProficiencyBonus : 0) +
                   AttackBonus +
                   actor.Boons.FlatOnAttacks;
        }

        // a crit doubles the dice and not the modifier - SRD 5.2.1
        public DiceRoll DamageFor(Actor actor, bool critical = false)
        {
            int flat = DamageBonus +
                       (AddsAbilityToDamage && actor != null
                           ? actor.AbilityModifier(AbilityFor(actor))
                           : 0) +
                       (actor == null ? 0 : actor.Boons.FlatOnDamageFor(AbilityFor(actor)));

            DiceRoll die = actor?.Boons.RewriteFor(Id) is WeaponRewrite rewrite &&
                           !rewrite.Die.IsNothing
                ? rewrite.Die
                : Damage;

            DiceRoll dice = critical ? die.Doubled() : die;

            return dice.Plus(flat);
        }

        // the damage type this hit deals to that target. a rewrite that offers another type
        // (Shillelagh's force) is taken when the target takes more from it - the wielder's choice,
        // and nobody chooses the worse one
        public DamageType DamageTypeFor(Actor actor, Actor target)
        {
            WeaponRewrite rewrite = actor?.Boons.RewriteFor(Id);

            if (rewrite == null || rewrite.DamageType == DamageType.None) return DamageType;

            if (target == null) return rewrite.DamageType;

            int mine = target.DefenseAgainst(DamageType).Apply(100);
            int theirs = target.DefenseAgainst(rewrite.DamageType).Apply(100);

            return theirs > mine ? rewrite.DamageType : DamageType;
        }

        // how far away it can still be used, in squares. a melee attack's answer is its reach
        public int Reaches => IsRanged ? LongRange : Thrown ? Math.Max(Reach, LongRange) : Reach;

        public override string ToString() =>
            $"{Id}: {Damage} {DamageType.Id()}" +
            (IsRanged ? $", range {Range}/{LongRange}" : $", reach {Reach}") +
            (Finesse ? ", finesse" : "") +
            $", {Hand.ToString().ToLowerInvariant()} hand";
    }
}
