using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Content.Schema;
using Core.Space;

namespace Content.Maps
{
    public sealed partial class MapDraft
    {
        // --- what it makes ------------------------------------------------------------------------

        public MapLayout Layout()
        {
            var vertical = new Edge[Extent.VerticalCount];
            var horizontal = new Edge[Extent.HorizontalCount];

            foreach (KeyValuePair<Border, Edge> edge in _edges)
            {
                Edge[] layer = edge.Key.Vertical ? vertical : horizontal;
                int i = Extent.Index(edge.Key);

                if (i >= 0 && i < layer.Length) layer[i] = edge.Value;
            }

            return new MapLayout(Columns, Rows, (Tile[])_tiles.Clone(), Start,
                                 vertical, horizontal, _spawns,
                                 _props.Where(Blocks).Select(p => p.Cell));
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
            else if (IsBlocked(Start))
                problems.Add("the hero starts inside a prop that blocks - move the start or the prop");

            MapLayout map = Layout();

            foreach (KeyValuePair<int, Cell> spawn in _spawns)
            {
                if (!At(spawn.Value).IsPassable())
                {
                    problems.Add($"spawn {spawn.Key} is on solid rock");
                    continue;
                }

                if (IsBlocked(spawn.Value))
                {
                    problems.Add($"spawn {spawn.Key} is inside a prop that blocks");
                    continue;
                }

                if (!Route.Exists(map, Start, spawn.Value, _ => false))
                    problems.Add($"spawn {spawn.Key} cannot be walked to from the start - " +
                                 "a wall, a gap of rock or a prop that blocks is in the way");
            }

            foreach (Prop prop in _props)
                if (!At(prop.Cell).IsPassable())
                    problems.Add($"the {prop.Id} at {prop.Cell} is inside a wall");

            int floor = Layout().Cells.Count(c => map.At(c).IsPassable());

            if (floor == 0) problems.Add("nothing has been painted yet");

            return problems;
        }

        public bool Sound => Problems().Count == 0;
    }
}
