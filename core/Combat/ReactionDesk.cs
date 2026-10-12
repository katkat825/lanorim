using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Core.Combat
{
    // THE REACTION WINDOW'S OWN BOOKS: what each creature can answer a moment with, who decides whether it does, and
    // how many reactions it has left this round; and the window itself (Offer). Was Encounter's private state
    // (cc_task_j-split-classes-dev-hooks.md Part 1); Encounter keeps its public members and hands them here. The
    // content layer arms the reactions; a creature with none simply never reacts
    internal sealed class ReactionDesk
    {
        readonly Encounter _fight;
        readonly Dictionary<Actor, List<IReaction>> _answers = new();
        readonly Dictionary<Actor, IReactionChooser> _choosers = new();
        readonly Dictionary<Actor, int> _left = new();

        public ReactionDesk(Encounter fight) => _fight = fight;

        public void Arm(Actor actor, IReaction reaction)
        {
            if (actor == null || reaction == null) return;

            if (!_answers.TryGetValue(actor, out List<IReaction> list))
                _answers[actor] = list = new List<IReaction>();

            // one of each: arming the same opportunity attack twice must not offer it twice
            list.RemoveAll(r => r.Id == reaction.Id);
            list.Add(reaction);
        }

        public IReadOnlyList<IReaction> Of(Actor actor) =>
            actor != null && _answers.TryGetValue(actor, out List<IReaction> list)
                ? list
                : (IReadOnlyList<IReaction>)Array.Empty<IReaction>();

        // without one, an actor takes the first reaction offered - what the opportunity attack
        // always did, so a fight nobody configured plays the way it used to
        public void ChooseWith(Actor actor, IReactionChooser chooser)
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
        public void Offer(Moment moment, IEnumerable<Actor> who)
        {
            if (moment == null || who == null) return;

            foreach (Actor reactor in who.Where(a => a != null).ToList())
            {
                if (moment.Stopped) return;

                if (!reactor.CanAct) continue;

                // SRD 5.2.1's smites are bonus actions taken in answer to a moment rather than
                // reactions: they are paid from the reactor's own turn, and only on it
                List<IReaction> options = Of(reactor)
                    .Where(r => (r.Trigger == moment.Trigger || r.AlsoAnswers(moment)) && CanPay(reactor, r) &&
                                r.CanAnswer(_fight, reactor, moment))
                    .ToList();

                if (options.Count == 0) continue;

                IReaction chosen = ChooserOf(reactor).Choose(_fight, reactor, moment, options);

                // a chooser may only pick from what it was offered
                if (chosen == null || !options.Contains(chosen)) continue;

                if (!Pay(reactor, chosen)) continue;

                _fight.Observer.Reacted(reactor, chosen.Id, moment);

                chosen.Answer(_fight, reactor, moment);
            }
        }

        // a reaction is the round's; a bonus action taken in answer is the reactor's own turn's
        bool CanPay(Actor reactor, IReaction reaction)
        {
            Turn current = _fight.Current;

            return reaction.Cost == Spend.Bonus
                ? current != null && !current.Ended && ReferenceEquals(current.Actor, reactor) &&
                  current.Can(Spend.Bonus)
                : Left(reactor) > 0 && !reactor.Boons.Forbids(Forbid.Reactions);
        }

        bool Pay(Actor reactor, IReaction reaction) =>
            reaction.Cost == Spend.Bonus ? _fight.Current.Take(Spend.Bonus) : Take(reactor);

        public int Left(Actor actor) =>
            _left.TryGetValue(actor, out int left) ? left : 0;

        public bool Take(Actor actor)
        {
            if (actor == null || Left(actor) <= 0) return false;

            _left[actor]--;
            return true;
        }

        // a reaction is spent between your turns, so it is the round that hands it back (and joining a fight)
        public void HandBack(Actor actor, int reactions) => _left[actor] = reactions;
    }
}
