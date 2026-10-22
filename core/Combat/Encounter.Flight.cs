using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): A SHORT FLIGHT ON THE MOVE (cc_task_m2 Part 4, the Fairy's Flutter). Not a second
    // flying rule: the Flying status - a Fly Speed boon, the one Fly and Gaseous Form put on (BoonSpec.FlySpeed) - worn
    // for the length of one move, and taken off when it lands. While it's on, the move is Walk's: difficult ground costs
    // a flier nothing extra (Battlefield.StepCost); it crosses the squares a flier crosses (v1 has no altitude, so a
    // chasm stops it as it stops a walker); it ends on a square the route can stand on; and every step provokes an
    // opportunity attack as a walking step does. It goes no further than the flight's feet, and no further than what is
    // left of the turn's movement, since it "uses your normal movement"
    public sealed partial class Encounter
    {
        // every square the flight could end on this turn, with what each costs in squares
        public IReadOnlyDictionary<Cell, int> FlightReach(Turn turn, BoonSpec flight, string source) =>
            CanFly(turn, flight)
                ? Aloft(turn.Actor, flight, source, () => Field.Reachable(turn.Actor, FlightSquares(turn, flight)))
                : new Dictionary<Cell, int>();

        // the way the flight would go there, as Walk would take it with the Flying status on
        public IReadOnlyList<Cell> FlightRoute(Turn turn, Cell destination, BoonSpec flight, string source) =>
            CanFly(turn, flight)
                ? Aloft(turn.Actor, flight, source, () => Field.RouteFor(turn.Actor, destination)) ?? Array.Empty<Cell>()
                : Array.Empty<Cell>();

        // the flight itself: the squares covered, as Walk returns them, or none when the square is out of its reach
        public IReadOnlyList<Cell> Fly(Turn turn, Cell destination, BoonSpec flight, string source)
        {
            if (!CanFly(turn, flight)) return Array.Empty<Cell>();

            return Aloft(turn.Actor, flight, source, () =>
                Field.Reachable(turn.Actor, FlightSquares(turn, flight)).ContainsKey(destination)
                    ? Walk(turn, destination)
                    : Array.Empty<Cell>());
        }

        static bool CanFly(Turn turn, BoonSpec flight) =>
            turn != null && !turn.Ended && !turn.Actor.IsRooted && flight != null && flight.FlySpeed > 0;

        static int FlightSquares(Turn turn, BoonSpec flight) =>
            Math.Min(turn.Movement, flight.FlySpeed) / Turn.FeetPerSquare;

        // the Flying status on for as long as the move takes, and off again whatever happens in it
        static T Aloft<T>(Actor mover, BoonSpec flight, string source, Func<T> move)
        {
            mover.Boons.Add(Boon.Of(flight with { Duration = Duration.TurnEnd }, source, source, mover));

            try
            {
                return move();
            }
            finally
            {
                mover.Boons.EndFrom(source);
            }
        }
    }
}
