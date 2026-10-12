using System;
using System.Collections.Generic;
using Core.Space;

namespace Content.Maps
{
    // A RECTANGLE OF SQUARES, corner to corner: a painted drag, an erased one, the Select tool's selection and what a
    // paste lands on (cc_task_l Part 4.3). The painting and the pointer each wrote their own loop over one before
    public readonly record struct CellBox(Cell Min, Cell Max)
    {
        // the box two corners make, whichever way round they were dragged
        public static CellBox Between(Cell a, Cell b) =>
            new(new Cell(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Cell(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));

        public static CellBox At(Cell anchor, int width, int height) =>
            new(anchor, new Cell(anchor.X + width - 1, anchor.Y + height - 1));

        public int Width => Max.X - Min.X + 1;

        public int Height => Max.Y - Min.Y + 1;

        public bool Contains(Cell cell) => cell.X >= Min.X && cell.X <= Max.X && cell.Y >= Min.Y && cell.Y <= Max.Y;

        public IEnumerable<Cell> Cells
        {
            get
            {
                for (int y = Min.Y; y <= Max.Y; y++)
                    for (int x = Min.X; x <= Max.X; x++)
                        yield return new Cell(x, y);
            }
        }

        // every line that is a side of one of its squares, the outline included
        public IEnumerable<Border> Borders
        {
            get
            {
                for (int y = Min.Y; y <= Max.Y; y++)
                    for (int x = Min.X; x <= Max.X + 1; x++)
                        yield return new Border(new Cell(x, y), true);

                for (int y = Min.Y; y <= Max.Y + 1; y++)
                    for (int x = Min.X; x <= Max.X; x++)
                        yield return new Border(new Cell(x, y), false);
            }
        }

        // a line of the box's outline, rather than one between two of its own squares
        public bool OnOutline(Border border) => border.Vertical
            ? border.Cell.X == Min.X || border.Cell.X == Max.X + 1
            : border.Cell.Y == Min.Y || border.Cell.Y == Max.Y + 1;

        // the squares just outside it, all round: what a cleared box takes its ground from (MapDraft.Clear)
        public IEnumerable<Cell> Ring
        {
            get
            {
                for (int x = Min.X - 1; x <= Max.X + 1; x++)
                {
                    yield return new Cell(x, Min.Y - 1);
                    yield return new Cell(x, Max.Y + 1);
                }

                for (int y = Min.Y; y <= Max.Y; y++)
                {
                    yield return new Cell(Min.X - 1, y);
                    yield return new Cell(Max.X + 1, y);
                }
            }
        }

        public CellBox Moved(int dx, int dy) => new(new Cell(Min.X + dx, Min.Y + dy), new Cell(Max.X + dx, Max.Y + dy));

        // debug only, never localized
        public override string ToString() => $"{Min} to {Max}";
    }
}
