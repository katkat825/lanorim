using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Maps;
using Content.Screens;
using Content.Workshop;
using Game.Builder;
using Game.Play;
using Game.Screens;
using Game.Workshop;
using Godot;

namespace Game.Dev
{
    // THE WORKSHOP, DRIVEN AS AN AUTHOR WOULD (checks/check-workshop.ps1; cc_task_m "Tests and checks"):
    //
    //   godot --headless --path game res://launch.tscn -- --workshop-probe --auto
    //
    // On a throwaway user:// (user://workshop_probe: My campaigns and My maps of its own, never the player's), through the
    // screens' own fields and buttons:
    //   1. the hub: New campaign first, Subscribed empty, the game's own campaigns (the editor build);
    //   2. the wizard: a name, a blurb, an author, a tag, mature; the defeat rule, three characters, the cave screen; a
    //      first chapter, a new map from a template - Finish, and the map is in the builder, living in the campaign;
    //   3. the builder left, and the campaign's page is back on its chapters; a second chapter, one of My maps copied in,
    //      Ctrl+Z and Ctrl+Y, a character; Save - it loads with no errors and the problems panel says so;
    //   4. its card on the hub, and its line in the book's contents;
    //   5. played from the book's own entry: its one line, then the end (--auto ends the run, "play    the end").
    // Prints "workshop passed" or "workshop FAILED". THE STAGES ARE STATIC: each scene change makes a new probe
    public partial class WorkshopProbe : Node
    {
        public const string Root = "user://workshop_probe";

        public const string CampaignId = "probe_tale";

        public const string MyMap = "probe_meadow";

        public Launch Launch { get; set; }

        public MapBuilder Builder { get; set; }

        enum Stage { Making, Building, Editing, Playing, Done }

        static Stage _stage = Stage.Making;
        static bool _setUp;
        static readonly ProbeLog Said = new("workshop");
        static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();

        const double GiveUpAfter = 300;

        int _frame;
        int _step;
        Action[] _steps;
        bool _said;

        static string Folder => Path.Combine(ProjectSettings.GlobalizePath(Root), "campaigns", CampaignId);

        // the first thing: a My campaigns and a My maps of the probe's own, one map in My maps, and the probe
        public static void AtLaunch(Launch launch)
        {
            if (!_setUp)
            {
                _setUp = true;

                string root = ProjectSettings.GlobalizePath(Root);
                if (Directory.Exists(root)) Directory.Delete(root, true);

                Game.Campaigns.CampaignFolders.MineFolder = Root + "/campaigns";
                MapBuilderPage.LibraryFolder = Root + "/maps";
                GameState.SaveProbesApart();
                GameState.CampaignsChanged();

                MapFiles.Write(MapBuilderPage.Library, MyMap, MapBuilderView.Fresh(10, 8, "empty_field").Draft);
            }

            launch.AddChild(new WorkshopProbe { Name = "WorkshopProbe", Launch = launch });
        }

        public static void AtPlay(PlayDirector director) => director.AddChild(new WorkshopProbe { Name = "WorkshopProbe" });

        public override void _Process(double delta)
        {
            if (Clock.Elapsed.TotalSeconds > GiveUpAfter && _stage != Stage.Done)
            {
                Expect(false, $"finished within {GiveUpAfter} seconds (stuck at {_stage}, step {_step})");
                _stage = Stage.Done;
                Finish();
                return;
            }

            if (_stage == Stage.Playing)
            {
                Listen();
                return;
            }

            if (++_frame % 15 != 0) return;

            _steps ??= Launch != null ? AtLaunchSteps() : Builder != null ? new Action[] { CheckBuilder, LeaveBuilder } : Array.Empty<Action>();

            if (_step < _steps.Length) _steps[_step++]();
        }

        Action[] AtLaunchSteps() => _stage switch
        {
            Stage.Making => new Action[] { OpenHub, NewCampaign, PageOne, PageTwo, PageThree },
            Stage.Editing => new Action[] { BackOnPage, AddChapter, CopyInMyMap, UndoRedo, AddCharacter, Save, ToHub, ToBook, Play },
            _ => Array.Empty<Action>(),
        };

        // --- 1. the hub --------------------------------------------------------------------------------------------

        void OpenHub()
        {
            Launch.PageCalled(WorkshopPage.PageId)?.Open(Launch);

            // New campaign at the top of My campaigns (cc_task_m2 Part 6): the first button after the way back
            Button first = Nodes.Under<Button>(Launch).Where(b => b.IsVisibleInTree()).Skip(1).FirstOrDefault();
            Expect(first?.Name == "NewCampaign", $"New campaign is the first thing in My campaigns ({first?.Name})");
            Expect(Has<Label>(l => l.Text == Ui.Say(WorkshopWords.SubscribedEmptyKey)), "Subscribed says it fills when sharing arrives");
            Expect(Has<PanelContainer>(p => Nodes.Under<HBoxContainer>(p).Any(r => r.Name == "Card_sample_millbrook")),
                   "the game's own campaigns are on the hub");
            Expect(Has<Button>(b => b.Name == "Copy_sample_millbrook"), "each with Make an editable copy");
            Expect(Has<Button>(b => b.Name == "Open_sample_millbrook") == WorkshopPage.EditorBuild,
                   "and Open the original only in the editor build");
            Expect(Has<HBoxContainer>(r => r.Name == "MyMap_" + MyMap), "My maps lists the probe's map");
        }

        void NewCampaign()
        {
            Press("NewCampaign");
            Expect(WizardScreen.Current != null, "New campaign opens the wizard");
        }

        // --- 2. the wizard -----------------------------------------------------------------------------------------

        void PageOne()
        {
            Type<LineEdit>("Name", "Probe Tale");
            TextEdit blurb = Find<TextEdit>("Description");
            if (blurb != null) { blurb.Text = "A campaign the probe made."; blurb.EmitSignal(TextEdit.SignalName.TextChanged); }
            Type<LineEdit>("Author", "The probe");
            Tick("Tag_mystery");
            Tick("Mature");

            CampaignWizard wizard = WizardScreen.Current.Wizard;
            Expect(wizard.Id == CampaignId, $"the id is made from the name ({wizard.Id})");
            Expect(!Find<Button>("Next").Disabled, "Next is open once it has a name");
            Press("Next");
            Expect(wizard.Page == WizardPage.Rules, "on to How it plays");
        }

        void PageTwo()
        {
            Choose(Find<OptionButton>("Defeat"), 1);
            SpinBox characters = Find<SpinBox>("Characters");
            if (characters != null) characters.Value = 3;
            Press("Screen_cave");

            CampaignDraft draft = WizardScreen.Current.Wizard.Draft;
            Expect(draft.Defeat == Core.Combat.Defeat.Continues && draft.Characters == 3 && draft.GmScreen == "cave",
                   $"the rules: the story goes on, 3 characters, the cave screen ({draft.Defeat}, {draft.Characters}, {draft.GmScreen})");
            Press("Next");
        }

        void PageThree()
        {
            Type<LineEdit>("ChapterTitle", "First Steps");
            Choose(Find<OptionButton>("Template"), MapTemplates.Ids.ToList().IndexOf("empty_field"));

            _stage = Stage.Building;
            Press("Finish");

            Expect(Directory.Exists(Folder), "Finish wrote the campaign into My campaigns");
        }

        // --- in the builder ------------------------------------------------------------------------------------------

        void CheckBuilder()
        {
            Expect(!Builder.View.Place.IsLibrary && Builder.View.Place.Id == CampaignId && Builder.View.Id == "first_steps",
                   $"Make opened the first map in the builder, living in the campaign ({Builder.View.Place}, {Builder.View.Id})");
            // made from the template asked for, and called what its chapter is (cc_task_m2 Part 7)
            MapDraft drawn = Builder.View.Draft;
            string name = drawn.Name;
            drawn.Name = "";
            Expect(drawn.Save() == MapTemplates.Make("empty_field", MapTemplates.Read("empty_field").Columns,
                                                     MapTemplates.Read("empty_field").Rows).Save(), "made from the template asked for");
            drawn.Name = name;
            Expect(name.Length > 0, $"and named after its chapter ({name})");
        }

        void LeaveBuilder()
        {
            _stage = Stage.Editing;
            Nodes.Under<Button>(Builder).FirstOrDefault(b => b.Text == Ui.Say(MapBuilderView.LeaveKey))?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        // --- 3. the campaign's page ----------------------------------------------------------------------------------

        CampaignScreen Page => CampaignScreen.Current;

        void BackOnPage()
        {
            // the launch screen's own first screen: back from the table, the page that sent it there
            Launch.ShowFirst("title", null);

            Expect(Page != null && Page.Editor.Id == CampaignId, "leaving the builder came back to the campaign's page");
            Expect(Page?.Tab == WorkshopTab.Chapters, $"on its chapters ({Page?.Tab})");
            Expect(Page?.Editor.Draft.Chapters.Single().Title == "First Steps", "with its first chapter");
        }

        void AddChapter()
        {
            Type<LineEdit>("NewChapter", "Second Steps", changed: false);
            Press("AddChapter");
            Expect(Page.Editor.Draft.Chapters.Select(c => c.Id).SequenceEqual(new[] { "first_steps", "second_steps" }), "a second chapter added");
        }

        void CopyInMyMap()
        {
            Choose(Find<OptionButton>("MyMaps_second_steps"), 0);
            Press("AddFromMyMaps_second_steps");
            Expect(Page.Editor.Draft.Chapter("second_steps")?.Maps.SequenceEqual(new[] { MyMap }) == true, "one of My maps copied onto it");
            Expect(MapFiles.Has(Page.Editor.Maps, MyMap), "and into the campaign's maps");
        }

        void UndoRedo()
        {
            GetViewport().GuiReleaseFocus();

            Key(Godot.Key.Z);
            Expect(Page.Editor.Draft.Chapter("second_steps")?.Maps.Count == 0, "Ctrl+Z took the map back off");
            Key(Godot.Key.Y);
            Expect(Page.Editor.Draft.Chapter("second_steps")?.Maps.Count == 1, "Ctrl+Y put it back");
        }

        void AddCharacter()
        {
            Page.GoThere(WorkshopTab.Characters);
            Type<LineEdit>("NewPerson", "Old Wenna", changed: false);
            Press("AddPerson");
            Expect(Page.Editor.Draft.Character("old_wenna")?.Name == "Old Wenna", "a character named");
        }

        void Save()
        {
            Press("Save");

            Package pack = Package.Read(Folder);
            Expect(!Page.Editor.Unsaved, "Save wrote it");
            Expect(pack.Sound, "it loads with no errors: " + string.Join("; ", pack.Problems));
            Expect(pack.Manifest?.Chapters.Count == 2 && pack.Manifest.Rules.Defeat == Core.Combat.Defeat.Continues && pack.Manifest.GmScreen == "cave",
                   "with its two chapters and its rules");
            Expect(WorkshopProblems.Errors(Page.Editor.Problems) == 0, "and the problems panel has no errors: " + string.Join("; ", Page.Editor.Problems));
        }

        void ToHub()
        {
            Nodes.Under<Button>(Launch).FirstOrDefault(b => b.Text == Ui.Say(WorkshopWords.TitleKey) && b.IsVisibleInTree())
                 ?.EmitSignal(BaseButton.SignalName.Pressed);

            Expect(Has<HBoxContainer>(r => r.Name == "Card_" + CampaignId), "its card is on the hub");
            Expect(!Has<Label>(l => l.Text.StartsWith(Ui.Say(WorkshopWords.WontLoadOneKey).Split(':')[0])), "and it doesn't say it won't load");
        }

        void ToBook()
        {
            Launch.ShowBook();

            Expect(new CampaignBook(GameState.Manifests, GameState.Saves).Pages.Any(p => p.Id == CampaignId), "it's in the book's campaign list");
            // the book's pages stand themselves up a frame later (BookPages), so the line is looked for whether shown yet or
            // not; on the panels (a book too small to read) it's a button with the campaign's name
            Expect(Nodes.Under<HBoxContainer>(Launch).Any(r => r.Name == $"Campaign {CampaignId}") ||
                   Nodes.Under<Button>(Launch).Any(b => b.Text == "Probe Tale"), "with its own line in the contents");
        }

        void Play()
        {
            _stage = Stage.Playing;
            GD.Print($"workshop playing {CampaignId}");
            QuickStart.Begin(Launch, CampaignId, "fighter");
        }

        // --- 5. played to its one line ---------------------------------------------------------------------------------

        static string Line => Content.Dialogue.DialogueKeys.Line(CampaignWizard.Speaker, CampaignId, CampaignWizard.LineId);

        void Listen()
        {
            if (_said || GameState.Run?.Talk.Heard.All(h => h.Key != Line) != false) return;

            _said = true;
            Expect(true, $"played: the GM said its one line ({Ui.Say(Line)})");
            Expect(Ui.Say(Line) == Ui.Say(WorkshopWords.StoryStartsKey), "in the campaign's own words");
            _stage = Stage.Done;
            Finish();
        }

        public override void _ExitTree()
        {
            if (_stage == Stage.Playing && Launch == null) Listen();
        }

        // --- the end ------------------------------------------------------------------------------------------------

        static void Finish()
        {
            try
            {
                string root = ProjectSettings.GlobalizePath(Root);
                if (_stage == Stage.Done && Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch (IOException) { }

            Said.Verdict();
        }

        static void Expect(bool held, string what) => Said.Expect(held, what);

        // --- the author's hands ---------------------------------------------------------------------------------------

        Node Here => (Node)Launch ?? Builder;

        T Find<T>(string name) where T : Node => Nodes.Under<T>(Here).FirstOrDefault(n => n.Name == name && (n is not CanvasItem c || c.IsVisibleInTree()));

        bool Has<T>(Func<T, bool> what) where T : Node => Nodes.Under<T>(Here).Any(n => (n is not CanvasItem c || c.IsVisibleInTree()) && what(n));

        void Press(string name)
        {
            BaseButton button = Find<BaseButton>(name);
            Expect(button != null, $"a button called {name}");
            button?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        void Type<T>(string name, string text, bool changed = true) where T : LineEdit
        {
            LineEdit line = Find<LineEdit>(name);
            Expect(line != null, $"a box called {name}");
            if (line == null) return;

            line.Text = text;
            if (changed) line.EmitSignal(LineEdit.SignalName.TextChanged, text);
        }

        void Tick(string name)
        {
            BaseButton tick = Find<BaseButton>(name);
            Expect(tick != null, $"a tick box called {name}");
            if (tick != null) tick.ButtonPressed = true;
        }

        static void Choose(OptionButton list, int index)
        {
            if (list == null || index < 0) return;
            list.Selected = index;
            list.EmitSignal(OptionButton.SignalName.ItemSelected, index);
        }

        void Key(Key key) =>
            GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, CtrlPressed = true });
    }
}
