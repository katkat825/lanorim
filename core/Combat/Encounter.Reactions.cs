using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): the reaction window. Its books and the window itself are ReactionDesk's
    // (cc_task_j-split-classes-dev-hooks.md Part 1); these are the fight's own doors onto it
    public sealed partial class Encounter
    {
        // --- the reaction window ---------------------------------------------------------------

        public void Arm(Actor actor, IReaction reaction) => _reactions.Arm(actor, reaction);

        public IReadOnlyList<IReaction> ReactionsOf(Actor actor) => _reactions.Of(actor);

        // without one, an actor takes the first reaction offered
        public void ChooseReactionsWith(Actor actor, IReactionChooser chooser) => _reactions.ChooseWith(actor, chooser);

        // THE WINDOW (ReactionDesk.Offer): each of `who` in turn may answer the moment, once a round
        public void Offer(Moment moment, params Actor[] who) =>
            Offer(moment, (IEnumerable<Actor>)who);

        public void Offer(Moment moment, IEnumerable<Actor> who) => _reactions.Offer(moment, who);

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

        public int ReactionsLeft(Actor actor) => _reactions.Left(actor);

        public bool TakeReaction(Actor actor) => _reactions.Take(actor);
    }
}
