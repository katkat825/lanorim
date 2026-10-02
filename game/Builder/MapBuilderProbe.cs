using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Maps;
using Core.Space;
using Godot;

namespace Game.Builder
{
    // THE MAP BUILDER, DRIVEN AS AN AUTHOR WOULD (checks/check-mapbuilder.ps1, cc_task_f Part 2):
    //
    //   godot --headless --path game res://launch.tscn -- --map-probe
    //
    // A throwaway campaign in user://, a new map opened in the builder, and then the builder's own way in: the tool
    // buttons pressed, the mouse clicked and dragged on the board at the squares and lines it means, Ctrl+Z and
    // Ctrl+S - paint, a wall run, a prop, the start and a spawn, one undo, a save. The file is read back and must be
    // the map the builder holds, and its campaign must load it. Prints "mapprobe passed" or "mapprobe FAILED"
    public partial class MapBuilderProbe : Node
    {
        public const string Flag = "--map-probe";

        public const string Campaign = "probe_campaign";

        public const string MapId = "probe_crypt";

        public MapBuilder Builder { get; set; }

        readonly List<string> _failed = new();
        int _frame;
        int _step;

        // a fresh campaign folder for the probe, with a manifest that plays the map
        public static string MakeCampaign()
        {
            string root = ProjectSettings.GlobalizePath($"user://map_probe_{System.Environment.ProcessId}");
            string folder = Path.Combine(root, Campaign);

            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "pack.json"),
                $"{{ \"id\": \"{Campaign}\", \"kind\": \"campaign\", \"format\": 1, \"engine\": \"0.1\", " +
                $"\"chapters\": [ {{ \"id\": \"one\", \"maps\": [ \"{MapId}\" ] }} ], \"start\": \"one\" }}");

            return folder;
        }

        MapDraft Draft => Builder.View.Draft;

        // a step every few frames, so the board is laid and the camera has settled between them
        public override void _Process(double delta)
        {
            if (++_frame % 20 != 0) return;

            Action[] steps = { Paint, Wall, Prop, Start, Spawn, Undo, Save, Check };

            if (_step < steps.Length)
            {
                steps[_step++]();
                return;
            }

            SetProcess(false);
            Finish();
        }

        void Paint()
        {
            Press(MapEditor.ToolKey(MapTool.Paint));
            Press(Content.Screens.MapBuilderView.GroundKey(Tile.Rough));
            Drag(Centre(new Cell(4, 4)), Centre(new Cell(5, 5)));
            Expect(Draft.At(new Cell(4, 4)) == Tile.Rough && Draft.At(new Cell(5, 5)) == Tile.Rough, "the drag painted the rough");
        }

        void Wall()
        {
            Press(MapEditor.ToolKey(MapTool.Wall));
            Press(Content.Screens.MapBuilderView.LineKey(Edge.Wall));
            Drag(Line(new Border(new Cell(7, 2), true)), Line(new Border(new Cell(7, 4), true)));
            Expect(Enumerable.Range(2, 3).All(y => Draft.At(new Border(new Cell(7, y), true)) == Edge.Wall), "the drag laid a run of three walls");
        }

        void Prop()
        {
            Press(MapEditor.ToolKey(MapTool.Prop));
            Press(Builder.Editor.Palette.Find("barrel").NameKey);
            Click(Centre(new Cell(2, 6)));
            Expect(Draft.Props.Any(p => p.Id == "barrel" && p.Cell == new Cell(2, 6)), "the barrel stands where it was clicked");
        }

        void Start()
        {
            Press(MapEditor.ToolKey(MapTool.Start));
            Click(Centre(new Cell(3, 3)));
            Expect(Draft.Start == new Cell(3, 3), "the start moved");
        }

        void Spawn()
        {
            Press(MapEditor.ToolKey(MapTool.Spawn));
            Press(Content.Screens.MapBuilderView.SpawnKey, 2);
            Click(Centre(new Cell(9, 6)));
            Expect(Draft.Spawns.TryGetValue(2, out Cell at) && at == new Cell(9, 6), "spawn 2 is where it was clicked");
        }

        void Undo()
        {
            Key(Godot.Key.Z, ctrl: true);
            Expect(!Draft.Spawns.ContainsKey(2), "Ctrl+Z took the spawn back");
            Expect(Draft.Start == new Cell(3, 3), "and only the spawn");
        }

        void Save()
        {
            Key(Godot.Key.S, ctrl: true);
            Expect(!Builder.Editor.Unsaved, "Ctrl+S saved");
        }

        void Check()
        {
            string folder = Builder.View.Campaign;

            if (!MapFiles.Read(folder, MapId, out MapDraft back, out string problem))
            {
                Expect(false, "the saved file reads back - " + problem);
                return;
            }

            Expect(back.Save() == Draft.Save(), "the reloaded map is the one saved");

            Package pack = Package.Read(folder);
            Expect(pack.Maps.ContainsKey(MapId), "its campaign loads it: " + string.Join("; ", pack.Problems.Select(p => p.What)));
            Expect(pack.PropsOn(MapId).Any(p => p.Id == "barrel"), "with its barrel");

            GD.Print($"mapprobe {back}");
        }

        void Finish()
        {
            try
            {
                string root = Path.GetDirectoryName(Builder.View.Campaign);
                if (root != null && root.Contains("map_probe_") && Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch (IOException) { }

            if (_failed.Count == 0) GD.Print("mapprobe passed");
            else GD.PrintErr("mapprobe FAILED - " + string.Join("; ", _failed));

            GetTree().Quit(_failed.Count == 0 ? 0 : 1);
        }

        void Expect(bool held, string what)
        {
            GD.Print($"mapprobe {(held ? "ok  " : "NOT ")} {what}");
            if (!held) _failed.Add(what);
        }

        // --- the author's hands -------------------------------------------------------------------------------------

        // the panels' button with these words, pressed
        void Press(string key, params object[] args)
        {
            string words = Game.Screens.Ui.Say(key, args);
            Button button = Nodes.Under<Button>(Builder).FirstOrDefault(b => b.Text == words && b.IsVisibleInTree());

            if (button == null)
            {
                Expect(false, $"a button saying '{words}'");
                return;
            }

            button.EmitSignal(BaseButton.SignalName.Pressed);
        }

        Vector2 Centre(Cell cell) => OnScreen(Builder.Table.Board.Where(cell));

        Vector2 Line(Border line) => OnScreen(Builder.Table.Board.Metrics.Centre(line));

        Vector2 OnScreen(Vector3 local) =>
            GetViewport().GetCamera3D().UnprojectPosition(Builder.Table.Board.ToGlobal(local));

        // in the viewport's own coordinates (true): pushed as the window's, they are scaled from the window to the
        // 1920x1080 canvas, and a headless window is small
        void Click(Vector2 at) => Drag(at, at);

        void Drag(Vector2 from, Vector2 to)
        {
            Mouse(from, true);
            GetViewport().PushInput(new InputEventMouseMotion { Position = to, GlobalPosition = to, ButtonMask = MouseButtonMask.Left }, true);
            Mouse(to, false);
        }

        void Mouse(Vector2 at, bool down) =>
            GetViewport().PushInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left, Pressed = down, Position = at, GlobalPosition = at,
            }, true);

        void Key(Key key, bool ctrl) =>
            GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = true, CtrlPressed = ctrl });
    }
}
