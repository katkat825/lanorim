using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Maps;
using Content.Screens;
using Core.Space;
using Game.Builder;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // THE MAP BUILDER, DRIVEN AS AN AUTHOR WOULD (checks/check-mapbuilder.ps1; cc_task_f Part 2, cc_task_l):
    //
    //   godot --headless --path game res://launch.tscn -- --map-probe --auto
    //
    // From the book's own Map builder page, through the builder's own buttons, mouse and keys:
    //   1. a map in My maps from each template, saved, and the builder left;
    //   2. a dungeon room: rough ground dragged, the floor filled with grass, a fence run, a barrel, a two-by-two
    //      building, a Shift-drag scatter of pines, the start, two spawns and an undo, saved;
    //   3. reopened from the page's My maps list and found identical; copied to a campaign (twice: the second asks, and
    //      Rename makes it _2) and that campaign loads it;
    //   4. played: Play this map, a level 3 fighter against a goblin a spawn, fought to the end (--auto);
    //   5. and back in the builder with the map as it was.
    // A library and a campaign of its own in user://map_probe_<pid>, never the player's, gone when it's done.
    // Prints "mapprobe passed" or "mapprobe FAILED". THE STAGES ARE STATIC: each scene change makes a new probe
    public partial class MapBuilderProbe : Node
    {
        public const string MapId = "probe_crypt";

        public MapBuilder Builder { get; set; }

        public Launch Launch { get; set; }

        enum Stage { Templates, Editing, Reopening, Fighting, Done }

        static Stage _stage = Stage.Templates;
        static int _template;
        static string _root;
        static string _saved;
        static readonly ProbeLog Said = new("mapprobe");

        static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();

        const double GiveUpAfter = 540;

        int _frame;
        int _step;

        // this probe's steps, settled when it first runs: a step may move the stage on for the next scene
        Action[] _steps;

        static string Campaign => Path.Combine(_root, "probe_campaign");

        static MapPlace CampaignPlace => MapPlace.OfCampaign(Campaign, null);

        // the first thing: a library and a campaign of the probe's own, and the book's Map builder page
        public static void AtLaunch(Launch launch)
        {
            if (_root == null)
            {
                _root = ProjectSettings.GlobalizePath($"user://map_probe_{System.Environment.ProcessId}");
                Directory.CreateDirectory(Campaign);
                File.WriteAllText(Path.Combine(Campaign, "pack.json"),
                    $"{{ \"id\": \"probe_campaign\", \"kind\": \"campaign\", \"format\": 1, \"engine\": \"0.1\", " +
                    $"\"chapters\": [ {{ \"id\": \"one\", \"maps\": [ \"{MapId}\" ] }} ], \"start\": \"one\" }}");
                GameState.SaveProbesApart();
            }

            MapBuilderPage.LibraryFolder = Path.Combine(_root, "maps");
            launch.AddChild(new MapBuilderProbe { Name = "MapBuilderProbe", Launch = launch });
        }

        // a step every few frames, so the board is laid and the camera has settled between them
        public override void _Process(double delta)
        {
            // a probe that stops getting anywhere fails rather than hanging the check
            if (Clock.Elapsed.TotalSeconds > GiveUpAfter && _stage != Stage.Done)
            {
                Expect(false, $"finished within {GiveUpAfter} seconds (stuck at {_stage}, step {_step})");
                _stage = Stage.Done;
                Finish();
                return;
            }

            if (++_frame % 20 != 0) return;

            _steps ??= Launch != null ? AtBook() : InBuilder();

            if (_step < _steps.Length) _steps[_step++]();
        }

        // --- at the book --------------------------------------------------------------------------------------------

        Action[] AtBook() => _stage switch
        {
            Stage.Templates when _template < MapTemplates.Ids.Count => new Action[] { OpenPage, () => NewMap(MapTemplates.Ids[_template], $"probe_{MapTemplates.Ids[_template]}") },
            Stage.Templates or Stage.Editing => new Action[] { OpenPage, () => { _stage = Stage.Editing; NewMap("dungeon_room", MapId); } },
            Stage.Reopening => new Action[] { OpenPage, OpenFromMyMaps },
            _ => new Action[] { Finish },
        };

        void OpenPage() => Launch.PageCalled("builder")?.Open(Launch);

        // the page's New map: a name, the template, My maps, and the big button
        void NewMap(string template, string id)
        {
            Find<LineEdit>("MapId").Text = id;
            Choose(Find<OptionButton>("Template"), MapTemplates.Ids.ToList().IndexOf(template));
            Choose(Find<OptionButton>("Lives"), 0);
            Find<Button>("MakeMap")?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        void OpenFromMyMaps()
        {
            Button mine = Nodes.Under<HBoxContainer>(Launch).FirstOrDefault(r => r.Name == "MyMap_" + MapId)?
                               .GetChildren().OfType<Button>().FirstOrDefault(b => b.Text == MapId);

            Expect(mine != null, $"My maps on the book's page lists {MapId}");
            mine?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        T Find<T>(string name) where T : Node => Nodes.Under<T>(Launch).FirstOrDefault(n => n.Name == name);

        static void Choose(OptionButton list, int index)
        {
            if (list == null) return;
            list.Selected = index;
            list.EmitSignal(OptionButton.SignalName.ItemSelected, index);
        }

        // --- in the builder -----------------------------------------------------------------------------------------

        MapDraft Draft => Builder.View.Draft;

        Action[] InBuilder() => _stage switch
        {
            Stage.Templates => new Action[] { CheckTemplate, Save, Leave },
            Stage.Editing => new Action[] { Paint, Fill, Fence, Barrel, Building, Scatter, Start, Spawns, Undo, Save, Remember, Leave },
            Stage.Reopening => new Action[] { CheckReopened, CopyToCampaign, CopyAgain, CheckCampaign, PlayIt, Fight },
            Stage.Fighting => new Action[] { CheckBack, Leave },
            _ => new Action[] { Finish },
        };

        void CheckTemplate()
        {
            string template = MapTemplates.Ids[_template];

            Expect(Builder.View.Place.IsLibrary && Builder.View.Id == $"probe_{template}", $"{template}: a new map in My maps");
            Expect(Builder.Panels.Title.StartsWith(Ui.Say(MapPlace.LibraryKey)), $"{template}: the title says where it lives ({Builder.Panels.Title})");
            Expect(Draft.Save() == MapTemplates.Read(template).Save(), $"{template}: the map is the template, at its own size");

            _template++;
        }

        void Paint()
        {
            Press(MapEditor.ToolKey(MapTool.Paint));
            Press(MapBuilderView.RuleKey(Tile.Rough));
            Drag(Centre(new Cell(4, 4)), Centre(new Cell(5, 5)));
            Expect(Draft.At(new Cell(4, 4)) == Tile.Rough && Draft.At(new Cell(5, 5)) == Tile.Rough, "the drag painted the rough");
        }

        void Fill()
        {
            Press(MapEditor.ToolKey(MapTool.Fill));
            Press(MapBuilderView.GroundKey(Ground.Grass));
            Click(Centre(new Cell(1, 1)));
            Expect(Draft.GroundAt(new Cell(10, 8)) == Ground.Grass && Draft.At(new Cell(10, 8)) == Tile.Floor, "the fill laid grass up to the walls");
            Expect(Draft.GroundAt(new Cell(4, 4)) == Ground.Stone, "and not over the rough, which is another ground's square");
        }

        void Fence()
        {
            Press(MapEditor.ToolKey(MapTool.Wall));
            Press(MapBuilderView.LineKey(Edge.Wall));
            Press(MapBuilderView.StyleKey(WallStyle.Fence));
            Drag(Line(new Border(new Cell(7, 2), true)), Line(new Border(new Cell(7, 4), true)));
            Expect(Enumerable.Range(2, 3).All(y => Draft.StyleAt(new Border(new Cell(7, y), true)) == WallStyle.Fence), "the drag laid a run of fence");
            Expect(Draft.Layout().At(new Border(new Cell(7, 3), true)) == Edge.Bars, "which the rules see through");
        }

        void Barrel()
        {
            PickProp("barrel");
            Click(Centre(new Cell(2, 6)));
            Expect(Draft.Props.Any(p => p.Id == "barrel" && p.Cell == new Cell(2, 6)), "the barrel stands where it was clicked");
        }

        void Building()
        {
            PickProp("cottage");

            Click(Centre(new Cell(5, 6)));
            Expect(Draft.Props.Any(p => p.Id == "cottage" && p.Cell == new Cell(5, 6)), "the cottage stands where it was clicked");
            Expect(Draft.IsBlocked(new Cell(6, 7)), "over all four of its squares");
        }

        // Shift held along a row: a pine every other square
        void Scatter()
        {
            PickProp("pine_1");

            Vector2 from = Centre(new Cell(4, 8)), to = Centre(new Cell(11, 8));
            Mouse(from, true, shift: true);

            for (int i = 1; i <= 9; i++)
                GetViewport().PushInput(new InputEventMouseMotion
                {
                    Position = from.Lerp(to, i / 9f), GlobalPosition = from.Lerp(to, i / 9f),
                    ButtonMask = MouseButtonMask.Left, ShiftPressed = true,
                }, true);

            Mouse(to, false, shift: true);

            int pines = Draft.Props.Count(p => p.Id == "pine_1");
            Expect(pines >= 3, $"the Shift-drag scattered pines along the row ({pines})");
            Expect(Draft.Props.Where(p => p.Id == "pine_1").Select(p => p.Turn).Distinct().Count() > 1, "each at a turn of its own");
        }

        void Start()
        {
            Press(MapEditor.ToolKey(MapTool.Start));
            Click(Centre(new Cell(3, 3)));
            Expect(Draft.Start == new Cell(3, 3), $"the start moved (it is on {Draft.Start})");
        }

        void Spawns()
        {
            Press(MapEditor.ToolKey(MapTool.Spawn));
            Press(MapBuilderView.SpawnKey, 1);
            Click(Centre(new Cell(9, 5)));
            Press(MapBuilderView.SpawnKey, 2);
            Click(Centre(new Cell(10, 6)));
            Expect(Draft.Spawns.TryGetValue(2, out Cell at) && at == new Cell(10, 6), "spawn 2 is where it was clicked");
        }

        void Undo()
        {
            Key(Godot.Key.Z, ctrl: true);
            Expect(!Draft.Spawns.ContainsKey(2), "Ctrl+Z took spawn 2 back");
            Expect(Draft.Spawns.ContainsKey(1) && Draft.Start == new Cell(3, 3), "and only spawn 2");
        }

        void Save()
        {
            Key(Godot.Key.S, ctrl: true);
            Expect(!Builder.Editor.Unsaved, $"Ctrl+S saved {Builder.View.Id}");
            Expect(MapFiles.Has(MapBuilderPage.Library, Builder.View.Id), $"into My maps");
        }

        void Remember()
        {
            _saved = Draft.Save();
            Expect(Draft.Sound, "the map has no problems: " + string.Join("; ", Draft.Problems()));
            _stage = Stage.Reopening;
        }

        void Leave() => Press(MapBuilderView.LeaveKey);

        void CheckReopened()
        {
            Expect(Builder.View.Id == MapId && Builder.View.Place.IsLibrary, "reopened from My maps");
            Expect(Draft.Save() == _saved, "the reopened map is the one saved");
        }

        void CopyToCampaign()
        {
            Press(MapBuilderView.CopyToKey);
            Expect(Builder.Panels.Asking, "Copy to campaign asks where");
            Builder.Panels.Write(copy: true, CampaignPlace, MapId, replace: false);
            Expect(MapFiles.Has(CampaignPlace, MapId), "copied into the campaign");
            Expect(Builder.View.Place.IsLibrary, "and the map open is still My maps'");
        }

        void CopyAgain()
        {
            Builder.Panels.Write(copy: true, CampaignPlace, MapId, replace: false);
            Expect(Builder.Panels.Asking, "a second copy asks: the name is taken");
            Press(MapBuilderView.RenameKey);
            Expect(MapFiles.Has(CampaignPlace, MapId + "_2"), "Rename copied it as the next free name");
        }

        void CheckCampaign()
        {
            Package pack = Package.Read(Campaign);

            Expect(pack.Maps.ContainsKey(MapId), "its campaign loads it: " + string.Join("; ", pack.Problems.Select(p => p.What)));
            Expect(pack.PropsOn(MapId).Any(p => p.Id == "cottage"), "with its cottage");
            Expect(pack.LookOn(MapId)?.At(new Cell(1, 1)) == Ground.Grass, "and its grass");
        }

        void PlayIt()
        {
            Press(MapTrial.PlayKey);

            TrialPanel trial = Builder.Panels.Trial;
            Expect(trial != null, "Play this map opens its panel");
            if (trial == null) return;

            Choose(Nodes.Under<OptionButton>(trial).FirstOrDefault(o => o.Name == "TrialClass"), trial.Classes.ToList().IndexOf("fighter"));
            var level = Nodes.Under<SpinBox>(trial).FirstOrDefault(s => s.Name == "TrialLevel");
            if (level != null) level.Value = 3;

            Expect(trial.Trial.Level == 3 && trial.Trial.Class == "fighter", "a level 3 fighter");
            Expect(trial.Trial.Slots.All(s => trial.Trial.At(s) == "goblin"), "against a goblin a spawn");
        }

        void Fight()
        {
            _stage = Stage.Fighting;
            Nodes.Under<Button>(Builder).FirstOrDefault(b => b.Name == "PlayTrial")?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        void CheckBack()
        {
            Expect(GameState.Trying == null && GameState.Run == null, "the fight over, the trial is forgotten");
            Expect(Builder.View.Id == MapId && Builder.View.Place.IsLibrary, "and the builder is back on the map");
            Expect(Draft.Save() == _saved, "as it was");
            Expect(!Directory.Exists(ProjectSettings.GlobalizePath("user://saves/" + MapTrial.CampaignId)),
                   "the trial left no saves among the player's");
            _stage = Stage.Done;
        }

        // --- the end ------------------------------------------------------------------------------------------------

        void Finish()
        {
            SetProcess(false);

            try
            {
                if (_root != null && _root.Contains("map_probe_") && Directory.Exists(_root)) Directory.Delete(_root, true);

                string trial = ProjectSettings.GlobalizePath(GameState.TrialFolder);
                if (Directory.Exists(trial)) Directory.Delete(trial, true);
            }
            catch (IOException) { }

            GameState.ForgetProbeSaves();

            Said.Verdict();

            GetTree().Quit(Said.Passed ? 0 : 1);
        }

        static void Expect(bool held, string what) => Said.Expect(held, what);

        // --- the author's hands -------------------------------------------------------------------------------------

        // the panels' button with these words, pressed
        void Press(string key, params object[] args)
        {
            string words = Ui.Say(key, args);
            Button button = Nodes.Under<Button>(Builder).FirstOrDefault(b => b.Text == words && b.IsVisibleInTree());

            if (button == null)
            {
                Expect(false, $"a button saying '{words}'");
                return;
            }

            button.EmitSignal(BaseButton.SignalName.Pressed);
        }

        // the Props tool, the prop's thumbnail found by its search
        void PickProp(string id)
        {
            Press(MapEditor.ToolKey(MapTool.Prop));

            LineEdit search = Nodes.Under<LineEdit>(Builder).FirstOrDefault(l => l.Name == "Search");
            if (search != null)
            {
                search.Text = Ui.Say(Builder.Editor.Palette.Find(id).NameKey);
                search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            }

            Press(Builder.Editor.Palette.Find(id).NameKey);
            Expect(Builder.Editor.PropId == id, $"the palette chose the {id}");
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

        void Mouse(Vector2 at, bool down, bool shift = false) =>
            GetViewport().PushInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left, Pressed = down, Position = at, GlobalPosition = at, ShiftPressed = shift,
            }, true);

        void Key(Key key, bool ctrl) =>
            GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, CtrlPressed = ctrl });
    }
}
