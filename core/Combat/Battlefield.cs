using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // the map, and who is standing where on it. all the geometry questions a fight asks, answered
    // in squares - core/Space does the hard part and this names it in the fight's words.
    public sealed class Battlefield
    {
        readonly Grid<Actor> _pieces;

        public Battlefield(MapLayout map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            _pieces = new Grid<Actor>(map.Columns, map.Rows);
        }

        public MapLayout Map { get; private set; }

        public int Columns => Map.Columns;

        public int Rows => Map.Rows;

        public IEnumerable<Actor> Pieces =>
            Map.Cells.Select(c => _pieces.At(c)).Where(a => a != null);

        public Actor At(Cell cell) => _pieces.At(cell);

        public Cell? Where(Actor actor) => _pieces.CellOf(actor);

        public bool IsOccupied(Cell cell) => _pieces.IsOccupied(cell);

        public bool Place(Actor actor, Cell cell) =>
            Map.IsPassable(cell) && _pieces.Place(actor, cell);

        public bool Remove(Actor actor) => _pieces.Remove(actor);

        // a downed actor still holds its square - the mini stays on the board until it is cleared
        public bool Occupies(Cell cell, Actor mover) =>
            _pieces.IsOccupied(cell) && !ReferenceEquals(_pieces.At(cell), mover);

        // a diagonal is one square, matching Route's movement cost and SRD's optional-but-normal
        // grid rule
        public static int Distance(Cell a, Cell b) =>
            Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        public int Distance(Actor a, Actor b)
        {
            Cell? from = Where(a);
            Cell? to = Where(b);

            return from.HasValue && to.HasValue ? Distance(from.Value, to.Value) : int.MaxValue;
        }

        public bool CanSee(Cell from, Cell to) => Sight.Clear(Map, from, to);

        public bool CanSee(Actor a, Actor b)
        {
            Cell? from = Where(a);
            Cell? to = Where(b);

            return from.HasValue && to.HasValue && CanSee(from.Value, to.Value);
        }

        // in range *and* in sight. v1 does no cover and no flanking
        // (decisions_checklist.md section 6) - a wall between you is the only thing that stops a
        // shot, and that is what Sight already answers.
        public bool InRange(Actor from, Actor to, int squares) =>
            from != null && to != null && Distance(from, to) <= squares && CanSee(from, to);

        public IReadOnlyList<Cell> RouteFor(Actor actor, Cell to)
        {
            Cell? from = Where(actor);

            if (!from.HasValue) return null;

            return Route.Between(Map, from.Value, to, c => Occupies(c, actor));
        }

        public int CostOf(IReadOnlyList<Cell> route) => Route.Cost(Map, route);

        // every square the actor can reach this turn, with what it costs to get there. used by the
        // UI to light the board up and by the AI to pick where to stand.
        public IReadOnlyDictionary<Cell, int> Reachable(Actor actor, int budgetSquares)
        {
            var reached = new Dictionary<Cell, int>();

            Cell? from = Where(actor);

            if (!from.HasValue || budgetSquares <= 0) return reached;

            foreach (Cell cell in Map.Cells)
            {
                if (cell == from.Value || Occupies(cell, actor) || !Map.IsPassable(cell)) continue;

                IReadOnlyList<Cell> route = RouteFor(actor, cell);

                if (route == null) continue;

                int cost = CostOf(route);

                if (cost <= budgetSquares) reached[cell] = cost;
            }

            return reached;
        }

        // a radius AoE: every square whose centre is within the radius and which the burst's
        // origin can see. one shape, and it is the only shape v1 has
        // (decisions_checklist.md section 6, grid tactics).
        public IEnumerable<Cell> Burst(Cell centre, int radiusSquares)
        {
            for (int y = centre.Y - radiusSquares; y <= centre.Y + radiusSquares; y++)
                for (int x = centre.X - radiusSquares; x <= centre.X + radiusSquares; x++)
                {
                    var cell = new Cell(x, y);

                    if (!Map.Contains(cell)) continue;
                    if (Distance(centre, cell) > radiusSquares) continue;
                    if (!CanSee(centre, cell)) continue;

                    yield return cell;
                }
        }

        public IEnumerable<Actor> Caught(Cell centre, int radiusSquares) =>
            Burst(centre, radiusSquares).Select(At).Where(a => a != null);

        public IEnumerable<Actor> Adjacent(Cell cell) =>
            Burst(cell, 1).Where(c => c != cell).Select(At).Where(a => a != null);

        public IEnumerable<Actor> Enemies(Actor of) =>
            Pieces.Where(a => !ReferenceEquals(a, of) && a.Side != of.Side && !a.IsDown);

        public IEnumerable<Actor> Allies(Actor of) =>
            Pieces.Where(a => !ReferenceEquals(a, of) && a.Side == of.Side && !a.IsDown);

        // an open door is runtime state, not content: the map is immutable and this swaps in a new
        // one, so a save carries only the change (MapLayout's own comment)
        public void OpenDoor(Border border) => Map = Map.With(border, Edge.None);

        // the pieces grid was sized from the map, so a replacement has to be the same shape -
        // otherwise a piece would be standing off the edge of its own board
        public void Reshape(MapLayout map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            if (map.Columns != Columns || map.Rows != Rows)
                throw new ArgumentException(
                    $"the battlefield is {Columns} x {Rows} and the new map is " +
                    $"{map.Columns} x {map.Rows}; reshaping cannot change the size", nameof(map));

            Map = map;
        }

        public override string ToString() =>
            $"{Columns} x {Rows} battlefield, {Pieces.Count()} pieces on it";
    }
}
