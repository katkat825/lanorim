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
        // --- the file -----------------------------------------------------------------------------

        // FORMAT 2 (cc_task_l Part 2.5) adds a "ground" layer and a "walls" layer, each written only when the map has a
        // look other than the dungeon's. format 1 still reads, as stone floor and stone walls, so every map written
        // before reads unchanged
        public static readonly FormatVersion Format = new FormatVersion(1, 2);

        // THE MAP'S NAME, as its author typed it ("Mill Yard - North"), apart from its id, the file's name (cc_task_m2
        // Part 7, MapName). Format 2 writes it as "name" when there is one; a map without one shows its id
        public string Name { get; set; } = "";

        // what a campaign ships: the map as the same text MapReader reads, plus the props beside
        // it. one file, and the map part stays hand-editable, which is what makes a Workshop
        // author's text editor a usable tool
        public string Save()
        {
            var json = new StringBuilder();

            json.Append("{\n  \"format\": ").Append(Format.Current)
                .Append(Name.Length > 0 ? ",\n  \"name\": " + JsonSerializer.Serialize(Name) : "")
                .Append(",\n  \"columns\": ").Append(Columns)
                .Append(",\n  \"rows\": ").Append(Rows)
                .Append(",\n  \"map\": ")
                .Append(JsonSerializer.Serialize(MapWriter.Write(Layout(asDrawn: true), _spawns)));

            if (_ground.Any(g => g != Ground.Stone))
                json.Append(",\n  \"ground\": ").Append(JsonSerializer.Serialize(MapLayers.WriteGround(Extent, _ground)));

            if (_styles.Count > 0)
                json.Append(",\n  \"walls\": ").Append(JsonSerializer.Serialize(MapLayers.WriteStyles(Extent, StyleAt)));

            // the map text writes '@' and '1'-'9' where the markers stand, which says nothing of the ground being rough:
            // shallow water under the start, or snow under a spawn. those squares are listed
            List<Cell> rough = Markers.Where(c => At(c) == Tile.Rough).Distinct().ToList();

            if (rough.Count > 0)
                json.Append(",\n  \"rough_markers\": [")
                    .Append(string.Join(", ", rough.Select(c => $"[{c.X}, {c.Y}]")))
                    .Append(']');

            json.Append(",\n  \"props\": [");

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

                int format = root.TryGetProperty("format", out JsonElement said) && said.ValueKind == JsonValueKind.Number
                    ? said.GetInt32()
                    : 1;

                if (!Format.CanRead(format))
                {
                    problem = format > Format.Current
                        ? $"this map is format {format} and this build reads up to {Format.Current} - update the game"
                        : $"this map is format {format}, and no build reads a map below format {Format.Oldest}";
                    return false;
                }

                if (!MapReader.TryRead(root.Text("map"), out MapLayout map, out problem))
                    return false;

                draft = new MapDraft(map.Columns, map.Rows);

                foreach (Cell cell in map.Cells) draft.Set(cell, map.At(cell));

                foreach (Border border in map.Borders)
                    draft.SetEdge(border, map.At(border));

                draft.Start = map.Start;
                draft.Name = root.Text("name");

                foreach (KeyValuePair<int, Cell> spawn in map.Spawns)
                    draft._spawns[spawn.Key] = spawn.Value;

                if (!draft.ReadLayers(root, out problem) || !draft.ReadProps(root, out problem))
                {
                    draft = null;
                    return false;
                }

                // reading is not an edit, so nothing goes on the undo stack
                draft._undo.Clear();
            }

            return true;
        }

        // the start and the spawns
        IEnumerable<Cell> Markers => _spawns.Values.Prepend(Start);

        // the look, when the file has one: each square's ground, and each wall's or door's style; and which of the
        // markers stand on rough ground
        bool ReadLayers(JsonElement root, out string problem)
        {
            problem = null;

            if (root.TryGetProperty("rough_markers", out JsonElement rough) && rough.ValueKind == JsonValueKind.Array)
                foreach (JsonElement at in rough.EnumerateArray())
                {
                    var cell = new Cell(at[0].GetInt32(), at[1].GetInt32());

                    if (!Markers.Contains(cell))
                    {
                        problem = $"rough_markers names {cell}, where no start or spawn stands";
                        return false;
                    }

                    Set(cell, Tile.Rough);
                }

            if (root.TryGetProperty("ground", out JsonElement ground) &&
                !MapLayers.ReadGround(ground.GetString(), Extent, _ground, out problem))
                return false;

            if (!root.TryGetProperty("walls", out JsonElement walls)) return true;

            if (!MapLayers.ReadStyles(walls.GetString(), Extent, out Dictionary<Border, WallStyle> styles, out problem))
                return false;

            foreach (KeyValuePair<Border, WallStyle> style in styles)
                if (At(style.Key) != Edge.None) SetEdge(style.Key, At(style.Key), style.Value);

            return true;
        }

        bool ReadProps(JsonElement root, out string problem)
        {
            problem = null;

            foreach (JsonElement raw in root.Items("props"))
            {
                string id = raw.Text("id");

                if (!Json.IsId(id))
                {
                    problem = $"'{id}' is not a prop id";
                    return false;
                }

                _props.Add(new Prop(id, new Cell(raw.Number("x"), raw.Number("y")), raw.Number("turn")));
            }

            return true;
        }
    }
}
