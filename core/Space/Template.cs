using System;
using System.Collections.Generic;

namespace Core.Space
{
    // which way a line, a cone or a cube is thrown. four of them, on purpose: the grid's own axes
    // are the only directions where a template's squares are not a judgement call, and SRD's own
    // grid variant leaves the diagonal to the table. north is toward row 0.
    public enum Facing
    {
        North,
        East,
        South,
        West,
    }

    // the squares an area of effect covers, for the shapes that come *out of* something rather
    // than sit centred on a square. radius is Battlefield.Burst; this is the other three SRD shapes.
    //
    // THE RULE FOR EVERY SHAPE IS THE SAME ONE: a square is in if its centre is inside the shape.
    // the shape starts at the edge of the origin square it faces, so the origin itself is never
    // in it - SRD's "you are not in the area unless you choose to be" for a cone, and the caster
    // does not stand in their own lightning. measured that way a cone comes out exactly: a
    // 60-foot cone is 72 squares, which is 60 x 60 / 2 in square feet over 25.
    //
    // these are geometry only. what the walls block and what is off the map is the battlefield's
    // question, the same way Burst answers it.
    public static class Template
    {
        // a line `length` squares long and `width` squares wide. an even width cannot sit centred
        // on a row of squares, so the extra column goes on the left of the facing - one fixed
        // answer, so the same aim covers the same squares every time
        public static IEnumerable<Cell> Line(Cell origin, Facing facing, int length, int width = 1)
        {
            width = Math.Max(1, width);

            for (int along = 1; along <= length; along++)
                for (int across = -width / 2; across <= (width - 1) / 2; across++)
                    yield return Step(origin, facing, along, across);
        }

        // SRD: a cone's width at any point along it equals that point's distance from the origin.
        // the centre of the k-th square out is k - 1/2 from the origin, so its half-width there is
        // (k - 1/2) / 2, and a square `across` columns off the axis is in when 2 * |across| is at
        // most k - 1/2 - which for whole numbers is k - 1.
        public static IEnumerable<Cell> Cone(Cell origin, Facing facing, int length)
        {
            for (int along = 1; along <= length; along++)
            {
                int half = (along - 1) / 2;

                for (int across = -half; across <= half; across++)
                    yield return Step(origin, facing, along, across);
            }
        }

        // SRD: a cube's point of origin is on one of its faces. thrown from a creature, that face
        // is against the creature's own square - Thunderwave's 15-foot cube is the three by three
        // in front of you.
        public static IEnumerable<Cell> Cube(Cell origin, Facing facing, int side) =>
            Line(origin, facing, side, side);

        // a square area put down on the board rather than thrown from somebody: Web's 20-foot cube,
        // Entangle's 20-foot square. centred on the square aimed at; an even side cannot be, so the
        // extra row and column go right and down - one fixed answer again
        public static IEnumerable<Cell> Square(Cell centre, int side)
        {
            side = Math.Max(1, side);

            int from = -(side - 1) / 2;

            for (int dy = from; dy < from + side; dy++)
                for (int dx = from; dx < from + side; dx++)
                    yield return new Cell(centre.X + dx, centre.Y + dy);
        }

        // which way to throw a template so it goes at a square the player pointed at: the axis the
        // square is further along. a dead diagonal goes north or south, because a tie has to go
        // somewhere and the same click must always give the same answer
        public static Facing Toward(Cell from, Cell to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            if (Math.Abs(dx) > Math.Abs(dy)) return dx > 0 ? Facing.East : Facing.West;

            return dy > 0 ? Facing.South : Facing.North;
        }

        // `along` squares forward, `across` squares to the right of the facing
        static Cell Step(Cell origin, Facing facing, int along, int across) => facing switch
        {
            Facing.North => new Cell(origin.X + across, origin.Y - along),
            Facing.East => new Cell(origin.X + along, origin.Y + across),
            Facing.South => new Cell(origin.X - across, origin.Y + along),
            _ => new Cell(origin.X - along, origin.Y - across),
        };

        public static string Id(this Facing facing) => facing.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out Facing facing)
        {
            foreach (Facing f in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
            {
                if (!string.Equals(f.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                facing = f;
                return true;
            }

            facing = Facing.North;
            return false;
        }
    }
}
