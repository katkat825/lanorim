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

            return Route.Between(Map, from.Value, to, c => Occupies(c, actor), c => StepCost(actor, c),
                                 (a, b) => !Forbids(actor, a, b));
        }

        public int CostOf(IReadOnlyList<Cell> route) => Route.Cost(Map, route);

        // what the route costs this mover, step by step as Walk pays it (StepCost)
        public int CostOf(IReadOnlyList<Cell> route, Actor mover) =>
            route == null ? 0 : route.Skip(1).Sum(c => StepCost(mover, c));

        // WHAT THE FIGHT ADDS TO A STEP (cc_task_e-shop-species-and-ui-notes.md 2.5): ground a spell made difficult (Spike
        // Growth, Web), and whether the mover drags a creature it grapples. the encounter sets both on its field
        public Func<Cell, Actor, bool> MadeRough { get; set; }

        public Func<Actor, bool> Drags { get; set; }

        // ONE STEP ONTO A SQUARE, IN SQUARES - the one cost the route (RouteFor), the flood (Reachable) and the walk
        // (Encounter.Walk) all pay, so a clicked route is never dearer than walking it square by square. difficult
        // ground is double, whether the map made it or a spell did, and the two don't stack (SRD's difficult terrain is a
        // yes or a no); a flyer ignores the map's. crawling one more (SRD 5.2.1 Prone), dragging the grappled one more
        public int StepCost(Actor mover, Cell to)
        {
            bool flying = mover?.IsFlying == true;
            bool rough = !flying && Map.At(to).MoveCost() > 1 || (MadeRough?.Invoke(to, mover) ?? false);
            bool crawling = mover != null && !flying && mover.Has(Condition.Prone);
            bool dragging = mover != null && (Drags?.Invoke(mover) ?? false);

            return (rough ? 2 : 1) + (crawling ? 1 : 0) + (dragging ? 1 : 0);
        }

        // a step it may not take: SRD 5.2.1 Frightened, one closer to what it fears
        public bool Forbids(Actor mover, Cell from, Cell to) => CloserToFear(mover, from, to);

        // SRD 5.2.1 Frightened: "can't willingly move closer to the source of its fear"
        public bool CloserToFear(Actor mover, Cell from, Cell to) =>
            mover != null && mover.Has(Condition.Frightened) &&
            mover.SourcesOf(Condition.Frightened)
                 .Any(s => !s.IsDown && Where(s) is Cell at && Distance(to, at) < Distance(from, at));

        // every square the actor can reach this turn, with what it costs to get there. used by the
        // UI to light the board up and by the AI to pick where to stand.
        // one flood from where it stands (Route.Reach), each step costed as CostOf costs a route
        public IReadOnlyDictionary<Cell, int> Reachable(Actor actor, int budgetSquares)
        {
            Cell? from = Where(actor);

            if (!from.HasValue || budgetSquares <= 0) return new Dictionary<Cell, int>();

            Dictionary<Cell, int> flood = Route.Reach(Map, from.Value, c => Occupies(c, actor), c => StepCost(actor, c),
                                                      budgetSquares, (a, b) => !Forbids(actor, a, b));

            // in the map's own order, not the flood's: the AI takes the first of equally good
            // squares, so the order is part of what a fight does
            var reached = new Dictionary<Cell, int>();

            foreach (Cell cell in Map.Cells)
                if (cell != from.Value && flood.TryGetValue(cell, out int cost))
                    reached[cell] = cost;

            return reached;
        }


        // a radius AoE: every square whose centre is within the radius and which the burst's
        // origin can see. the lines, cones and cubes below obey the same two rules - on the map,
        // and in sight of where it came from (decisions_checklist.md section 6, grid tactics).
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

        // SRD: an area spreads from its point of origin, and a square with no clear line back to it
        // is not in it - a fireball does not go round a wall
        public IEnumerable<Actor> Caught(Cell centre, int radiusSquares) =>
            Burst(centre, radiusSquares).Where(c => CanSee(centre, c)).Select(At)
                                        .Where(a => a != null);

        public IEnumerable<Cell> Line(Cell origin, Facing facing, int length, int width = 1) =>
            Visible(origin, Template.Line(origin, facing, length, width));

        public IEnumerable<Cell> Cone(Cell origin, Facing facing, int length) =>
            Visible(origin, Template.Cone(origin, facing, length));

        public IEnumerable<Cell> Cube(Cell origin, Facing facing, int side) =>
            Visible(origin, Template.Cube(origin, facing, side));

        public IEnumerable<Cell> Square(Cell centre, int side) =>
            Visible(centre, Template.Square(centre, side));

        // whoever is standing in a set of squares - any template's
        public IEnumerable<Actor> Caught(IEnumerable<Cell> squares) =>
            squares.Select(At).Where(a => a != null);

        IEnumerable<Cell> Visible(Cell origin, IEnumerable<Cell> squares) =>
            squares.Where(c => Map.Contains(c) && CanSee(origin, c));

        public IEnumerable<Actor> Adjacent(Cell cell) =>
            Burst(cell, 1).Where(c => c != cell).Select(At).Where(a => a != null);

        public IEnumerable<Actor> Enemies(Actor of) =>
            Pieces.Where(a => !ReferenceEquals(a, of) && a.Side != of.Side && !a.IsDown);

        public IEnumerable<Actor> Allies(Actor of) =>
            Pieces.Where(a => !ReferenceEquals(a, of) && a.Side == of.Side && !a.IsDown);

        // THE WAY OUT OF A FIGHT: a square on the edge of the map whose outer side has no wall on
        // it. a room drawn with its outline closed has none, and cannot be fled - which is the
        // author's call, made with the map builder by leaving a gap. a shut door is not a way out;
        // an opened one is (OpenDoor swaps it for a gap).
        public bool IsExit(Cell cell)
        {
            if (!Map.Contains(cell) || !Map.IsPassable(cell)) return false;

            return cell.X == 0 && Map.At(Border.West(cell)).IsOpen() ||
                   cell.X == Columns - 1 && Map.At(Border.East(cell)).IsOpen() ||
                   cell.Y == 0 && Map.At(Border.North(cell)).IsOpen() ||
                   cell.Y == Rows - 1 && Map.At(Border.South(cell)).IsOpen();
        }

        public IEnumerable<Cell> Exits => Map.Cells.Where(IsExit);

        // an open door is runtime state, not content: the map is immutable and this swaps in a new
        // one, so a save carries only the change (MapLayout's own comment)
        public void OpenDoor(Border border) => Map = Map.With(border, Edge.None);

        // a spell's walls going up on the board (Forcecage): the edges it sets, and what was
        // there before so taking them down puts it back
        public IReadOnlyDictionary<Border, Edge> Raise(IEnumerable<Border> borders, Edge edge)
        {
            var was = new Dictionary<Border, Edge>();

            foreach (Border border in borders.Where(Map.Contains).Distinct())
            {
                was[border] = Map.At(border);
                Map = Map.With(border, edge);
            }

            return was;
        }

        public void Lower(IReadOnlyDictionary<Border, Edge> was)
        {
            foreach (KeyValuePair<Border, Edge> put in was ?? new Dictionary<Border, Edge>())
                Map = Map.With(put.Key, put.Value);
        }

        public override string ToString() =>
            $"{Columns} x {Rows} battlefield, {Pieces.Count()} pieces on it";
    }
}
