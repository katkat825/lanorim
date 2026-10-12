using System;
using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // THE MAP BUILDER'S PANELS (cc_task_f Part 2; cc_task_l), built plain from the Ui helpers and the theme, every number in
    // BuilderLayout: down the left the file's buttons, the tools and what the tool in hand lays (MapBuilderUi.Options.cs);
    // down the right the problems, live, each a button that shows where; and the questions (MapBuilderUi.Files.cs) and
    // the Play this map panel (TrialPanel) over the middle. How it looks is Kathleen's (#5): lay it out as you like
    public partial class MapBuilderUi : Control
    {
        public MapBuilder Builder { get; set; }

        MapEditor Editor => Builder.Editor;

        // the tools as many to a row as fit, so what the tool in hand lays has the height (cc_task_l: eight tools now);
        // a flow keeps to the panel's width, where a grid of two pushed the panel out over the board
        HFlowContainer _tools;
        VBoxContainer _options;
        VBoxContainer _problems;
        Label _title;
        Label _notice;
        Control _asking;

        // what each button checks to show itself pressed: the panel's own, and the options page's, which go with it
        readonly List<Action> _updates = new();
        readonly List<Action> _optionUpdates = new();

        double _noticeLeft;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            BuilderLayout layout = BuilderLayout.Current;

            AddChild(Side(left: true, layout.ToolsWidth, Ui.Column(layout.Gap,
                _title = Ui.Title(MapBuilderView.TitleKey),
                Files(),
                _tools = Ui.Flow(layout.Gap / 2),
                Ui.Scroll(_options = Ui.Column(layout.Gap / 2)))));

            AddChild(Side(left: false, layout.ProblemsWidth, Ui.Column(layout.Gap,
                Ui.Title(MapBuilderView.ProblemsKey),
                Ui.Scroll(_problems = Ui.Column(layout.Gap / 2)))));

            _notice = new Label
            {
                Name = "Notice",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                AnchorLeft = 0.5f,
                AnchorRight = 0.5f,
                AnchorTop = 1,
                AnchorBottom = 1,
                OffsetLeft = -360,
                OffsetRight = 360,
                OffsetTop = -layout.EdgeMargin - 80,
                OffsetBottom = -layout.EdgeMargin,
                Visible = false,
            };
            AddChild(_notice);

            Tools();
            Refresh();
        }

        // a panel the height of the screen on one side
        Control Side(bool left, int width, Control content)
        {
            int margin = BuilderLayout.Current.EdgeMargin;
            PanelContainer panel = Ui.Panel(content);

            panel.AnchorLeft = left ? 0 : 1;
            panel.AnchorRight = left ? 0 : 1;
            panel.AnchorBottom = 1;
            panel.OffsetLeft = left ? margin : -width - margin;
            panel.OffsetRight = left ? margin + width : -margin;
            panel.OffsetTop = margin;
            panel.OffsetBottom = -margin;

            return panel;
        }

        Control Files()
        {
            HFlowContainer row = Ui.Flow(BuilderLayout.Current.Gap);

            row.AddChild(Ui.Button(MapBuilderView.SaveKey, Builder.Save));
            row.AddChild(Ui.Button(MapBuilderView.SaveAsKey, () => AskWhere(copy: false)));
            row.AddChild(Ui.Button(MapBuilderView.CopyToKey, () => AskWhere(copy: true)));
            row.AddChild(Ui.Button(MapBuilderView.UndoKey, Builder.Undo));
            row.AddChild(Ui.Button(MapBuilderView.RedoKey, Builder.Redo));
            row.AddChild(Toggle(Ui.Say(MapBuilderView.TopDownKey), () => Builder.ToggleTopDown(), () => Builder.TopDown));
            row.AddChild(Ui.Button(MapTrial.PlayKey, ShowTrial));
            row.AddChild(Ui.Button(MapBuilderView.LeaveKey, () => Builder.Leave()));

            return row;
        }

        // the tools, one pressed
        void Tools()
        {
            foreach (MapTool tool in MapEditor.Tools)
            {
                MapTool one = tool;
                _tools.AddChild(Toggle(Ui.Say(MapEditor.ToolKey(one)), () =>
                {
                    Editor.Tool = one;
                    if (one != MapTool.Select) Editor.Deselect();
                    Builder.Chose();
                }, () => Editor.Tool == one));
            }
        }

        // a button that stays pressed while `chosen` is true
        Button Toggle(string words, Action pick, Func<bool> chosen)
        {
            Button button = Ui.Button(words, pick, true);
            button.ToggleMode = true;
            _updates.Add(() => button.SetPressedNoSignal(chosen()));
            return button;
        }

        // --- the problems, live -------------------------------------------------------------------------------------

        void Problems()
        {
            Ui.Clear(_problems);

            IReadOnlyList<MapProblem> problems = Builder.View.Problems;

            if (problems.Count == 0) _problems.AddChild(Ui.Label(MapBuilderView.NoProblemsKey));

            foreach (MapProblem problem in problems)
            {
                MapProblem one = problem;
                Button button = Ui.Button(Game.Access.AccessDesk.Words(one.Said), () => Builder.Show(one), true);
                button.Alignment = HorizontalAlignment.Left;
                button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                button.Disabled = one.At == null;
                _problems.AddChild(button);
            }
        }

        // --- the whole screen, again --------------------------------------------------------------------------------

        // "My maps / mill_yard •": where it lives, its name, and a dot while there are changes not saved
        public string Title =>
            $"{Ui.Say(Builder.View.Place.NameKey ?? Builder.View.Place.Id ?? "")} / {Builder.View.Id}{(Editor.Unsaved ? " •" : "")}";

        public void Refresh()
        {
            Options();
            Problems();

            _title.Text = Title;

            foreach (Action update in _updates.Concat(_optionUpdates)) update();
        }

        public void Notice(string words)
        {
            _notice.Text = words ?? "";
            _notice.Visible = true;
            _noticeLeft = BuilderLayout.Current.NoticeSeconds;
        }

        public override void _Process(double delta)
        {
            if (!_notice.Visible) return;

            _noticeLeft -= delta;
            if (_noticeLeft <= 0) _notice.Visible = false;
        }

        // --- questions over the middle ------------------------------------------------------------------------------

        // one question at a time, in the middle of the screen
        void Ask(Control question, string name)
        {
            Dismiss();

            _asking = question;
            _asking.Name = name;

            // grown out from the middle both ways, so a tall question isn't pushed off the top of the screen
            _asking.GrowHorizontal = GrowDirection.Both;
            _asking.GrowVertical = GrowDirection.Both;
            _asking.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            AddChild(_asking);

            // the question's first button, not the panel, which takes no focus
            Control asking = _asking;
            Callable.From(() => { if (IsInstanceValid(asking)) Ui.FocusFirst(asking); }).CallDeferred();
        }

        // "this map has changes that aren't saved": leave anyway, or stay
        public void AskToLeave() =>
            Ask(Ui.Panel(Ui.Column(BuilderLayout.Current.Gap,
                Ui.Label(MapBuilderView.UnsavedKey),
                Ui.Row(BuilderLayout.Current.Gap,
                    Ui.Button(MapBuilderView.SaveKey, () => { Builder.Save(); Dismiss(); }),
                    Ui.Button(MapBuilderView.LeaveAnywayKey, () => Builder.Leave(anyway: true)),
                    Ui.Button(MapBuilderView.StayKey, Dismiss)))), "Unsaved");

        // PLAY THIS MAP: the hero, the spawns and the budget, over the middle (TrialPanel)
        public void ShowTrial()
        {
            var panel = new TrialPanel { Builder = Builder };
            panel.Closed += () => _asking = null;
            Ask(panel, "Trial");
        }

        void Dismiss()
        {
            if (_asking != null && IsInstanceValid(_asking)) _asking.QueueFree();
            _asking = null;
        }

        public bool Asking => _asking != null && IsInstanceValid(_asking);

        public TrialPanel Trial => _asking as TrialPanel;
    }
}
