using System;
using System.Collections.Generic;
using System.Linq;
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

        // a condition the target may save against: a wolf's bite knocks prone unless... SRD
        // statblocks spell the save and its DC out. null is no save - the condition just lands
        public Ability? Save { get; init; }

        public int Dc { get; init; }

        // only a creature this size or smaller: the wolf's "if the target is Medium or smaller"
        public Size? MaxSize { get; init; }

        // not on these: the Ghoul's claw paralyses "a creature that isn't an Undead or elf"
        public IReadOnlyList<string> ExceptTags { get; init; } = Array.Empty<string>();

        // the condition lasts "until the end of its next turn" (the target's), not for good: the
        // fight keeps that book (Encounter.Hit)
        public bool UntilTargetsNextTurn { get; init; }

        public DamageType TypeOr(DamageType weapon) => Type == DamageType.None ? weapon : Type;

        public override string ToString() =>
            Id + (Damage.IsNothing ? "" : $" {Damage}") +
            (Condition == Condition.None ? "" : $" {Condition.Id()}");
    }

    // an attack is two halves with a gap between them: the roll, and what the roll does. the gap
    // is where a reaction lives - a Shield raises the armor class after the die is thrown and
    // before the damage is - so the fight calls the halves separately, and anything outside a
    // fight calls Make, which is both halves with nothing in between.
    public static class Strike
    {
        public static Blow Make(IResolver resolver, Actor attacker, Actor target, Attack attack,
                                Advantage extra = Advantage.Flat,
                                IReadOnlyList<Rider> riders = null) =>
            Land(resolver, attacker, target, attack,
                 Roll(resolver, attacker, target, attack, extra), riders);

        // everything an attack roll leans on, as SRD counts it: any advantage and any disadvantage
        // cancel, however many of each. close is "within 5 feet" (Prone); the two sight answers are
        // the board's when there is one (walls, obscurement) and the creatures' own otherwise
        public static Advantage Lean(Actor attacker, Actor target, bool close = true,
                                     bool? attackerSees = null, bool? targetSees = null,
                                     Advantage extra = Advantage.Flat, bool? fearInSight = null,
                                     Attack attack = null)
        {
            (bool adv, bool dis) mine = attacker.AttackLeansWith(attack);

            // SRD 5.2.1 Frightened: disadvantage "while the source of fear is within line of
            // sight" - the fight says whether it is; off a board it is taken to be
            if (attacker.Has(Condition.Frightened) && (fearInSight ?? true)) mine.dis = true;

            // SRD 5.2.1 Grappled: disadvantage on attacks at anyone but the grappler
            if (attacker.Has(Condition.Grappled) && !attacker.HasFrom(Condition.Grappled, target))
                mine.dis = true;

            // SRD 5.2.1: attacking what you can't see is at disadvantage; being attacked by what
            // you can't see gives the attacker advantage
            bool sees = attackerSees ?? attacker.CanSee(target);
            bool seen = targetSees ?? target.CanSee(attacker);

            (bool adv, bool dis) theirs = target.LeansAgainstMe(close, attacker, sees);

            return Advantages.Of(mine.adv || theirs.adv || !seen || extra == Advantage.Advantage,
                                 mine.dis || theirs.dis || !sees ||
                                 extra == Advantage.Disadvantage).And(
                       extra == Advantage.Cancelled ? Advantage.Cancelled : Advantage.Flat);
        }

        public static Attempt Roll(IResolver resolver, Actor attacker, Actor target, Attack attack,
                                   Advantage extra = Advantage.Flat, bool close = true,
                                   bool? attackerSees = null, bool? targetSees = null,
                                   int cover = 0, bool? fearInSight = null)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (attack == null) throw new ArgumentNullException(nameof(attack));

            Advantage advantage = Lean(attacker, target, close, attackerSees, targetSees, extra,
                                       fearInSight, attack);

            int boons = 0;

            foreach (DiceRoll boon in attacker.Boons.DiceOnAttacks) boons += resolver.Roll(boon, attacker);

            Attempt attempt = resolver.Resolve(RollKind.Attack, attack.Modifier(attacker) + boons,
                                               target.ArmorClass + cover, advantage, attacker);

            // the Champion's 19 and 18: a weapon or an Unarmed Strike (SRD 5.2.1 p.49)
            if (attacker.CritOn < 20) attempt = attempt.CritsFrom(attacker.CritOn);

            // whatever that roll leaned on is spent now it has been leaned on
            attacker.Boons.Attacked();
            target.Boons.AttackedAt();

            return Closing(attempt, target, close);
        }

        // SRD 5.2.1 Mirror Image: a hit on the bearer rolls a d6 for each duplicate left, and on
        // any 3 or higher a duplicate takes the hit and is gone. an attacker that is Blinded or
        // has Truesight is not fooled. returns the attempt, diverted if a duplicate took it
        public static Attempt Decoyed(IResolver resolver, Actor attacker, Actor target,
                                      Attempt attempt)
        {
            if (attempt == null || !attempt.Succeeded) return attempt;

            Boon decoys = target.Boons.Decoy;

            if (decoys == null) return attempt;

            if (attacker != null &&
                (attacker.Has(Condition.Blinded) || attacker.Boons.Truesight)) return attempt;

            bool taken = false;

            for (int i = 0; i < decoys.Decoys; i++)
                if (resolver.Roll(new DiceRoll(1, Die.D6), target) >= 3) taken = true;

            if (!taken) return attempt;

            decoys.Decoys--;

            if (decoys.Decoys <= 0) target.Boons.Remove(decoys);

            return attempt.Divert();
        }

        // SRD 5.2.1 Paralyzed and Unconscious: any hit from within 5 feet is a critical hit
        public static Attempt Closing(Attempt attempt, Actor target, bool close)
        {
            if (close && attempt.Succeeded && target.Conditions.Any(c => c.CritsFromClose()))
                return attempt.Critical();

            return attempt;
        }

        public static Blow Land(IResolver resolver, Actor attacker, Actor target, Attack attack,
                                Attempt attempt, IReadOnlyList<Rider> riders = null)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (attempt == null) throw new ArgumentNullException(nameof(attempt));

            if (!attempt.Succeeded)
                return new Blow(attacker, target, attack, attempt, 0, 0, Array.Empty<Rider>());

            // Uncanny Dodge: the attack's damage against it is halved
            bool halved = target.Boons.TakeHalving();

            int rolled = Math.Max(0, resolver.Roll(attack.DamageFor(attacker, attempt.IsCritical),
                                                   attacker));

            // Enlarge's +1d4 and Reduce's -1d4 on weapon damage, doubled on a critical like any
            // of the hit's dice; Reduce never takes it below 1
            foreach (Boon grown in attacker?.Boons.WeaponDice ?? Enumerable.Empty<Boon>())
            {
                int more = Math.Max(0, resolver.Roll(attempt.IsCritical
                                                         ? grown.WeaponDice.Doubled()
                                                         : grown.WeaponDice, attacker));

                rolled = grown.WeaponDiceLess ? Math.Max(1, rolled - more) : rolled + more;
            }

            DamageType type = attack.DamageTypeFor(attacker, target);

            if (halved) rolled /= 2;

            int suffered = target.Suffer(rolled, type);

            var landed = new List<Rider>();

            foreach (Rider rider in riders ?? Array.Empty<Rider>())
            {
                if (rider.OnlyOnCritical && !attempt.IsCritical) continue;

                if (!rider.Damage.IsNothing)
                {
                    // a rider crits with the blow it rides on, dice doubled and modifier not
                    DiceRoll dice = attempt.IsCritical ? rider.Damage.Doubled() : rider.Damage;

                    int extraRolled = Math.Max(0, resolver.Roll(dice, attacker));

                    if (halved) extraRolled /= 2;

                    rolled += extraRolled;
                    suffered += target.Suffer(extraRolled, rider.TypeOr(type));
                }

                if (rider.Condition != Condition.None &&
                    (!rider.MaxSize.HasValue || target.CurrentSize <= rider.MaxSize.Value) &&
                    !rider.ExceptTags.Any(target.Is) &&
                    (!rider.Save.HasValue ||
                     Checks.Save(resolver, target, rider.Save.Value, rider.Dc,
                                 target.HasAdvantage("save_vs:" + rider.Condition.Id())
                                     ? Advantage.Advantage
                                     : Advantage.Flat).Failed))
                    target.Apply(rider.Condition, attacker);

                landed.Add(rider);
            }

            // Hex, Hunter's Mark: the target carries the attacker's mark, and a hit collects it
            foreach (Boon mark in target.Boons.MarksFrom(attacker).ToList())
            {
                DiceRoll dice = attempt.IsCritical ? mark.Mark.Doubled() : mark.Mark;
                int extra = Math.Max(0, resolver.Roll(dice, attacker));

                rolled += extra;
                suffered += target.Suffer(extra, mark.MarkType);
            }

            return new Blow(attacker, target, attack, attempt, rolled, suffered, landed);
        }
    }
}
