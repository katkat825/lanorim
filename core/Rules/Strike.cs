using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Dice;
using Core.Resolution;

namespace Core.Rules
{
    // what one attack did. built by Strike.Make, which is the only thing that rolls an attack and
    // the only thing that turns one into hit points off a target.
    public sealed class Blow
    {
        public Blow(Actor attacker, Actor target, Attack attack, Attempt attempt,
                    int rolled, int suffered, IReadOnlyList<Rider> riders)
        {
            Attacker = attacker;
            Target = target;
            Attack = attack;
            Attempt = attempt;
            Rolled = rolled;
            Suffered = suffered;
            Riders = riders ?? Array.Empty<Rider>();
        }

        public Actor Attacker { get; }

        public Actor Target { get; }

        public Attack Attack { get; }

        public Attempt Attempt { get; }

        // before the target's resistance; the sheet shows this and the target shows Suffered
        public int Rolled { get; }

        public int Suffered { get; }

        public IReadOnlyList<Rider> Riders { get; }

        public bool Hit => Attempt.Succeeded;

        public bool Critical => Attempt.IsCritical;

        public bool Downed => Target != null && Target.IsDown;

        public override string ToString() =>
            $"{Attacker?.Id} {Attack?.Id} at {Target?.Id}: {Attempt}" +
            (Hit ? $", {Suffered} {Attack?.DamageType.Id()}" : "") +
            (Downed ? ", down" : "");
    }

    // extra damage or a condition that comes along with a hit: Sneak Attack, Divine Smite, a
    // flaming sword, Hunter's Mark. the class layer declares them; core just adds them up.
    public sealed class Rider
    {
        public Rider(string id, DiceRoll damage = default, DamageType type = DamageType.None,
                     Condition condition = Condition.None, bool onlyOnCritical = false)
        {
            Id = id;
            Damage = damage;
            Type = type;
            Condition = condition;
            OnlyOnCritical = onlyOnCritical;
        }

        public string Id { get; }

        public DiceRoll Damage { get; }

        // None means "the same type the weapon deals" - a Sneak Attack is not its own damage type
        public DamageType Type { get; }

        public Condition Condition { get; }

        public bool OnlyOnCritical { get; }

        public DamageType TypeOr(DamageType weapon) => Type == DamageType.None ? weapon : Type;

        public override string ToString() =>
            Id + (Damage.IsNothing ? "" : $" {Damage}") +
            (Condition == Condition.None ? "" : $" {Condition.Id()}");
    }

    public static class Strike
    {
        public static Blow Make(IResolver resolver, Actor attacker, Actor target, Attack attack,
                                Advantage extra = Advantage.Flat,
                                IReadOnlyList<Rider> riders = null)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (attack == null) throw new ArgumentNullException(nameof(attack));

            Advantage advantage = attacker.AttackAdvantage
                                          .And(target.AdvantageAgainstMe)
                                          .And(extra);

            int boons = 0;

            foreach (DiceRoll boon in attacker.Boons.DiceOnAttacks) boons += resolver.Roll(boon);

            Attempt attempt = resolver.Resolve(RollKind.Attack, attack.Modifier(attacker) + boons,
                                               target.ArmorClass, advantage);

            if (!attempt.Succeeded)
                return new Blow(attacker, target, attack, attempt, 0, 0, Array.Empty<Rider>());

            int rolled = Math.Max(0, resolver.Roll(attack.DamageFor(attacker, attempt.IsCritical)));
            int suffered = target.Suffer(rolled, attack.DamageType);

            var landed = new List<Rider>();

            foreach (Rider rider in riders ?? Array.Empty<Rider>())
            {
                if (rider.OnlyOnCritical && !attempt.IsCritical) continue;

                if (!rider.Damage.IsNothing)
                {
                    // a rider crits with the blow it rides on, dice doubled and modifier not
                    DiceRoll dice = attempt.IsCritical ? rider.Damage.Doubled() : rider.Damage;

                    int extraRolled = Math.Max(0, resolver.Roll(dice));

                    rolled += extraRolled;
                    suffered += target.Suffer(extraRolled, rider.TypeOr(attack.DamageType));
                }

                if (rider.Condition != Condition.None) target.Apply(rider.Condition);

                landed.Add(rider);
            }

            return new Blow(attacker, target, attack, attempt, rolled, suffered, landed);
        }
    }
}
