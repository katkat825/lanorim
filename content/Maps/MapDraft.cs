using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Content.Schema;
using Core.Space;

namespace Content.Maps
{
    // a prop on a square: a barrel, a brazier, a bookcase. the editor paints these; the board
    // layer turns each into a Quaternius model. purely decorative to the rules - anything that
    // blocks movement is a tile or a wall, not a prop, so the rules never have to ask a model.
    public sealed class Prop
    {
        public Prop(string id, Cell cell, int turn = 0)
        {
            Id = id;
            Cell = cell;
            Turn = ((turn % 4) + 4) % 4;
        }

        public string Id { get; }

        public Cell Cell { get; }

        // quarter turns, 0 to 3. the only transform the editor offers, because a free rotation is
        // a thing you can only judge by looking - and the person doing the looking is in Godot
        public int Turn { get; }

        public override string ToString() => $"{Id} at {Cell}" + (Turn > 0 ? $" turned {Turn}" : "");
    }

    // the map builder's model. mutable, undoable, and it never draws anything: the Godot editor
    // is a palette and a grid over this, and every rule about what may be painted lives here.
    //
    // v1_build_checklist.md section 9: paint tiles, place props, set spawns, no scripting.
    public sealed class MapDraft
    {
        readonly Tile[] _tiles;
        readonly Dictionary<Border, Edge> _edges = new Dictionary<Border, Edge>();
        readonly Dictionary<int, Cell> _spawns = new Dictionary<int, Cell>();
        readonly List<Prop> _props = new List<Prop>();

        readonly Stack<Action> _undo = new Stack<Action>();
        readonly Stack<Action> _redo = new Stack<Action>();

        public MapDraft(int columns, int rows)
        {
            Columns = Math.Clamp(columns, 1, MaxSide);
            Rows = Math.Clamp(rows, 1, MaxSide);

            _tiles = new Tile[Columns * Rows];

            // a fresh map is solid rock, so the author paints the room rather than carving it
            for (int i = 0; i < _tiles.Length; i++) _tiles[i] = Tile.Void;

            Start = new Cell(0, 0);
        }

        // a map the tray and the camera can still frame. bigger is a campaign's problem and the
        // editor refuses it rather than letting somebody paint for an hour and then find out
        public const int MaxSide = 64;

        public int Columns { get; }

        public int Rows { get; }

        public Cell Start { get; private set; }

        public IReadOnlyList<Prop> Props => _props;

        public IReadOnlyDictionary<int, Cell> Spawns => _spawns;

        public bool Contains(Cell cell) =>
            cell.X >= 0 && cell.X < Columns && cell.Y >= 0 && cell.Y < Rows;

        public Tile At(Cell cell) => Contains(cell) ? _tiles[cell.Y * Columns + cell.X] : Tile.Void;

        public Edge At(Border border) => _edges.TryGetValue(border, out Edge edge) ? edge : Edge.None;


        // --- painting ----------------------------------------------------------------------------

        public bool Paint(Cell cell, Tile tile)
        {
            if (!Contains(cell)) return false;

            Tile was = At(cell);

            if (was == tile) return false;

            Do(() => Set(cell, tile), () => Set(cell, was));

            return true;
        }

        // the editor's drag: every square between two corners at once, one undo step for the lot
        public int Paint(Cell from, Cell to, Tile tile)
        {
            var changed = new List<(Cell Cell, Tile Was)>();

            for (int y = Math.Min(from.Y, to.Y); y <= Math.Max(from.Y, to.Y); y++)
                for (int x = Math.Min(from.X, to.X); x <= Math.Max(from.X, to.X); x++)
                {
                    var cell = new Cell(x, y);

                    if (!Contains(cell) || At(cell) == tile) continue;

                    changed.Add((cell, At(cell)));
                }

            if (changed.Count == 0) return 0;

            Do(() =>
               {
                   foreach ((Cell cell, Tile _) in changed) Set(cell, tile);
               },
               () =>
               {
                   foreach ((Cell cell, Tile was) in changed) Set(cell, was);
               });

            return changed.Count;
        }

        void Set(Cell cell, Tile tile) => _tiles[cell.Y * Columns + cell.X] = tile;

        public bool Wall(Border border, Edge edge)
        {
            if (!OnTheMap(border)) return false;

            Edge was = At(border);

            if (was == edge) return false;

            Do(() => SetEdge(border, edge), () => SetEdge(border, was));

            return true;
        }

        // the editor clicks between two squares rather than naming a border
        public bool Wall(Cell a, Cell b, Edge edge) =>
            Border.Between(a, b, out Border border) && Wall(border, edge);

        void SetEdge(Border border, Edge edge)
        {
            if (edge == Edge.None) _edges.Remove(border);
            else _edges[border] = edge;
        }

        bool OnTheMap(Border border) => border.Vertical
            ? border.Cell.X >= 0 && border.Cell.X <= Columns &&
              border.Cell.Y >= 0 && border.Cell.Y < Rows
            : border.Cell.X >= 0 && border.Cell.X < Columns &&
              border.Cell.Y >= 0 && border.Cell.Y <= Rows;

        // the outside edge, in one stroke. the first thing anyone does to a new map
        public int Enclose()
        {
            int put = 0;

            for (int y = 0; y < Rows; y++)
            {
                if (Wall(new Border(new Cell(0, y), true), Edge.Wall)) put++;
                if (Wall(new Border(new Cell(Columns, y), true), Edge.Wall)) put++;
            }

            for (int x = 0; x < Columns; x++)
            {
                if (Wall(new Border(new Cell(x, 0), false), Edge.Wall)) put++;
                if (Wall(new Border(new Cell(x, Rows), false), Edge.Wall)) put++;
            }

            return put;
        }


        // --- markers -----------------------------------------------------------------------------

        public bool PlaceStart(Cell cell)
        {
            if (!Contains(cell) || !At(cell).IsPassable()) return false;

            Cell was = Start;

            Do(() => Start = cell, () => Start = was);

            return true;
        }

        public const int FirstSpawn = 1;
        public const int LastSpawn = 9;

        public bool PlaceSpawn(int slot, Cell cell)
        {
            if (slot < FirstSpawn || slot > LastSpawn) return false;

            if (!Contains(cell) || !At(cell).IsPassable()) return false;

            // two monsters on one square would both be standing in the same place at round one
            if (_spawns.Any(s => s.Key != slot && s.Value == cell)) return false;

            bool had = _spawns.TryGetValue(slot, out Cell was);

            Do(() => _spawns[slot] = cell,
               () =>
               {
                   if (had) _spawns[slot] = was;
                   else _spawns.Remove(slot);
               });

            return true;
        }

        public bool ClearSpawn(int slot)
        {
            if (!_spawns.TryGetValue(slot, out Cell was)) return false;

            Do(() => _spawns.Remove(slot), () => _spawns[slot] = was);

            return true;
        }

        public bool PlaceProp(string id, Cell cell, int turn = 0)
        {
            if (!Json.IsId(id) || !Contains(cell) || !At(cell).IsPassable()) return false;

            var prop = new Prop(id, cell, turn);

            Do(() => _props.Add(prop), () => _props.Remove(prop));

            return true;
        }

        public int ClearProps(Cell cell)
        {
            List<Prop> here = _props.Where(p => p.Cell == cell).ToList();

            if (here.Count == 0) return 0;

            Do(() => _props.RemoveAll(p => p.Cell == cell),
               () => _props.AddRange(here));

            return here.Count;
        }


        // --- undo ---------------------------------------------------------------------------------

        void Do(Action forward, Action back)
        {
            forward();

            _undo.Push(back);
            _redo.Clear();

            // an editor that remembers forever is an editor that eats a map's worth of memory per
            // drag; a hundred steps is more than anybody reaches for
            if (_undo.Count > UndoDepth)
            {
                var kept = _undo.ToArray().Take(UndoDepth).Reverse().ToArray();

                _undo.Clear();

                foreach (Action step in kept) _undo.Push(step);
            }
        }

        public const int UndoDepth = 100;

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        public bool Undo()
        {
            if (!CanUndo) return false;

            // the redo is built by replaying: capturing both directions per step would double
            // every closure above, and a map is cheap to snapshot
            string before = Save();

            _undo.Pop()();

            string after = Save();

            _redo.Push(() => Restore(before));

            return before != after;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;

            string before = Save();

            _redo.Pop()();

            _undo.Push(() => Restore(before));

            return true;
        }

        void Restore(string saved)
        {
            if (!TryRead(saved, out MapDraft draft, out _)) return;

            Array.Copy(draft._tiles, _tiles, Math.Min(draft._tiles.Length, _tiles.Length));

            _edges.Clear();
            foreach (KeyValuePair<Border, Edge> edge in draft._edges) _edges[edge.Key] = edge.Value;

            _spawns.Clear();
            foreach (KeyValuePair<int, Cell> spawn in draft._spawns) _spawns[spawn.Key] = spawn.Value;

            _props.Clear();
            _props.AddRange(draft._props);

            Start = draft.Start;
        }


        // --- what it makes ------------------------------------------------------------------------

        public MapLayout Layout()
        {
            var vertical = new Edge[(Columns + 1) * Rows];
            var horizontal = new Edge[Columns * (Rows + 1)];

            foreach (KeyValuePair<Border, Edge> edge in _edges)
            {
                Border border = edge.Key;

                if (border.Vertical)
                {
                    int i = border.Cell.Y * (Columns + 1) + border.Cell.X;

                    if (i >= 0 && i < vertical.Length) vertical[i] = edge.Value;
                }
                else
                {
                    int i = border.Cell.Y * Columns + border.Cell.X;

                    if (i >= 0 && i < horizontal.Length) horizontal[i] = edge.Value;
                }
            }

            return new MapLayout(Columns, Rows, (Tile[])_tiles.Clone(), Start,
                                 vertical, horizontal, _spawns);
        }

        // what a campaign ships: the map as the same text MapReader reads, plus the props beside
        // it. one file, and the map part stays hand-editable, which is what makes a Workshop
        // author's text editor a usable tool
        public string Save()
        {
            var json = new StringBuilder();

            json.Append("{\n  \"format\": 1,\n  \"columns\": ").Append(Columns)
                .Append(",\n  \"rows\": ").Append(Rows)
                .Append(",\n  \"map\": ")
                .Append(JsonSerializer.Serialize(MapWriter.Write(Layout(), _spawns)))
                .Append(",\n  \"props\": [");

            for (int i = 0; i < _props.Count; i++)
            {
                Prop prop = _props[i];

                json.Append(i == 0 ? "\n    " : ",\n    ")
                    .Append("{ \"id\": ").Append(JsonSerializer.Serialize(prop.Id))
                    .Append(", \"x\": ").Append(prop.Cell.X)
                    .Append(", \"y\": ").Append(prop.Cell.Y)
                    .Append(", \"turn\": ").Append(prop.Turn)
                    .Append(" }");
            }

            return json.Append(_props.Count == 0 ? "]" : "\n  ]").Append("\n}\n").ToString();
        }

        public static bool TryRead(string text, out MapDraft draft, out string problem)
        {
            draft = null;
            problem = null;

            if (!Json.TryParse(text, out JsonDocument document, out problem)) return false;

            using (document)
            {
                JsonElement root = document.RootElement;

                if (!MapReader.TryRead(root.Text("map"), out MapLayout map, out problem))
                    return false;

                draft = new MapDraft(map.Columns, map.Rows);

                foreach (Cell cell in map.Cells) draft.Set(cell, map.At(cell));

                foreach (Border border in map.Borders)
                    draft.SetEdge(border, map.At(border));

                draft.Start = map.Start;

                foreach (KeyValuePair<int, Cell> spawn in map.Spawns)
                    draft._spawns[spawn.Key] = spawn.Value;

                foreach (JsonElement raw in root.Items("props"))
                {
                    string id = raw.Text("id");

                    if (!Json.IsId(id))
                    {
                        problem = $"'{id}' is not a prop id";
                        return false;
                    }

                    draft._props.Add(new Prop(id, new Cell(raw.Number("x"), raw.Number("y")),
                                              raw.Number("turn")));
                }

                // reading is not an edit, so nothing goes on the undo stack
                draft._undo.Clear();
            }

            return true;
        }

        // everything that would make the map unplayable, in the words the editor shows. checked on
        // save, so an author finds out in the editor rather than the player finding out mid-fight.
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();

            if (!At(Start).IsPassable())
                problems.Add("the hero starts on solid rock - put the start somewhere you can stand");

            MapLayout map = Layout();

            foreach (KeyValuePair<int, Cell> spawn in _spawns)
            {
                if (!At(spawn.Value).IsPassable())
                {
                    problems.Add($"spawn {spawn.Key} is on solid rock");
                    continue;
                }

                if (!Route.Exists(map, Start, spawn.Value, _ => false))
                    problems.Add($"spawn {spawn.Key} cannot be walked to from the start - " +
                                 "a wall or a gap of rock is in the way");
            }

            foreach (Prop prop in _props)
                if (!At(prop.Cell).IsPassable())
                    problems.Add($"the {prop.Id} at {prop.Cell} is inside a wall");

            int floor = Layout().Cells.Count(c => map.At(c).IsPassable());

            if (floor == 0) problems.Add("nothing has been painted yet");

            return problems;
        }

        public bool Sound => Problems().Count == 0;

        public override string ToString() =>
            $"{Columns} x {Rows} draft, {_spawns.Count} spawns, {_props.Count} props" +
            (Sound ? "" : $", {Problems().Count} problems");
    }
}
