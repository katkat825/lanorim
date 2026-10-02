using System;
using System.Collections.Generic;
using System.Linq;
using Core.Space;

namespace Content.Maps
{
    // THE MAP BUILDER'S POINTER (cc_task_f, Part 2): which square or line a point on the board means for the tool in hand,
    // what a click or a drag from there would change (the hover highlight), and doing it. The screen only turns the mouse
    // into a point in squares and draws what this says; every rule is here, where it can be tested
    public sealed partial class MapEditor
    {
        // how near a line the eraser has to be to take a wall or a door instead of the square, in squares
        public const double LineReach = 0.2;

        // the point is in squares from the map's top-left corner: x across, y down
        public MapTarget Aim(double x, double y)
        {
            Border line = Border.Nearest(x, y, out double off);
            var square = new Cell((int)Math.Floor(x), (int)Math.Floor(y));

            if (Tool == MapTool.Wall)
                return Draft.Extent.Contains(line) ? new MapTarget(null, line) : MapTarget.Nothing;

            if (Tool == MapTool.Erase && off <= LineReach && Draft.Extent.Contains(line) && Draft.At(line) != Edge.None)
                return new MapTarget(null, line);

            return Draft.Contains(square) ? new MapTarget(square, null) : MapTarget.Nothing;
        }

        // A RUN OF LINES: from the first, straight along its own way (down a column for an upright line, across a row
        // for a flat one), as far as the second reaches along it. what a wall drag lays
        public static IReadOnlyList<Border> Run(Border from, Border to)
        {
            var run = new List<Border>();

            if (from.Vertical)
                for (int y = Math.Min(from.Cell.Y, to.Cell.Y); y <= Math.Max(from.Cell.Y, to.Cell.Y); y++)
                    run.Add(new Border(new Cell(from.Cell.X, y), true));
            else
                for (int x = Math.Min(from.Cell.X, to.Cell.X); x <= Math.Max(from.Cell.X, to.Cell.X); x++)
                    run.Add(new Border(new Cell(x, from.Cell.Y), false));

            return run;
        }

        static IEnumerable<Cell> Box(Cell from, Cell to)
        {
            for (int y = Math.Min(from.Y, to.Y); y <= Math.Max(from.Y, to.Y); y++)
                for (int x = Math.Min(from.X, to.X); x <= Math.Max(from.X, to.X); x++)
                    yield return new Cell(x, y);
        }

        // WHAT A CLICK (from == to) OR A DRAG WOULD TOUCH: the squares of a painted or erased rectangle, the lines of a
        // wall run, or the one square or line the other tools act on. the hover highlight
        public (IReadOnlyList<Cell> Squares, IReadOnlyList<Border> Lines) Preview(MapTarget from, MapTarget to)
        {
            if (to.IsNothing) return (Array.Empty<Cell>(), Array.Empty<Border>());

            if (from.Line is Border a && to.Line is Border b)
                return (Array.Empty<Cell>(), Tool is MapTool.Wall or MapTool.Erase ? Run(a, b) : new[] { b });

            if (to.Line is Border only) return (Array.Empty<Cell>(), new[] { only });

            Cell end = to.Square.Value;

            if (from.Square is Cell start && Tool is MapTool.Paint or MapTool.Erase)
                return (Box(start, end).Where(Draft.Contains).ToList(), Array.Empty<Border>());

            return (new[] { end }, Array.Empty<Border>());
        }

        // DO IT: a click when the two are the same, a drag when they aren't. how many things changed (0: nothing did)
        public int Apply(MapTarget from, MapTarget to)
        {
            if (to.IsNothing) return 0;

            if (from == to || from.IsNothing)
                return to.Line is Border line ? (Click(line) ? 1 : 0) : Click(to.Square.Value) ? 1 : 0;

            if (from.Line is Border a && to.Line is Border b)
                return Tool == MapTool.Wall ? Draft.Wall(Run(a, b), Line)
                     : Tool == MapTool.Erase ? Draft.Wall(Run(a, b).Where(l => Draft.At(l) != Edge.None), Edge.None)
                     : 0;

            if (from.Square is Cell start && to.Square is Cell end) return Drag(start, end);

            return Apply(to, to);
        }

        // --- unsaved changes ----------------------------------------------------------------------------------------

        string _saved;

        // what the file holds as of the last save or open; a new map counts as unsaved until it is saved once
        public void MarkSaved() => _saved = Draft.Save();

        public bool Unsaved => _saved == null || Draft.Save() != _saved;
    }
}
