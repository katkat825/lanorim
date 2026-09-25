using System;
using System.Collections.Generic;

namespace Core.Space
{
    // symmetric by construction - one segment between two centres, not a ray, so A sees B iff B sees A
    // neither end blocks - only what's between stops a line; the corner rule matches Route's
    public static class Sight
    {
        public static bool Clear(MapLayout map, Cell from, Cell to)
        {
            if (map == null) return false;

            if (from == to) return true;

            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            int nx = dx < 0 ? -dx : dx;
            int ny = dy < 0 ? -dy : dy;

            int sx = dx > 0 ? 1 : dx < 0 ? -1 : 0;
            int sy = dy > 0 ? 1 : dy < 0 ? -1 : 0;

            int x = from.X;
            int y = from.Y;

            // half-squares compared by cross-multiplication - all integer, so a corner decides the same from both ends
            int across = 0;
            int along = 0;

            while (across < nx || along < ny)
            {
                var here = new Cell(x, y);
                long decision = (long)(1 + 2 * across) * ny - (long)(1 + 2 * along) * nx;

                if (decision == 0)
                {
                    var corner = new Cell(x + sx, y + sy);
                    var sideways = new Cell(x + sx, y);
                    var forward = new Cell(x, y + sy);

                    bool round = (Steps(map, here, sideways, to) && Steps(map, sideways, corner, to))
                              || (Steps(map, here, forward, to) && Steps(map, forward, corner, to));

                    if (!round) return false;

                    x += sx;
                    y += sy;
                    across++;
                    along++;
                }
                else if (decision < 0)
                {
                    if (!Steps(map, here, new Cell(x + sx, y), to)) return false;

                    x += sx;
                    across++;
                }
                else
                {
                    if (!Steps(map, here, new Cell(x, y + sy), to)) return false;

                    y += sy;
                    along++;
                }

                if (new Cell(x, y) == to) break;
            }

            return true;
        }

        // the squares the same line passes through, both ends included - what a fog or a darkness
        // has to cover to stand in the way. a line through an exact corner goes diagonally
        public static IReadOnlyList<Cell> Between(Cell from, Cell to)
        {
            var cells = new List<Cell> { from };

            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            int nx = Math.Abs(dx);
            int ny = Math.Abs(dy);
            int sx = Math.Sign(dx);
            int sy = Math.Sign(dy);
            int x = from.X;
            int y = from.Y;
            int across = 0;
            int along = 0;

            while (across < nx || along < ny)
            {
                long decision = (long)(1 + 2 * across) * ny - (long)(1 + 2 * along) * nx;

                if (decision == 0)
                {
                    x += sx;
                    y += sy;
                    across++;
                    along++;
                }
                else if (decision < 0)
                {
                    x += sx;
                    across++;
                }
                else
                {
                    y += sy;
                    along++;
                }

                cells.Add(new Cell(x, y));
            }

            return cells;
        }

        static bool Steps(MapLayout map, Cell from, Cell to, Cell target) =>
            map.CanSee(from, to) && (to == target || map.IsTransparent(to));
    }
}
