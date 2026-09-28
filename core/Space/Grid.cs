using System.Collections.Generic;

namespace Core.Space
{
    // generic in its occupant - Core.Space can't name an Actor; game/ and the sim each pick their own
    public sealed class Grid<TOccupant> where TOccupant : class
    {
        readonly Extent _extent;

        public int Columns => _extent.Columns;

        public int Rows => _extent.Rows;

        // occupancy both ways, kept in step by a single write path
        // reference identity, not Equals - two equal-looking pieces are still two pieces
        readonly Dictionary<Cell, TOccupant> _byCell = new Dictionary<Cell, TOccupant>();

        readonly Dictionary<TOccupant, Cell> _byOccupant =
            new Dictionary<TOccupant, Cell>(ReferenceEqualityComparer.Instance);

        public Grid(int columns, int rows) => _extent = new Extent(columns, rows);

        public int Count => _extent.Count;

        public IEnumerable<Cell> Cells => _extent.Cells;

        public bool Contains(Cell cell) => _extent.Contains(cell);

        public TOccupant At(Cell cell) =>
            _byCell.TryGetValue(cell, out TOccupant occupant) ? occupant : null;

        public bool IsOccupied(Cell cell) => _byCell.ContainsKey(cell);

        public Cell? CellOf(TOccupant occupant) =>
            occupant != null && _byOccupant.TryGetValue(occupant, out Cell cell) ? cell : null;

        public bool Place(TOccupant occupant, Cell cell)
        {
            if (occupant == null || !Contains(cell)) return false;

            if (_byCell.TryGetValue(cell, out TOccupant sitting))
                return ReferenceEquals(sitting, occupant);

            if (_byOccupant.TryGetValue(occupant, out Cell was)) _byCell.Remove(was);

            _byCell[cell] = occupant;
            _byOccupant[occupant] = cell;
            return true;
        }

        public bool Move(TOccupant occupant, Cell cell) =>
            occupant != null && _byOccupant.ContainsKey(occupant) && Place(occupant, cell);

        public bool Remove(TOccupant occupant)
        {
            if (occupant == null || !_byOccupant.TryGetValue(occupant, out Cell cell)) return false;

            _byOccupant.Remove(occupant);
            _byCell.Remove(cell);
            return true;
        }

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            $"{Columns} x {Rows} grid, {_byOccupant.Count} of {Count} squares occupied";
    }
}
