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
    // THE MAP BUILDER, DRIVEN AS AN AUTHOR WOULD (checks/check-mapbuilder.ps1; cc_task_f Part 2, cc_task_l, cc_task_m2):
    //
    //   godot --headless --path game res://launch.tscn -- --map-probe --auto
    //
    // From the Map builder page, through the builder's own menus, bars, mouse and keys:
    //   1. a map in My maps from each template (Blank first), named, saved from the File menu, and the builder left;
    //   2. a dungeon room called "Probe Crypt": the menu bar opened with Alt; rough ground dragged and the floor filled
    //      with grass (Terrain); a fence run (Walls and doors, its pop-up shut with Esc); a barrel from the Props picker,
    //      placed once and selected; a two-by-two house from the Buildings picker, Ctrl held so the hand keeps it, then
    //      Esc to put it down; a Shift-drag scatter of pines; the start and two spawns (Spawn); an undo; saved;
    //   3. reopened by its name from the page's My maps and found identical; copied to a campaign (twice: the second
    //      asks, and Rename makes it _2) and that campaign loads it;
    //   4. played: Play this map from the File menu, a level 3 fighter against a goblin a spawn, fought to the end;
    //   5. and back in the builder with the map as it was.
    // A library and a campaign of its own in user://map_probe_<pid>, never the player's, gone when it's done.
    // Prints "mapprobe passed" or "mapprobe FAILED". THE STAGES ARE STATIC: each scene change makes a new probe
    public partial class MapBuilderProbe : Node
    {
        public const string MapId = "probe_crypt";

        const string MapName = "Probe Crypt";

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

        // the first thing: a library and a campaign of the probe's own, and the Map builder page
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
            Stage.Templates or Stage.Editing => new Action[] { OpenPage, NameRule, () => { _stage = Stage.Editing; NewMap("dungeon_room", MapName); } },
            Stage.Reopening => new Action[] { OpenPage, OpenFromMyMaps },
            _ => new Action[] { Finish },
        };

        // --- in the builder -----------------------------------------------------------------------------------------

        MapDraft Draft => Builder.View.Draft;

        MapEditor Editor => Builder.Editor;

        Action[] InBuilder() => _stage switch
        {
            Stage.Templates => new Action[] { CheckTemplate, SaveFromMenu, Leave },
            Stage.Editing => new Action[]
            {
                CheckScreen, Menus, Paint, Fill, Fence, Barrel, Building, Person, Scatter, Start, Spawns, Undo, Save, Remember, Leave,
            },
            Stage.Reopening => new Action[] { CheckReopened, CopyToCampaign, CopyAgain, CheckCampaign, PlayIt, Fight },
            Stage.Fighting => new Action[] { CheckBack, Leave },
            _ => new Action[] { Finish },
        };

        void CheckTemplate()
        {
            string template = MapTemplates.Ids[_template];

            Expect(Builder.View.Place.IsLibrary && Builder.View.Id == $"probe_{template}", $"{template}: a new map in My maps");
            Expect(Draft.Name == $"probe_{template}", $"{template}: named as typed");
            Expect(Builder.Panels.Title.StartsWith(Ui.Say(MapPlace.LibraryKey)), $"{template}: the title says where it lives ({Builder.Panels.Title})");

            string name = Draft.Name;
            Draft.Name = "";

            if (MapTemplates.TakesTheSize(template))
                Expect(Draft.Columns == BlankSize.X && Draft.Rows == BlankSize.Y && Draft.Start == new Cell(BlankSize.X / 2, BlankSize.Y / 2),
                       $"{template}: at the size chosen, {Draft.Columns} x {Draft.Rows}, the start in the middle ({Draft.Start})");
            else Expect(Draft.Save() == MapTemplates.Read(template).Save(), $"{template}: the map is the template, at its own size");

            Draft.Name = name;

            _template++;
        }

        // the screen: two bars and no GM screen; the problems folded to a button; the name in the title
        void CheckScreen()
        {
            Expect(Builder.View.Id == MapId && Draft.Name == MapName, $"the name is {MapName} and its id {MapId}");
            Expect(Builder.Panels.Title.Contains(MapName), $"the title says its name ({Builder.Panels.Title})");
            Expect(Builder.Table.GmScreen == null || !Builder.Table.GmScreen.Visible, "the GM screen isn't on the table");
            Expect(Named<MenuBar>("Menus") != null && Named<Control>("Bar") != null, "a menu bar along the top and a bar along the bottom");
            Expect(Named<Button>("ProblemsButton") != null, "the problems folded to a button");
        }

        // Alt opens the menu bar on File; the arrows and Enter are the PopupMenu's own
        void Menus()
        {
            Key(Godot.Key.Alt, ctrl: false);
            PopupMenu file = Named<MenuBar>("Menus")?.GetMenuPopup(0);
            Expect(file?.Visible == true, "Alt opened the File menu");
            file?.Hide();
        }

        void Paint()
        {
            Key(Godot.Key.B, ctrl: false);
            Expect(Editor.Tool == MapTool.Paint, "B took up Paint");
            Menu("Terrain", MapBuilderView.RuleKey(Tile.Rough));
            Drag(Centre(new Cell(4, 4)), Centre(new Cell(5, 5)));
            Expect(Draft.At(new Cell(4, 4)) == Tile.Rough && Draft.At(new Cell(5, 5)) == Tile.Rough, "the drag painted the rough");
        }

        void Fill()
        {
            Menu("Terrain", MapEditor.ToolKey(MapTool.Fill));
            Menu("Terrain", MapBuilderView.GroundKey(Ground.Grass));
            Expect(Editor.Tool == MapTool.Fill, "Terrain's Fill, and grass, keeps Fill in hand");
            Click(Centre(new Cell(1, 1)));
            Expect(Draft.GroundAt(new Cell(10, 8)) == Ground.Grass && Draft.At(new Cell(10, 8)) == Tile.Floor, "the fill laid grass up to the walls");
            Expect(Draft.GroundAt(new Cell(4, 4)) == Ground.Stone, "and not over the rough, which is another ground's square");
        }

        // one placed, and selected: the next click picks, it doesn't place another
        void Barrel()
        {
            PickProp("barrel", buildings: false);
            Expect(Builder.Panels.Open == null, "choosing it closed the picker");
            Click(Centre(new Cell(2, 6)));
            Expect(Draft.Props.Count(p => p.Id == "barrel") == 1 && Draft.Props.Any(p => p.Id == "barrel" && p.Cell == new Cell(2, 6)),
                   "the barrel stands where it was clicked");
            Expect(!Editor.InHand && Editor.Tool == MapTool.Select && Editor.Picked?.Id == "barrel", "and is selected, the hand empty");
            Expect(Named<Control>("Context")?.GetChildren().OfType<Button>().Select(b => b.Text).ToList() is { } verbs &&
                   verbs.Contains(Ui.Say(MapBuilderView.DeleteKey)) && verbs.Contains(Ui.Say(MapBuilderView.RotateKey)) &&
                   !verbs.Any(v => v == "Copy" || v == "Paste"),
                   "the selection shows Delete and Turn, and no Copy or Paste (cc_task_m3 Part 8)");

            Click(Centre(new Cell(9, 2)));
            Expect(Draft.Props.Count(p => p.Id == "barrel") == 1, "a second click placed no second barrel");

            Key(Godot.Key.Escape, ctrl: false);
            Expect(Editor.Chosen == null, "Esc let the selection go");
        }

        // Ctrl held: the hand keeps it; Esc puts it down
        void Building()
        {
            PickProp("house_small", buildings: true);

            Click(Centre(new Cell(5, 6)), ctrl: true);
            Expect(Draft.Props.Any(p => p.Id == "house_small" && p.Cell == new Cell(5, 6)), "the house stands where it was clicked");
            Expect(Draft.IsBlocked(new Cell(6, 7)), "over all four of its squares");
            Expect(Editor.InHand && Editor.PropId == "house_small", "Ctrl held: the house still in hand");

            Key(Godot.Key.Escape, ctrl: false);
            Expect(!Editor.InHand, "Esc put it down");
        }

        // Shift held along a row: a pine every other square
        void Scatter()
        {
            PickProp("pine_1", buildings: false);

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

            Key(Godot.Key.Escape, ctrl: false);
        }

        void Start()
        {
            Menu("Spawn", MapEditor.ToolKey(MapTool.Start));
            Click(Centre(new Cell(3, 3)));
            Expect(Draft.Start == new Cell(3, 3), $"the start moved (it is on {Draft.Start})");
        }

        void Spawns()
        {
            Menu("Spawn", MapBuilderView.SpawnKey, 1);
            Click(Centre(new Cell(9, 5)));
            Menu("Spawn", MapBuilderView.SpawnKey, 2);
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
            Expect(!Editor.Unsaved, $"Ctrl+S saved {Builder.View.Id}");
            Expect(MapFiles.Has(MapBuilderPage.Library, Builder.View.Id), $"into My maps");
        }

        void SaveFromMenu()
        {
            MenuItem("File", MapBuilderView.SaveKey);
            Expect(!Editor.Unsaved, $"File, Save saved {Builder.View.Id}");
        }

        void Remember()
        {
            _saved = Draft.Save();
            Expect(Draft.Sound, "the map has no problems: " + string.Join("; ", Draft.Problems()));
            _stage = Stage.Reopening;
        }

        void Leave() => Bar("Leave");

        void CheckReopened()
        {
            Expect(Builder.View.Id == MapId && Builder.View.Place.IsLibrary, "reopened from My maps");
            Expect(Draft.Save() == _saved, "the reopened map is the one saved");
            Expect(Draft.Name == MapName, "with its name");
        }

        void CopyToCampaign()
        {
            Key(Godot.Key.C, ctrl: true, shift: true);
            Expect(Builder.Panels.Asking, "Ctrl+Shift+C, Copy to campaign, asks where");
            Builder.Panels.Write(copy: true, CampaignPlace, MapId, replace: false, MapName);
            Expect(MapFiles.Has(CampaignPlace, MapId), "copied into the campaign");
            Expect(Content.Maps.MapName.Of(CampaignPlace, MapId) == MapName, "under its name");
            Expect(Builder.View.Place.IsLibrary, "and the map open is still My maps'");
        }

        void CopyAgain()
        {
            Builder.Panels.Write(copy: true, CampaignPlace, MapId, replace: false, MapName);
            Expect(Builder.Panels.Asking, "a second copy asks: the name is taken");
            Press(MapBuilderView.RenameKey);
            Expect(MapFiles.Has(CampaignPlace, MapId + "_2"), "Rename copied it as the next free id");
        }

        void CheckCampaign()
        {
            Package pack = Package.Read(Campaign);

            Expect(pack.Maps.ContainsKey(MapId), "its campaign loads it: " + string.Join("; ", pack.Problems.Select(p => p.What)));
            Expect(pack.PropsOn(MapId).Any(p => p.Id == "house_small"), "with its house");
            Expect(pack.LookOn(MapId)?.At(new Cell(1, 1)) == Ground.Grass, "and its grass");
        }

        void PlayIt()
        {
            MenuItem("File", MapTrial.PlayKey);

            TrialPanel trial = Builder.Panels.Trial;
            Expect(trial != null, "File, Play this map opens its panel");
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
    }
}
