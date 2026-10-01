using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): the reaction window
    public sealed partial class Encounter
    {
        // --- the reaction window ---------------------------------------------------------------

        // what each actor can answer a moment with, and who decides whether it does. the content
        // layer arms these; an actor with none simply never reacts
        readonly Dictionary<Actor, List<IReaction>> _answers = new();
        readonly Dictionary<Actor, IReactionChooser> _choosers = new();

        public void Arm(Actor actor, IReaction reaction)
        {
            if (actor == null || reaction == null) return;

            if (!_answers.TryGetValue(actor, out List<IReaction> list))
                _answers[actor] = list = new List<IReaction>();

            // one of each: arming the same opportunity attack twice must not offer it twice
            list.RemoveAll(r => r.Id == reaction.Id);
            list.Add(reaction);
        }

        public IReadOnlyList<IReaction> ReactionsOf(Actor actor) =>
            actor != null && _answers.TryGetValue(actor, out List<IReaction> list)
                ? list
                : (IReadOnlyList<IReaction>)Array.Empty<IReaction>();

        // without one, an actor takes the first reaction offered - what the opportunity attack
        // always did, so a fight nobody configured plays the way it used to
        public void ChooseReactionsWith(Actor actor, IReactionChooser chooser)
        {
            if (actor == null) return;

            _choosers[actor] = chooser ?? ReactionChoosers.Always;
        }

        IReactionChooser ChooserOf(Actor actor) =>
            _choosers.TryGetValue(actor, out IReactionChooser chooser)
                ? chooser
                : ReactionChoosers.Always;

        // THE WINDOW. offered to each of `who` in turn: the reactions it has that answer this
        // trigger and can answer this particular moment, and its chooser picks one or none.
        // choosing spends the reaction - one per creature per round, whatever it was spent on -
        // and the engine does what was chosen without second-guessing it. stops early once the
        // moment is stopped, because there is nothing left to react to.
        public void Offer(Moment moment, params Actor[] who) =>
            Offer(moment, (IEnumerable<Actor>)who);

        public void Offer(Moment moment, IEnumerable<Actor> who)
        {
            if (moment == null || who == null) return;

            foreach (Actor reactor in who.Where(a => a != null).ToList())
            {
                if (moment.Stopped) return;

                if (!reactor.CanAct) continue;

                // SRD 5.2.1's smites are bonus actions taken in answer to a moment rather than
                // reactions: they are paid from the reactor's own turn, and only on it
                List<IReaction> options = ReactionsOf(reactor)
                    .Where(r => (r.Trigger == moment.Trigger || r.AlsoAnswers(moment)) && CanPay(reactor, r) &&
                                r.CanAnswer(this, reactor, moment))
                    .ToList();

                if (options.Count == 0) continue;

                IReaction chosen = ChooserOf(reactor).Choose(this, reactor, moment, options);

                // a chooser may only pick from what it was offered
                if (chosen == null || !options.Contains(chosen)) continue;

                if (!Pay(reactor, chosen)) continue;

                Observer.Reacted(reactor, chosen.Id, moment);

                chosen.Answer(this, reactor, moment);
            }
        }

        // what an actor swings with when something runs past. set by the content layer; without
        // one, an actor simply doesn't take opportunity attacks
        public void ArmOpportunity(Actor actor, Attack attack)
        {
            // SRD 5.2.1: one melee attack - a bow is no opportunity attack
            if (actor == null || attack == null || attack.IsRanged) return;

            Arm(actor, new OpportunityAttack(attack));
        }

        public Attack OpportunityAttackOf(Actor actor) =>
            actor != null && !actor.IsDown && actor.CanAct
                ? ReactionsOf(actor).OfType<OpportunityAttack>().FirstOrDefault()?.Attack
                : null;

        // a reaction is the round's; a bonus action taken in answer is the reactor's own turn's
        bool CanPay(Actor reactor, IReaction reaction) =>
            reaction.Cost == Spend.Bonus
                ? Current != null && !Current.Ended && ReferenceEquals(Current.Actor, reactor) &&
                  Current.Can(Spend.Bonus)
                : ReactionsLeft(reactor) > 0 && !reactor.Boons.Forbids(Forbid.Reactions);

        bool Pay(Actor reactor, IReaction reaction) =>
            reaction.Cost == Spend.Bonus ? Current.Take(Spend.Bonus) : TakeReaction(reactor);

        public int ReactionsLeft(Actor actor) =>
            _reactions.TryGetValue(actor, out int left) ? left : 0;

        public bool TakeReaction(Actor actor)
        {
            if (actor == null || ReactionsLeft(actor) <= 0) return false;

            _reactions[actor]--;
            return true;
        }

        public Blow Hit(Turn turn, Actor target, Attack attack,
                        Spend spend = Spend.Action,
                        Advantage extra = Advantage.Flat,
                        IReadOnlyList<Rider> riders = null)
        {
            if (turn == null || turn.Ended || target == null || attack == null) return null;

            if (!Field.InRange(turn.Actor, target, attack.Reaches)) return null;

            // a dropped weapon is not in hand
            if (!turn.Actor.CanUse(attack)) return null;

            // SRD 5.2.1 Charmed: it can't attack the charmer
            if (turn.Actor.HasFrom(Condition.Charmed, target)) return null;

            // Gaseous Form: a misty cloud can't attack
            if (turn.Actor.Boons.Forbids(Forbid.Attacks)) return null;

            // Slow's "only one attack", and Haste's extra action buying a single weapon attack
            if (!turn.TakeAttack(spend, attack)) return null;

            // attacking gives away where you were hiding - Strike.Roll spends the hidden boon after
            // its advantage has been read, not before
            return Swing(turn.Actor, target, attack, Band(turn.Actor, target, attack).And(extra),
                         riders);
        }

        // what the range does to an attack roll: a shot past its normal range is at disadvantage
        // (the only range band v1 keeps), and so is a shot with an enemy beside you
        public Advantage Band(Actor attacker, Actor target, Attack attack)
        {
            if (attack == null) return Advantage.Flat;

            bool thrownFar = attack.Thrown && Field.Distance(attacker, target) > attack.Reach;

            if (!attack.IsRanged && !thrownFar) return Advantage.Flat;

            bool far = Field.Distance(attacker, target) > attack.Range;

            return far || Crowded(attacker) ? Advantage.Disadvantage : Advantage.Flat;
        }

        // SRD 5.2.1 Ranged Attacks in Close Combat: "within 5 feet of an enemy who can see you and
        // who doesn't have the Incapacitated condition" - weapons and spells alike
        public bool Crowded(Actor attacker) =>
            Field.Where(attacker) is Cell here &&
            Field.Adjacent(here).Any(a => a.Side != attacker.Side && !a.IsDown && !a.IsIncapacitated &&
                                          Sees(a, attacker));

        // SRD 5.2.1 Frightened: the disadvantage holds while the source is in line of sight. a fear
        // with no source on record is taken to be in sight
        public bool FearInSight(Actor actor)
        {
            if (actor == null || !actor.Has(Condition.Frightened)) return false;

            IReadOnlyList<Actor> sources = actor.SourcesOf(Condition.Frightened);

            return sources.Count == 0 || sources.Any(s => !s.IsDown && Sees(actor, s));
        }

        // one attack, start to finish, with both reaction windows in it: the target may answer the
        // hit before it lands, and answer the damage after. everything that swings in a fight goes
        // through here - a turn's attack and an opportunity attack alike
        public Blow Swing(Actor attacker, Actor target, Attack attack,
                          Advantage extra = Advantage.Flat, IReadOnlyList<Rider> riders = null)
        {
            bool close = Field.Distance(attacker, target) <= 1;
            int cover = Cover(attacker, target);

            // a statblock's Pack Tactics, Bloodied Fury, Sunlight Sensitivity (Encounter.Knacks.cs)
            extra = extra.And(KnackLean(attacker, target, attack));

            Attempt attempt = Strike.Roll(_resolver, attacker, target, attack, extra, close,
                                          Sees(attacker, target), Sees(target, attacker), cover,
                                          FearInSight(attacker));

            // an attack roll gives an Invisibility away (SRD 5.2.1), and a hidden creature too
            AttackRolled?.Invoke(attacker);
            Reveal(attacker);

            if (attempt.Succeeded)
            {
                Offer(Moment.Hit(attacker, target, attempt), target);

                // the roll stays; the armor class it has to beat may not have
                attempt = attempt.Rejudged(target.ArmorClass + cover);
            }

            attempt = Strike.Decoyed(_resolver, attacker, target, attempt);

            // hit or miss is said now, before the damage dice are thrown
            Observer.Judged(attacker, target, attempt);

            // the statblock's own riders ride with whatever the swing brought
            if (attack.OnHit.Count > 0)
                riders = (riders ?? Array.Empty<Rider>()).Concat(attack.OnHit).ToList();

            // conditions the target already had are not the rider's to end
            List<Condition> had = attack.OnHit.Where(r => r.UntilTargetsNextTurn && target.Has(r.Condition))
                                              .Select(r => r.Condition).ToList();

            Blow blow = Strike.Land(_resolver, attacker, target, attack, attempt, riders);

            foreach (Rider timed in attack.OnHit.Where(r => r.UntilTargetsNextTurn && !had.Contains(r.Condition) &&
                                                            target.Has(r.Condition)))
                _untilNextTurn.Add(new Lapse(target, timed.Condition,
                                             ReferenceEquals(Current?.Actor, target)));

            Observer.Struck(blow);

            if (blow.Hit)
                Observer.Dealt(new Harm(attacker, target, attack.NameKey, attack.DamageFor(attacker, attempt.IsCritical),
                                        blow.Rolled, blow.Suffered, attack.DamageTypeFor(attacker, target)));

            if (blow.Hit && blow.Suffered > 0)
                Hurt(attacker, target, blow.Suffered, attack.DamageTypeFor(attacker, target), attempt.IsCritical, blow.Rolled);

            // SRD 5.2.1's smites: a bonus action taken right after the attacker's own hit
            if (blow.Hit) Offer(Moment.Struck(attacker, target, blow), attacker);

            return blow;
        }

        // hit points came off a creature. whatever keeps books on damage hears it first (a Sleep
        // ends, a Hideous Laughter saves again), then the creature is offered its reaction to
        // being hurt if it is still standing to take one. the type, a critical and the damage
        // before the hit-point floor are for what keeps a creature up (Undead Fortitude)
        public void Hurt(Actor attacker, Actor target, int suffered, DamageType type = DamageType.None,
                         bool critical = false, int dealt = 0)
        {
            if (target == null || suffered <= 0) return;

            if (target.IsDown) HoldsOn(target, System.Math.Max(dealt, suffered), type, critical);

            // SRD 5.2.1 Unconscious: it drops whatever it's holding
            if (target.Has(Condition.Unconscious)) Drop(target);

            Damaged?.Invoke(attacker, target, suffered);

            // EVERY FALL IS TOLD, a spell's as much as a sword's: until 2026-10-03 only a swing told it, so a
            // monster a spell killed neither toppled on the board nor went down in the log. Told after whatever
            // can keep a creature up has had its say (Undead Fortitude above, Relentless Endurance on Damaged),
            // so one that stays up was never said to fall
            if (target.IsDown) Observer.Downed(target);
            else Offer(Moment.Damaged(attacker, target, suffered), target);
        }

        // (attacker, target, hit points lost). the attacker may be null - a zone with nobody's
        // hand on it, a fall
        public event Action<Actor, Actor, int> Damaged;

        // somebody just made an attack roll
        public event Action<Actor> AttackRolled;

        // somebody took one step of a walk
        public event Action<Actor> Stepped;

        // a corpse is rising as a statblock on its master's side: Finger of Death's Zombie. the
        // content layer knows statblocks, so it answers with Join
        public event Action<Actor, string, Actor> Rising;

        public void Raise(Actor corpse, string statblock, Actor master) =>
            Rising?.Invoke(corpse, statblock, master);

        // A CREATURE JOINING A FIGHT ALREADY UNDER WAY: it rolls initiative and takes its place in
        // the order - after the creature it belongs to, when it belongs to one
        public bool Join(Actor actor, Cell cell, ActionBudget budget = null, Actor after = null)
        {
            if (actor == null || !Field.Place(actor, cell)) return false;

            _budgets[actor] = budget ?? new ActionBudget();
            _reactions[actor] = _budgets[actor].ReactionsFor(Round);

            InitiativeRoll roll = Initiative.Roll(_resolver, new[] { actor }).First();

            int at = after != null ? _order.FindIndex(r => ReferenceEquals(r.Actor, after)) + 1 : _order.Count;

            if (at <= 0) at = _order.Count;

            _order.Insert(at, roll);

            if (at < _next) _next++;

            Observer.Moved(actor, new[] { cell });
            return true;
        }

        // somebody at 0 hit points, and not dead, is made Stable
        public bool Stabilize(Actor creature) => creature != null && creature.Stabilize();

        // a creature's condition changed outside a turn's own actions - a spell's expiry, a save
        public void Changed(Actor creature, Condition condition, bool gained)
        {
            if (gained && condition == Condition.Unconscious) Drop(creature);

            // a grappler that can't act lets go
            if (gained && condition.Incapacitates()) LetGo();

            Observer.ConditionChanged(creature, condition, gained);
        }

        void Drop(Actor creature)
        {
            if (creature != null && !creature.Disarmed) creature.Disarm(Field.Where(creature));
        }
    }
}
