using System.Linq;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): morale (cc_task_h 3.4)
    public sealed partial class Encounter
    {
        // --- morale --------------------------------------------------------------------------------
        //
        // A fight the campaign gave a morale (Morale.Leader, Morale.Half) breaks when that happens: the enemies still
        // standing stop fighting and use their turns to run for the nearest way off the map - a Disengage when the hero
        // is next to them, else a Dash - and step off it (Rout). A map with no way off, and they throw down their arms
        // there and then (Surrendered). Either way the fight is WON, its loot is the loot, and the story can tell a
        // rout from a slaughter ($fight_broke)

        // the books are MoraleWatch's (cc_task_j Part 1)
        public Morale Morale => _morale.Morale;

        // the leader whose fall breaks a Morale.Leader fight
        public Actor Leader => _morale.Leader;

        public bool Broken => _morale.Broken;

        public bool Surrendered => _morale.Surrendered;

        public void HoldMorale(Morale morale, Actor leader) =>
            _morale.Hold(morale, leader, Actors.Count(a => a.Side == Allegiance.Enemy));

        // has it broken, just now? told once, the moment it does
        bool Breaks()
        {
            if (!_morale.Breaks(() => Actors.Where(a => a.Side == Allegiance.Enemy).ToList(), _off.IsGone,
                                () => !Field.Exits.Any()))
                return false;

            Observer.Broke(Surrendered);

            return true;
        }

        // A BROKEN ENEMY'S TURN: to the nearest way off the map, and off it. A Disengage when one of its enemies is next
        // to it, else a Dash; then the walk, and off the board if it reached the edge
        public void Rout(Turn turn)
        {
            if (turn == null || turn.Ended) return;

            Actor me = turn.Actor;

            if (!me.CanAct || !(Field.Where(me) is Cell here))
            {
                EndTurn();
                return;
            }

            if (!Field.IsExit(here))
            {
                bool engaged = Field.Enemies(me).Any(a => !a.IsDown && Field.Distance(me, a) <= 1);

                if (engaged) Disengage(turn);
                else Dash(turn);

                Cell? exit = Field.Exits.Where(c => !Field.Occupies(c, me))
                                  .OrderBy(c => Battlefield.Distance(here, c))
                                  .ThenBy(c => c.Y).ThenBy(c => c.X)
                                  .Select(c => (Cell?)c)
                                  .FirstOrDefault();

                if (exit.HasValue) Walk(turn, exit.Value);
            }

            if (Field.Where(me) is Cell now && Field.IsExit(now) && !me.IsDown) Leave(me);

            if (!turn.Ended) EndTurn();
        }

        // stepped off the edge: gone from the fight for good, as the hero who flees is
        void Leave(Actor actor)
        {
            Field.Remove(actor);
            _off.Gone(actor);
            Observer.Left(actor);
            Judge();
        }
    }
}
