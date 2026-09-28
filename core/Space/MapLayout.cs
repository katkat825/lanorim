using System.Collections.Generic;
using System.Linq;

namespace Core.Space
{
    // immutable - a map is content, an open door is runtime state; With returns a new map, so a save carries only changes
    public sealed class MapLayout
    {
        public Extent Extent { get; }

        public int Columns => Extent.Columns;

        public int Rows => Extent.Rows;

        public Cell Start { get; }

        readonly Dictionary<int, Cell> _spawns;

        public IReadOnlyDictionary<int, Cell> Spawns => _spawns;

        public const int HeroSlot = 0;

        public Cell? SpawnAt(int slot) =>
            slot == HeroSlot ? Start
            : _spawns.TryGetValue(slot, out Cell cell) ? cell
            : (Cell?)null;

        readonly Tile[] _tiles;

        // vertical is (Columns+1) x Rows - one more column of lines than squares, for the west/east boundaries
        readonly Edge[] _vertical;

        readonly Edge[] _horizontal;

        public MapLayout(int columns, int rows, Tile[] tiles, Cell start,
                         Edge[] vertical = null, Edge[] horizontal = null,
                         IReadOnlyDictionary<int, Cell> spawns = null)
        {
            Extent = new Extent(columns, rows);
            Start = start;

            _spawns = spawns == null
                ? new Dictionary<int, Cell>()
                : spawns.Where(s => s.Key != HeroSlot).ToDictionary(s => s.Key, s => s.Value);

            _tiles = Sized(tiles, Extent.Count);
            _vertical = Sized(vertical, Extent.VerticalCount);
            _horizontal = Sized(horizontal, Extent.HorizontalCount);
        }

        static T[] Sized<T>(T[] layer, int wanted) =>
            layer != null && layer.Length == wanted ? layer : new T[wanted];

        public int Count => Extent.Count;

        public bool Contains(Cell cell) => Extent.Contains(cell);

        // off the map is Void, not an exception - so Route and Sight can step outside without a bounds check
        public Tile At(Cell cell) => Extent.At(_tiles, cell);

        public bool IsPassable(Cell cell) => At(cell).IsPassable();

        public bool IsTransparent(Cell cell) => At(cell).IsTransparent();


        public bool Contains(Border border) => Extent.Contains(border);

        public Edge At(Border border)
        {
            if (!Contains(border)) return Edge.None;

            return (border.Vertical ? _vertical : _horizontal)[Extent.Index(border)];
        }

        public Edge Between(Cell a, Cell b) =>
            Border.Between(a, b, out Border border) ? At(border) : Edge.Wall;

        public bool CanCross(Cell a, Cell b) =>
            Border.Between(a, b, out Border border) && At(border).IsOpen() && IsPassable(b);

        public bool CanSee(Cell a, Cell b) =>
            Border.Between(a, b, out Border border) && At(border).IsTransparent();

        public IEnumerable<Border> Borders
        {
            get
            {
                for (int y = 0; y < Rows; y++)
                    for (int x = 0; x <= Columns; x++)
                        yield return new Border(new Cell(x, y), true);

                for (int y = 0; y <= Rows; y++)
                    for (int x = 0; x < Columns; x++)
                        yield return new Border(new Cell(x, y), false);
            }
        }


        public MapLayout With(Cell cell, Tile tile)
        {
            if (!Contains(cell) || At(cell) == tile) return this;

            var changed = (Tile[])_tiles.Clone();
            changed[Extent.Index(cell)] = tile;

            return new MapLayout(Columns, Rows, changed, Start, _vertical, _horizontal, _spawns);
        }

        public MapLayout With(Border border, Edge edge)
        {
            if (!Contains(border) || At(border) == edge) return this;

            var vertical = _vertical;
            var horizontal = _horizontal;

            if (border.Vertical)
            {
                vertical = (Edge[])_vertical.Clone();
                vertical[Extent.Index(border)] = edge;
            }
            else
            {
                horizontal = (Edge[])_horizontal.Clone();
                horizontal[Extent.Index(border)] = edge;
            }

            return new MapLayout(Columns, Rows, _tiles, Start, vertical, horizontal, _spawns);
        }

        public IEnumerable<Cell> Cells => Extent.Cells;

        // debug only, never localized - keep it off the screen
        public override string ToString()
        {
            int walls = 0;
            int doors = 0;

            foreach (Border border in Borders)
            {
                if (At(border) == Edge.Wall) walls++;
                else if (At(border) == Edge.Door) doors++;
            }

            return $"{Columns} x {Rows} map, {walls} walls and {doors} doors on the lines, " +
                   $"hero starts on {Start}";
        }
    }
}
