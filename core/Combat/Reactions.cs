using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Combat
{
    // the moments the fight stops and asks whether anybody wants to spend a reaction. closed, like
    // the primitives: a reaction answers one of these or it does not exist
    // (decisions_checklist.md section 6, reactions and interrupts).
    public enum Trigger
    {
        // an attack roll has hit and nothing has been done about it yet - the armor class can still
        // change, so a hit can still become a miss. Shield
        Hit,

        // a creature has started casting a spell and it has not taken effect. Counterspell
        Cast,

        // a creature is stepping out of another's reach. the opportunity attack
        LeaveReach,

        // a hit has landed and taken hit points off. Hellish Rebuke
        Damaged,

        // the reactor's own attack just hit. offered to the attacker, and answered with a bonus
        // action on its own turn rather than a reaction: SRD 5.2.1's Divine Smite and Searing Smite
        Struck,

        // a named spell has just picked the reactor as a target and has not landed yet: SRD 5.2.1
        // Shield's "or targeted by the Magic Missile spell" (2026-09-25)
        Targeted,
    }

    public static class Triggers
    {
        public static readonly IReadOnlyList<Trigger> All = new[]
        {
            Trigger.Hit, Trigger.Cast, Trigger.LeaveReach, Trigger.Damaged, Trigger.Struck,
        };

        public static string Id(this Trigger trigger) => trigger switch
        {
            Trigger.LeaveReach => "leave_reach",
            _ => trigger.ToString().ToLowerInvariant(),
        };

        public static bool TryParse(string id, out Trigger trigger)
        {
            foreach (Trigger t in All)
            {
                if (!string.Equals(t.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                trigger = t;
                return true;
            }

            trigger = Trigger.Hit;
            return false;
        }
    }

    // one of those moments, with everything a reaction might want to know about it. the engine
    // builds it, offers it, and then reads back the one thing a reaction may change about it:
    // whether it was stopped.
    public sealed class Moment
    {
        Moment(Trigger trigger, Actor source, Actor target)
        {
            Trigger = trigger;
            Source = source;
            Target = target;
        }

        public Trigger Trigger { get; }

        // who did the thing: the attacker, the caster, the one walking away
        public Actor Source { get; }

        // who it was done to. for a cast, nobody yet; for leaving reach, whoever is being left
        public Actor Target { get; }

        // the attack roll, for a Hit
        public Attempt Attempt { get; private set; }

        // the spell being cast, by id - the fight layer does not know what a spell is
        public string Spell { get; private set; }

        // the level it is being cast at, for a reaction that cares
        public int Level { get; private set; }

        // hit points lost, for a Damaged
        public int Damage { get; private set; }

        // the step being taken, for a LeaveReach
        public Cell From { get; private set; }

        public Cell To { get; private set; }

        // a reaction ended the thing outright. only a cast can be stopped in v1
        public bool Stopped { get; private set; }

        public void Stop() => Stopped = true;

        public static Moment Hit(Actor attacker, Actor target, Attempt attempt) =>
            new Moment(Trigger.Hit, attacker, target) { Attempt = attempt };

        public static Moment Cast(Actor caster, string spell, int level) =>
            new Moment(Trigger.Cast, caster, null) { Spell = spell, Level = level };

        public static Moment Targeted(Actor caster, Actor target, string spell) =>
            new Moment(Trigger.Targeted, caster, target) { Spell = spell };

        public static Moment Leaving(Actor mover, Actor watcher, Cell from, Cell to) =>
            new Moment(Trigger.LeaveReach, mover, watcher) { From = from, To = to };

        public static Moment Damaged(Actor attacker, Actor target, int damage) =>
            new Moment(Trigger.Damaged, attacker, target) { Damage = damage };

        // the attacker's own hit, before anything else happens: the source is the attacker, the
        // target the one it hit
        public static Moment Struck(Actor attacker, Actor target, Blow blow) =>
            new Moment(Trigger.Struck, attacker, target) { Blow = blow, Attempt = blow?.Attempt };

        // the hit, for a Struck: whether it was a melee attack, whether it was a critical
        public Blow Blow { get; private set; }

        public override string ToString() =>
            $"{Trigger.ToString().ToLowerInvariant()} by {Source?.Id}" +
            (Target != null ? $" on {Target.Id}" : "") +
            (Spell != null ? $" ({Spell})" : "") +
            (Stopped ? ", stopped" : "");
    }

    // something an actor can do with its reaction. the engine asks whether it can answer this
    // moment - in range, in sight, paid for - and, if the actor's chooser picks it, tells it to.
    public interface IReaction
    {
        string Id { get; }

        Trigger Trigger { get; }

        // armor class it would add against the blow that triggered it. zero for almost everything;
        // it is here so a chooser can tell whether a Shield would turn the hit, without knowing
        // what a Shield is
        int Deflects { get; }

        // what answering costs: a reaction for nearly everything, a bonus action for a smite
        Spend Cost => Spend.Reaction;

        // a moment it answers besides its own trigger: Shield's "or targeted by the Magic
        // Missile spell"
        bool AlsoAnswers(Moment moment) => false;

        bool CanAnswer(Encounter fight, Actor reactor, Moment moment);

        void Answer(Encounter fight, Actor reactor, Moment moment);
    }

    // who decides whether a reaction is spent, and on which of the reactions that could answer.
    // the player's is the UI; a monster's is its tactics. null means "no, let it happen".
    public interface IReactionChooser
    {
        IReaction Choose(Encounter fight, Actor reactor, Moment moment,
                         IReadOnlyList<IReaction> options);
    }

    public static class ReactionChoosers
    {
        // takes the first thing offered. what every monster had before the window existed, so the
        // opportunity attack keeps firing exactly as it did
        public static readonly IReactionChooser Always = new First();

        public static readonly IReactionChooser Never = new Decline();

        // the sensible default for a hero the UI is not asking: an opportunity attack whenever
        // it is offered, a spell that deflects only when the armor class would turn the hit, and
        // anything else when there is nothing better to do with the reaction
        public static readonly IReactionChooser WhenItHelps = new Helps();

        sealed class First : IReactionChooser
        {
            public IReaction Choose(Encounter fight, Actor reactor, Moment moment,
                                    IReadOnlyList<IReaction> options) =>
                options.FirstOrDefault();
        }

        sealed class Decline : IReactionChooser
        {
            public IReaction Choose(Encounter fight, Actor reactor, Moment moment,
                                    IReadOnlyList<IReaction> options) => null;
        }

        sealed class Helps : IReactionChooser
        {
            public IReaction Choose(Encounter fight, Actor reactor, Moment moment,
                                    IReadOnlyList<IReaction> options)
            {
                // a smite spends a spell slot, and nothing spends one unasked: the player's
                // reaction settings decide whether it is always, never or asked (combat_ux.md)
                if (moment.Trigger == Trigger.Struck) return null;

                if (moment.Trigger != Trigger.Hit) return options.FirstOrDefault();

                Attempt hit = moment.Attempt;

                // a natural 20 hits whatever the armor class is; spending a Shield on it is a
                // wasted spell - but halving it (Uncanny Dodge) still helps, and helps most
                if (hit == null) return null;

                IReaction turns = hit.IsCritical
                    ? null
                    : options.Where(o => o.Deflects > 0)
                             .Where(o => !hit.Rejudged(reactor.ArmorClass + o.Deflects).Succeeded)
                             .OrderBy(o => o.Deflects)
                             .FirstOrDefault();

                return turns ?? options.FirstOrDefault(o => o is HalveReaction);
            }
        }
    }

    // SRD 5.2.1 Uncanny Dodge (p.63): "When an attacker that you can see hits you with an attack
    // roll, you can take a Reaction to halve the attack's damage against you"
    public sealed class HalveReaction : IReaction
    {
        public HalveReaction(string id = "uncanny_dodge") => Id = id ?? "uncanny_dodge";

        public string Id { get; }

        public Trigger Trigger => Trigger.Hit;

        public int Deflects => 0;

        public bool CanAnswer(Encounter fight, Actor reactor, Moment moment) =>
            moment != null && ReferenceEquals(moment.Target, reactor) && reactor.CanAct &&
            (fight == null || fight.Sees(reactor, moment.Source));

        public void Answer(Encounter fight, Actor reactor, Moment moment) =>
            reactor.Boons.Add(new Boon(Id, Id, Duration.Encounter) { HalvesNextHit = true });
    }

    // the opportunity attack, as one reaction among several rather than the only one there is
    public sealed class OpportunityAttack : IReaction
    {
        public OpportunityAttack(Attack attack) =>
            Attack = attack ?? throw new ArgumentNullException(nameof(attack));

        public Attack Attack { get; }

        public string Id => "opportunity_attack";

        public Trigger Trigger => Trigger.LeaveReach;

        public int Deflects => 0;

        // SRD: leaving a hostile's reach. the reactor is the one being left; stepping from inside
        // its reach to outside it is the whole trigger
        public bool CanAnswer(Encounter fight, Actor reactor, Moment moment)
        {
            if (!ReferenceEquals(moment.Target, reactor)) return false;

            // Shocking Grasp
            if (reactor.Boons.NoOpportunityAttacks) return false;

            // a charmed creature does not swing at its charmer
            if (reactor.HasFrom(Condition.Charmed, moment.Source)) return false;

            // SRD 5.2.1: "a creature that you can see" leaving your reach
            if (!fight.Sees(reactor, moment.Source)) return false;

            Cell? standing = fight.Field.Where(reactor);

            if (!standing.HasValue) return false;

            bool wasInReach = Battlefield.Distance(standing.Value, moment.From) <= Attack.Reach;
            bool stillInReach = Battlefield.Distance(standing.Value, moment.To) <= Attack.Reach;

            return wasInReach && !stillInReach;
        }

        public void Answer(Encounter fight, Actor reactor, Moment moment)
        {
            fight.Observer.Opportunity(reactor, moment.Source);

            // through the fight's own swing, so the one running away can Shield against it
            fight.Swing(reactor, moment.Source, Attack);
        }

        public override string ToString() => $"{Id} with {Attack.Id}";
    }
}
