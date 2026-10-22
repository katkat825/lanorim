using System;
using System.Linq;
using Content.Maps;
using Content.Workshop;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE NEW-CAMPAIGN WIZARD (cc_task_m Part 3): three short pages over CampaignWizard - its name and blurb, how it plays,
    // its first chapter and map - every field with a one-line hint under it. Finish writes the pack, and either opens the
    // new map in the builder (Make; the builder comes back to the campaign's page) or the campaign's page (Pick)
    public sealed class WizardScreen
    {
        readonly Launch _launch;

        bool _advanced;

        Button _next;
        Label _why;
        LineEdit _id;

        WizardScreen(Launch launch) => _launch = launch;

        public CampaignWizard Wizard { get; } = new(WorkshopPage.Taken);

        // the wizard open now (the probe fills it through its own fields)
        public static WizardScreen Current { get; private set; }

        public static WizardScreen Begin(Launch launch)
        {
            Current = new WizardScreen(launch);
            Current.Show();
            return Current;
        }

        public void Show()
        {
            var form = Ui.Column(12);
            form.Name = "WizardForm";

            switch (Wizard.Page)
            {
                case WizardPage.About:
                    Forms.About(form, Wizard.Draft, Edit, _advanced, Wizard.Id, typed => { Wizard.TypedId = typed; Refresh(); });
                    _id = Nodes.Under<LineEdit>(form).FirstOrDefault(l => l.Name == "Id");
                    break;

                case WizardPage.Rules:
                    Forms.Rules(form, Wizard.Draft, Edit);
                    break;

                case WizardPage.FirstChapter:
                    FirstChapter(form);
                    break;
            }

            int step = (int)Wizard.Page + 1;

            var buttons = Ui.Row(10);

            if (Wizard.Page != WizardPage.About)
                buttons.AddChild(Ui.Button(WorkshopWords.BackKey, () => { Wizard.Back(); Show(); }));

            bool last = Wizard.Page == WizardPage.FirstChapter;
            _next = Ui.Button(last ? WorkshopWords.FinishKey : WorkshopWords.NextKey, last ? Finish : () => { if (Wizard.Next()) Show(); });
            _next.Name = last ? "Finish" : "Next";
            buttons.AddChild(_next);
            buttons.AddChild(Ui.Button(WorkshopWords.CancelKey, () => Hub.Show(_launch)));

            var advanced = new CheckButton { Name = "Advanced", Text = Ui.Say(WorkshopWords.AdvancedKey), ButtonPressed = _advanced };
            advanced.Toggled += on => { _advanced = on; Show(); };
            buttons.AddChild(advanced);

            _why = Forms.Hint(WorkshopWords.NameHintKey);

            var page = Ui.Column(10,
                Ui.Title(WorkshopWords.WizardTitleKey),
                Ui.Label(WorkshopWords.StepKey, step, Ui.Say(WorkshopWords.PageKey(Wizard.Page))),
                Ui.Scroll(form, 480),
                _why,
                buttons);
            page.Name = "Wizard";

            _launch.Show(Ui.Panel(page), 900);
            Refresh();
        }

        // a field changed: the draft takes it, and Next and the reason it's greyed are said again
        void Edit(string what, Action<CampaignDraft> change, bool typing = false)
        {
            change(Wizard.Draft);
            Refresh();
        }

        void Refresh()
        {
            string missing = Wizard.Missing(Wizard.Page);

            if (GodotObject.IsInstanceValid(_next)) _next.Disabled = missing != null;

            if (GodotObject.IsInstanceValid(_why))
            {
                _why.Visible = missing != null;
                if (missing != null) _why.Text = Ui.Say(missing);
            }

            if (GodotObject.IsInstanceValid(_id) && !_id.HasFocus()) _id.Text = Wizard.Id;
        }

        // --- the third page -------------------------------------------------------------------------------------------

        void FirstChapter(VBoxContainer form)
        {
            form.AddChild(Forms.Field(WorkshopWords.ChapterTitleKey,
                Forms.Line("ChapterTitle", Wizard.ChapterTitle, t => { Wizard.ChapterTitle = t; Refresh(); }), WorkshopWords.ChapterTitleHintKey));

            form.AddChild(Forms.Field(WorkshopWords.FirstMapKey,
                Forms.Choice("FirstMap", Enum.GetValues<FirstMap>(), Wizard.FirstMap, WorkshopWords.FirstMapChoiceKey, v => { Wizard.FirstMap = v; Show(); }),
                WorkshopWords.FirstMapHintKey));

            if (Wizard.FirstMap == FirstMap.Make)
            {
                form.AddChild(Forms.Field(Content.Screens.MapBuilderView.TemplateKey,
                    Forms.Choice("Template", MapTemplates.Ids, Wizard.Template, MapTemplates.NameKey, v => Wizard.Template = v),
                    WorkshopWords.TemplateHintKey));
                return;
            }

            var mine = MapFiles.Ids(MapBuilderPage.Library);

            if (mine.Count == 0)
            {
                form.AddChild(Forms.Hint(WorkshopWords.NoMyMapsKey));
                return;
            }

            if (!mine.Contains(Wizard.Picked)) Wizard.Picked = mine[0];

            var list = new OptionButton { Name = "Picked", ClipText = true, FitToLongestItem = false };
            for (int i = 0; i < mine.Count; i++)
            {
                list.AddItem(MapName.Of(MapBuilderPage.Library, mine[i]), i);
                if (mine[i] == Wizard.Picked) list.Selected = i;
            }
            list.ItemSelected += i => { Wizard.Picked = mine[(int)i]; Refresh(); };

            form.AddChild(Forms.Field(MapPlace.LibraryKey, list, null));
        }

        // --- finish ---------------------------------------------------------------------------------------------------

        void Finish()
        {
            string folder = Wizard.Finish(WorkshopPage.Mine, MapBuilderPage.Library, Ui.Say(WorkshopWords.StoryStartsKey), out string problem);

            if (folder == null)
            {
                GD.PushWarning($"workshop: the campaign could not be made - {problem}");
                _why.Visible = true;
                _why.Text = Ui.Say(WorkshopWords.CouldNotKey, problem);
                return;
            }

            GD.Print($"workshop made {Wizard.Draft.Id} in {folder}");

            GameState.CampaignsChanged(folder);
            CampaignEditor editor = CampaignEditor.Open(folder);
            Current = null;

            // MAKE: the new map, in the builder, living in the campaign; leaving it comes back to the campaign's page
            if (Wizard.FirstMap == FirstMap.Make)
            {
                GameState.Editing = editor;
                MapBuilderPage.OpenMap(_launch, editor.Maps, Wizard.MapId);
                return;
            }

            CampaignScreen.Show(_launch, editor);
        }
    }
}
