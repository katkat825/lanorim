using System.Collections.Generic;

namespace Core.Space
{
    // HOW BIG A BOARD IS: its bounds, and where a square or a border sits in a row-major layer. the
    // pieces on a board (Grid), a map (MapLayout) and a map being drawn (MapDraft) each kept their
    // own copy of this (cc_task_dedupe-methods.md #7)
    public readonly struct Extent
    {
        public Extent(int columns, int rows)
        {
            Columns = columns < 0 ? 0 : columns;
            Rows = rows < 0 ? 0 : rows;
        }

        public int Columns { get; }

        public int Rows { get; }

        public int Count => Columns * Rows;

        public bool Contains(Cell cell) =>
            cell.X >= 0 && cell.X < Columns && cell.Y >= 0 && cell.Y < Rows;

        // a border is on the board when it is a side of one of its squares: each square's west and
        // north side, and the far east and south lines
        public bool Contains(Border border) => border.Vertical
            ? border.Cell.X >= 0 && border.Cell.X <= Columns &&
              border.Cell.Y >= 0 && border.Cell.Y < Rows
            : border.Cell.X >= 0 && border.Cell.X < Columns &&
              border.Cell.Y >= 0 && border.Cell.Y <= Rows;

        public int Index(Cell cell) => cell.Y * Columns + cell.X;

        // the vertical lines are one more column than the squares, the horizontal ones one more row
        public int Index(Border border) => border.Vertical
            ? border.Cell.Y * (Columns + 1) + border.Cell.X
            : border.Cell.Y * Columns + border.Cell.X;

        public int VerticalCount => (Columns + 1) * Rows;

        public int HorizontalCount => Columns * (Rows + 1);

        // what a layer holds for a square, or its default off the board (a tile's is Void)
        public T At<T>(T[] layer, Cell cell) => Contains(cell) ? layer[Index(cell)] : default;

        public IEnumerable<Cell> Cells
        {
            get
            {
                for (int y = 0; y < Rows; y++)
                    for (int x = 0; x < Columns; x++)
                        yield return new Cell(x, y);
            }
        }
    }
}
