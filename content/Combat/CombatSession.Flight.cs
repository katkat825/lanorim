using System;
using System.Collections.Generic;
using Content.Classes;
using Core.Space;

namespace Content.Combat
{
    public sealed partial class CombatSession
    {
        // --- a flight (the Fairy's Flutter, cc_task_m2 Part 4) -------------------------------------------------------

        // the squares the selected flight could land on, with what each costs (Encounter.FlightReach)
        IReadOnlyDictionary<Cell, int> FlightSquares() =>
            MyTurn && Selected?.Feature?.Flight is { } flight
                ? Fight.FlightReach(Turn, flight, Selected.Feature.Id)
                : new Dictionary<Cell, int>();

        // the route the flight would take to the square under the mouse, lit as a template is
        Preview FlightPreview(Cell? square)
        {
            bool legal = square.HasValue && FlightSquares().ContainsKey(square.Value);

            return new Preview
            {
                Legal = legal,
                WhyNotKey = legal ? null : Why("too_far"),
                Template = legal ? Fight.FlightRoute(Turn, square.Value, Selected.Feature.Flight, Selected.Feature.Id)
                                 : Array.Empty<Cell>(),
            };
        }

        // the flight itself: up to its feet and what's left of the move, then the use is spent. It costs what the
        // feature's use_time says (the Flutter: nothing, since it is the move)
        ActionResult DoFlight(Feature feature, Cell? square)
        {
            if (Hero.UsesLeft(feature) <= 0) return ActionResult.No(Why("no_uses"));

            if (square == null || !FlightSquares().ContainsKey(square.Value)) return ActionResult.No(Why("too_far"));

            if (!Turn.Take(feature.UseTime))
                return ActionResult.No(Why(feature.UseTime == Core.Combat.Spend.Bonus ? "no_bonus" : "no_action"));

            IReadOnlyList<Cell> flown = Fight.Fly(Turn, square.Value, feature.Flight, feature.Id);

            if (flown.Count < 2) return ActionResult.No(Why("too_far"));

            Hero.Invoke(feature, Fight.Resolver);

            return new ActionResult { Done = true, Walked = flown };
        }
    }
}
