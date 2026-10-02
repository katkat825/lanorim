using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Core.Space;
using Godot;

namespace Game.Builder
{
    // WHAT THE MAP BUILDER DRAWS ON THE BOARD BESIDE THE MAP ITSELF (cc_task_f Part 2): the lines a click would change
    // (the squares are the board's own Flash), each spawn's number and the start's word, standing over their squares.
    // Under the board, so they turn and zoom with it. The map is the table's own Board; nothing here draws a tile
    public partial class BuilderMarks : Node3D
    {
        public Game.Board.Board Board { get; set; }

        Node3D _lines;
        Node3D _labels;

        public override void _Ready()
        {
            AddChild(_lines = new Node3D { Name = "Lines" });
            AddChild(_labels = new Node3D { Name = "Labels" });
        }

        // a thin bar along each line, lifted off the mat
        public void Lines(IEnumerable<Border> lines)
        {
            Clear(_lines);

            if (Board?.Map == null) return;

            BuilderLayout layout = BuilderLayout.Current;
            float cell = Board.Metrics.CellSize;
            var material = new StandardMaterial3D
            {
                AlbedoColor = layout.LineColour,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };

            foreach (Border line in lines)
            {
                float tall = cell * layout.LineHeight;
                var size = line.Vertical
                    ? new Vector3(cell * layout.LineThickness, tall, cell)
                    : new Vector3(cell, tall, cell * layout.LineThickness);

                _lines.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = size, Material = material },
                    Position = Board.Metrics.Centre(line) + new Vector3(0f, tall * 0.5f, 0f),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
        }

        // the spawns' numbers and the start's word, over their squares; facing the camera wherever it has turned
        public void Markers(MapDraft draft, string startWord)
        {
            Clear(_labels);

            if (Board?.Map == null || draft == null) return;

            BuilderLayout layout = BuilderLayout.Current;

            foreach (KeyValuePair<int, Cell> spawn in draft.Spawns.OrderBy(s => s.Key))
                _labels.AddChild(Label($"Spawn{spawn.Key}", spawn.Key.ToString(), spawn.Value, layout.SpawnColour, layout));

            _labels.AddChild(Label("Start", startWord, draft.Start, layout.StartColour, layout));
        }

        Label3D Label(string name, string text, Cell at, Color colour, BuilderLayout layout) => new Label3D
        {
            Name = name,
            Text = text,
            FontSize = layout.MarkFontSize,
            PixelSize = layout.MarkPixel,
            Modulate = colour,
            OutlineSize = layout.MarkFontSize / 6,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Position = Board.Metrics.Centre(at) + new Vector3(0f, Board.Metrics.CellSize * 0.2f + layout.MarkLift, 0f),
        };

        static void Clear(Node node)
        {
            if (node == null) return;

            foreach (Node child in node.GetChildren()) child.QueueFree();
        }
    }
}
