using System;
using System.Linq;
using Content.Workshop;
using Game.Access;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // A CAMPAIGN'S OWN PAGE (cc_task_m Parts 4-6): its tabs (About, Chapters and maps, Characters; the table editors and
    // the story editor add theirs), the problems panel beside them, Save, Undo and Redo (Ctrl+S, Ctrl+Z, Ctrl+Y: the
    // map builder's acts, rebindable on the controls page), Advanced, and the way back to the hub. A view over
    // CampaignEditor: every change is one of its Edits (one undo step), the autosave keeps the draft a few seconds after
    // the last one, and a draft from last time is asked about when the page opens
    public partial class CampaignScreen : VBoxContainer
    {
        // seconds after the last change before the draft is kept
        public const double AutosaveAfter = 3;

        readonly Launch _launch;
        readonly bool _fresh;

        TabContainer _tabs;
        VBoxContainer _problems;
        Label _status;
        Label _title;
        Timer _autosave;
        bool _advanced;

        CampaignScreen(Launch launch, CampaignEditor editor, bool fresh)
        {
            _launch = launch;
            Editor = editor;
            _fresh = fresh;
            Name = "CampaignScreen";
        }

        public CampaignEditor Editor { get; }

        CampaignDraft Draft => Editor.Draft;

        // the page open now (the probe and the layout check reach it here)
        public static CampaignScreen Current { get; private set; }

        // the page, on a panel over the book. `fresh`: the campaign was just opened from disk (and not handed back by
        // the builder), so an autosave newer than its files is asked about
        public static CampaignScreen Show(Launch launch, CampaignEditor editor, WorkshopTab tab = WorkshopTab.About, bool fresh = true)
        {
            GameState.Editing = editor;
            Current = new CampaignScreen(launch, editor, fresh) { Tab = tab };
            launch.Show(Ui.Panel(Current), 1100);
            return Current;
        }

        public WorkshopTab Tab { get; set; }

        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 8);

            _autosave = new Timer { OneShot = true, WaitTime = AutosaveAfter, Name = "Autosave" };
            _autosave.Timeout += KeepDraft;
            AddChild(_autosave);

            Editor.Undo.Changed += Changed;

            Build();
        }

        public override void _ExitTree()
        {
            Editor.Undo.Changed -= Changed;
            if (Current == this) Current = null;
        }

        void Build()
        {
            _title = Ui.Plain(Hub.Named(CampaignFiles.NameKey(Editor.Id), Draft.Name));
            _title.ThemeTypeVariation = "TitleLabel";
            _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var advanced = new CheckButton { Name = "Advanced", Text = Ui.Say(WorkshopWords.AdvancedKey), ButtonPressed = _advanced };
            advanced.Toggled += on => { _advanced = on; Redraw(); };

            Button save = Ui.Button(Content.Screens.MapBuilderView.SaveKey, Save);
            save.Name = "Save";

            AddChild(Ui.Row(10, _title, save,
                            Ui.Button(Content.Screens.MapBuilderView.UndoKey, Undo),
                            Ui.Button(Content.Screens.MapBuilderView.RedoKey, Redo),
                            advanced,
                            Ui.Button(WorkshopWords.TitleKey, Back)));

            // play-testing (cc_task_q): Play the campaign, the quick hero, Run it for me
            AddChild(TestTools());

            _status = Forms.Hint(WorkshopWords.UnsavedKey);
            _status.Name = "Status";
            _status.Visible = Editor.Unsaved;
            AddChild(_status);

            if (_fresh && Editor.HasDraftFromLastTime) AddChild(DraftQuestion());

            _tabs = new TabContainer { Name = "Tabs", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            _tabs.TabChanged += t => Tab = (WorkshopTab)(int)t;

            _problems = Ui.Column(8);
            _problems.Name = "Problems";

            ScrollContainer problems = Ui.Scroll(_problems, 440);
            problems.CustomMinimumSize = new Vector2(Ui.Px(300), problems.CustomMinimumSize.Y);

            var body = Ui.Row(12, _tabs, problems);
            body.SizeFlagsVertical = SizeFlags.ExpandFill;
            AddChild(body);

            Redraw();
        }

        // EVERY TAB AND THE PROBLEMS DRAWN AGAIN from the draft: after an undo or a redo, or a change that adds or takes
        // away a row. A field being typed into is never redrawn under the typing
        public void Redraw()
        {
            WorkshopTab showing = Tab;

            foreach (Node old in _tabs.GetChildren())
            {
                _tabs.RemoveChild(old);
                old.QueueFree();
            }

            foreach (WorkshopTab tab in Enum.GetValues<WorkshopTab>())
            {
                var page = Ui.Column(12);
                page.Name = "Page";

                switch (tab)
                {
                    case WorkshopTab.About: About(page); break;
                    case WorkshopTab.Chapters: Chapters(page); break;
                    case WorkshopTab.Characters: People(page); break;
                    case WorkshopTab.Story: Story(page); break;
                    case WorkshopTab.Fights: Fights(page); break;
                    case WorkshopTab.Encounters: Encounters(page); break;
                    case WorkshopTab.Loot: Loot(page); break;
                    case WorkshopTab.Shops: Shops(page); break;
                    case WorkshopTab.Lucky: Lucky(page); break;
                }

                ScrollContainer scroll = Ui.Scroll(page, 440);
                scroll.Name = tab.ToString();
                _tabs.AddChild(scroll);
                _tabs.SetTabTitle(_tabs.GetTabCount() - 1, Ui.Say(WorkshopWords.TabKey(tab)));
            }

            _tabs.CurrentTab = (int)showing;
            Tab = showing;

            ProblemsPanel.Fill(_problems, Editor, _advanced, GoThere);
            _title.Text = Hub.Named(CampaignFiles.NameKey(Editor.Id), Draft.Name);
        }

        public void GoThere(WorkshopTab tab)
        {
            Tab = tab;
            _tabs.CurrentTab = (int)tab;
        }

        // an edit through the page: one undo step (CampaignEditor.Edit)
        void Edit(string what, Action<CampaignDraft> change, bool typing = false) => Editor.Edit(what, change, typing);

        // the same, and the page drawn again for it (a row came or went)
        void Change(Func<bool> change)
        {
            change();
            Redraw();
        }

        // --- save, undo, the autosave -------------------------------------------------------------------------------

        // something changed: it's unsaved, and the autosave starts counting again
        void Changed()
        {
            if (GodotObject.IsInstanceValid(_status))
            {
                _status.Text = Ui.Say(WorkshopWords.UnsavedKey);
                _status.Visible = Editor.Unsaved;
            }

            if (IsInsideTree() && Editor.Unsaved) _autosave.Start();
        }

        void KeepDraft()
        {
            string refused = Editor.KeepDraft();
            if (refused != null) GD.PushWarning($"workshop: the autosave of {Editor.Id} - {refused}");
        }

        public void Save()
        {
            string refused = Editor.Save();

            _autosave.Stop();
            ForgetContent();
            GameState.CampaignsChanged(Editor.Folder);

            _status.Visible = true;
            _status.Text = refused != null ? Ui.Say(WorkshopWords.CouldNotKey, refused)
                         : WorkshopProblems.Errors(Editor.Problems) > 0 ? Ui.Say(WorkshopWords.SavedWithProblemsKey)
                         : Ui.Say(WorkshopWords.SavedKey);

            GD.Print($"workshop saved {Editor.Id}: {Editor.Problems.Count} problems, {WorkshopProblems.Errors(Editor.Problems)} errors");

            ProblemsPanel.Fill(_problems, Editor, _advanced, GoThere);
            _title.Text = Hub.Named(CampaignFiles.NameKey(Editor.Id), Draft.Name);
        }

        public void Undo()
        {
            if (Editor.Undo.Undo()) Redraw();
        }

        public void Redo()
        {
            if (Editor.Undo.Redo()) Redraw();
        }

        // back to the hub: anything unsaved kept as the draft, to be asked about next time
        void Back()
        {
            KeepDraft();
            GameState.Editing = null;
            Hub.Show(_launch);
        }

        // "You have unsaved changes from last time. Keep them / Throw them away."
        HBoxContainer DraftQuestion()
        {
            Label question = Ui.Label(WorkshopWords.DraftQuestionKey);
            question.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var row = Ui.Row(8, question);
            row.Name = "DraftQuestion";

            row.AddChild(Ui.Button(WorkshopWords.KeepThemKey, () => { Editor.TakeDraft(); row.QueueFree(); Redraw(); }));
            row.AddChild(Ui.Button(WorkshopWords.ThrowAwayKey, () => { Editor.DropDraft(); row.QueueFree(); }));

            return row;
        }

        // Ctrl+Z, Ctrl+Y and Ctrl+S, by the acts the map builder's are (rebindable together). A text box being typed in
        // has its own undo first
        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey { Pressed: true } key) return;

            Act? act = Keyboard.Pressed(key);

            if (StoryKeys(act))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            switch (act)
            {
                case Act.UndoEdit: Undo(); break;
                case Act.RedoEdit: Redo(); break;
                case Act.SaveMap: Save(); break;
                default: return;
            }

            GetViewport().SetInputAsHandled();
        }

        // the JSON a tab writes, read-only, under Advanced
        void Json(VBoxContainer page, string text)
        {
            if (!_advanced) return;

            page.AddChild(Ui.Label(WorkshopWords.JsonKey));
            page.AddChild(new TextEdit
            {
                Name = "Json",
                Text = text,
                Editable = false,
                CustomMinimumSize = new Vector2(0, Ui.Px(200)),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            });
        }
    }
}
