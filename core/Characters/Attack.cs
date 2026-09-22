using System;
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

        public bool IsRanged => Range > 0;

        public string NameKey => KeyConventions.ItemName(Id);

        // finesse takes the better of the two, which is SRD's wording turned into arithmetic
        public Ability AbilityFor(Actor actor)
        {
            if (actor == null || !Finesse) return Ability;

            return actor.AbilityModifier(Ability.Dexterity) > actor.AbilityModifier(Ability)
                ? Ability.Dexterity
                : Ability;
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
                       (actor?.Boons.FlatOnDamage ?? 0);

            DiceRoll dice = critical ? Damage.Doubled() : Damage;

            return dice.Plus(flat);
        }

        // how far away it can still be used, in squares. a melee attack's answer is its reach
        public int Reaches => IsRanged ? LongRange : Reach;

        public override string ToString() =>
            $"{Id}: {Damage} {DamageType.Id()}" +
            (IsRanged ? $", range {Range}/{LongRange}" : $", reach {Reach}") +
            (Finesse ? ", finesse" : "") +
            $", {Hand.ToString().ToLowerInvariant()} hand";
    }
}
