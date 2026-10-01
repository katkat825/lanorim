using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): the other things a turn can be spent on
    public sealed partial class Encounter
    {
        // --- the other things a turn can be spent on -------------------------------------------

        // SRD: Dash doubles your movement for the turn - another turn's worth of speed on top
        public bool Dash(Turn turn, Spend spend = Spend.Action)
        {
            if (!CanSpendOn(turn, spend, Manoeuvre.Dash)) return false;

            turn.Hasten();
            return true;
        }

        // SRD: Disengage - your movement provokes no opportunity attacks for the rest of the turn
        public bool Disengage(Turn turn, Spend spend = Spend.Action)
        {
            if (!CanSpendOn(turn, spend, Manoeuvre.Disengage)) return false;

            turn.Disengage();
            return true;
        }

        // SRD Prone: standing up costs half your speed (Turn.StandUp). Told like any condition ending, so
        // the board stands the piece back up (cc_task_working-notes-10-01.md 1.2: the rules cleared Prone and
        // the mini stayed on its back, because nothing said so)
        public bool StandUp(Turn turn)
        {
            if (turn == null || !turn.StandUp()) return false;

            Observer.ConditionChanged(turn.Actor, Condition.Prone, false);
            return true;
        }

        public const string Hidden = "hidden";

        // SRD 5.2.1 Hide (p.183): a DC 15 Dexterity (Stealth) check, only while no enemy sees you
        // (CanHide). on a success you have the Invisible condition until you attack, cast or are
        // found (Reveal). being found by a searcher is campaign narration; the fight does not
        // model searching.
        public Attempt Hide(Turn turn, Spend spend = Spend.Action)
        {
            if (turn == null || !CanHide(turn.Actor)) return null;

            if (!CanSpendOn(turn, spend, Manoeuvre.Hide)) return null;

            Attempt attempt = Checks.Check(_resolver, turn.Actor, Skill.Stealth, HideDc);

            // SRD 5.2.1 Hide: on a success, the Invisible condition - until it attacks, casts or
            // is found (finding is the narrator's)
            if (attempt.Succeeded && turn.Actor.Apply(Condition.Invisible))
            {
                _hidden.Add(turn.Actor);
                Changed(turn.Actor, Condition.Invisible, true);
            }

            return attempt;
        }

        // SRD 5.2.1: "Heavily Obscured or behind Three-Quarters Cover or Total Cover, and ... out
        // of any enemy's line of sight"
        public bool CanHide(Actor actor) =>
            actor != null &&
            Actors.Where(e => e.Side != actor.Side && !e.IsDown && Field.Where(e).HasValue)
                  .All(e => !Sees(e, actor) || Cover(e, actor) >= 5);

        readonly HashSet<Actor> _hidden = new();

        // a hidden creature attacking or casting is hidden no more
        public void Reveal(Actor actor)
        {
            if (actor == null || !_hidden.Remove(actor)) return;

            actor.Remove(Condition.Invisible);
            Changed(actor, Condition.Invisible, false);
        }

        public const int HideDc = 15;

        // Dash, Disengage and Hide are actions anyone can take. a bonus action may only be spent
        // on one when a feature says so - the Rogue's Cunning Action
        bool CanSpendOn(Turn turn, Spend spend, Manoeuvre manoeuvre)
        {
            if (turn == null || turn.Ended || Over) return false;

            if (spend == Spend.Bonus && !turn.Actor.QuickOnBonus.HasFlag(manoeuvre)) return false;

            if (spend != Spend.Action && spend != Spend.Bonus) return false;

            // Haste's extra action may be a Dash, a Disengage or a Hide
            return turn.Take(spend) || spend == Spend.Action && turn.TakeLimited();
        }

        public bool Afflict(Actor actor, Condition condition)
        {
            if (actor == null || !actor.Apply(condition)) return false;

            Observer.ConditionChanged(actor, condition, true);

            return true;
        }

        public bool Relieve(Actor actor, Condition condition)
        {
            if (actor == null || !actor.Remove(condition)) return false;

            Observer.ConditionChanged(actor, condition, false);

            return true;
        }

        // the campaign or the narrator ending the fight as a flight - "you run, and they let you"
        public void Flee()
        {
            Outcome = Outcome.Fled;
            Observer.Ended(Outcome);
        }

        // FLEEING, THE WAY A TABLE EXPECTS IT: get to an open edge of the map and step off it
        // (decisions_checklist.md section 3, "whatever is more expected and is easier"). there is
        // no roll and no special action - the cost is the run to the edge, which walks out of
        // everybody's reach and hands each of them an opportunity attack on the way, unless the
        // hero spent an action on Disengage first. only the hero flees; a monster that runs is
        // the campaign's business, not the rules'.
        public bool CanFlee(Turn turn) =>
            turn != null && !turn.Ended && !Over &&
            turn.Actor.Side == Allegiance.Hero && !turn.Actor.IsDown &&
            Field.Where(turn.Actor) is Cell here && Field.IsExit(here);

        public bool Flee(Turn turn)
        {
            if (!CanFlee(turn)) return false;

            turn.End();
            Flee();
            return true;
        }
    }
}
