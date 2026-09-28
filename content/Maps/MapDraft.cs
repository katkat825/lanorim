using System;
using System.Collections.Generic;
using Core.Space;

namespace Content.Maps
{
    // the map builder's model. mutable, undoable, and it never draws anything: the Godot editor
    // is a palette and a grid over this, and every rule about what may be painted lives here.
    //
    // v1_build_checklist.md section 9: paint tiles, place props, set spawns, no scripting.
    public sealed partial class MapDraft
    {
        readonly Tile[] _tiles;
        readonly Dictionary<Border, Edge> _edges = new Dictionary<Border, Edge>();
        readonly Dictionary<int, Cell> _spawns = new Dictionary<int, Cell>();
        readonly List<Prop> _props = new List<Prop>();

        readonly Stack<Action> _undo = new Stack<Action>();
        readonly Stack<Action> _redo = new Stack<Action>();

        public MapDraft(int columns, int rows)
        {
            Extent = new Extent(Math.Clamp(columns, 1, MaxSide), Math.Clamp(rows, 1, MaxSide));

            _tiles = new Tile[Extent.Count];

            // a fresh map is solid rock, so the author paints the room rather than carving it
            for (int i = 0; i < _tiles.Length; i++) _tiles[i] = Tile.Void;

            Start = new Cell(0, 0);
        }

        // a map the tray and the camera can still frame. bigger is a campaign's problem and the
        // editor refuses it rather than letting somebody paint for an hour and then find out
        public const int MaxSide = 64;

        public Extent Extent { get; }

        public int Columns => Extent.Columns;

        public int Rows => Extent.Rows;

        public Cell Start { get; private set; }

        public IReadOnlyList<Prop> Props => _props;

        public IReadOnlyDictionary<int, Cell> Spawns => _spawns;

        public bool Contains(Cell cell) => Extent.Contains(cell);

        public Tile At(Cell cell) => Extent.At(_tiles, cell);

        public Edge At(Border border) => _edges.TryGetValue(border, out Edge edge) ? edge : Edge.None;

        public override string ToString() =>
            $"{Columns} x {Rows} draft, {_spawns.Count} spawns, {_props.Count} props" +
            (Sound ? "" : $", {Problems().Count} problems");
    }
}
