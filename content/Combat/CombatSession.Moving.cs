using System;
using System.Collections.Generic;
using Content.Sheet;
using Core.Combat;
using Core.Space;

namespace Content.Combat
{
    public sealed partial class CombatSession
    {
        // --- moving --------------------------------------------------------------------------------

        // every square the hero could walk to with what is left of the turn, and what each costs
        public IReadOnlyDictionary<Cell, int> Reachable() =>
            MyTurn && !Hero.Actor.IsRooted
                ? Fight.Field.Reachable(Hero.Actor, Turn.SquaresLeft)
                : new Dictionary<Cell, int>();

        public IReadOnlyList<Cell> PathTo(Cell cell) =>
            MyTurn ? Fight.Field.RouteFor(Hero.Actor, cell) ?? (IReadOnlyList<Cell>)Array.Empty<Cell>()
                   : Array.Empty<Cell>();

        public ActionResult Move(Cell cell)
        {
            if (!MyTurn) return ActionResult.No(Why("not_on_your_turn"));

            if (Hero.Actor.IsRooted) return ActionResult.No(Why("rooted"));

            if (!Reachable().ContainsKey(cell)) return ActionResult.No(Why("too_far"));

            IReadOnlyList<Cell> walked = Fight.Walk(Turn, cell);

            Battle.Magic.Check(Fight.Actors);
            Fight.Judge();

            if (Fight.Over) Phase = SessionPhase.Over;

            return new ActionResult { Done = walked.Count > 1, Walked = walked };
        }

        // the open edge squares the hero could run off the map from
        public IEnumerable<Cell> Exits => Fight.Field.Exits;

        public override string ToString() =>
            $"{Phase}: {Fight}" + (Turn != null ? $", {Turn}" : "") +
            (Selected != null ? $", aiming {Selected.Id}" : "");
    }
}
