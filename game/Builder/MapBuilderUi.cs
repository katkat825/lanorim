using System;
using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Core.Space;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // THE MAP BUILDER'S PANELS (cc_task_f Part 2), built plain from the Ui helpers and the theme, every number in
    // BuilderLayout: down the left the file's buttons, the six tools and what the tool in hand lays (ground, wall or
    // door, the prop palette by category and its turn, the spawn's number); down the right the problems, live, each a
    // button that shows where. How it looks is Kathleen's (#5): lay it out as you like
    public partial class MapBuilderUi : Control
    {
        public MapBuilder Builder { get; set; }

        MapEditor Editor => Builder.Editor;

        VBoxContainer _tools;
        VBoxContainer _options;
        VBoxContainer _problems;
        Label _title;
        Label _notice;
        PanelContainer _asking;

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
                _tools = Ui.Column(layout.Gap / 2),
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
            row.AddChild(Ui.Button(MapBuilderView.UndoKey, Builder.Undo));
            row.AddChild(Ui.Button(MapBuilderView.RedoKey, Builder.Redo));
            row.AddChild(Toggle(Ui.Say(MapBuilderView.TopDownKey), () => Builder.ToggleTopDown(), () => Builder.TopDown));
            row.AddChild(Ui.Button(MapBuilderView.LeaveKey, () => Builder.Leave()));

            return row;
        }

        // the six tools, one pressed
        void Tools()
        {
            foreach (MapTool tool in MapEditor.Tools)
            {
                MapTool one = tool;
                _tools.AddChild(Toggle(Ui.Say(MapEditor.ToolKey(one)), () =>
                {
                    Editor.Tool = one;
                    Refresh();
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

        // --- what the tool in hand lays ---------------------------------------------------------------------------

        MapTool? _optionsFor;
        string _categoryShown;

        void Options()
        {
            // rebuilt only when the tool or the palette's category changes, so a press isn't lost to a rebuild
            if (_optionsFor == Editor.Tool && _categoryShown == Editor.Category) return;

            _optionsFor = Editor.Tool;
            _categoryShown = Editor.Category;
            _optionUpdates.Clear();

            Ui.Clear(_options);

            switch (Editor.Tool)
            {
                case MapTool.Paint:
                    foreach (Tile ground in MapBuilderView.Grounds)
                        Option(MapBuilderView.GroundKey(ground), () => Editor.Ground = ground, () => Editor.Ground == ground);
                    break;

                case MapTool.Wall:
                    foreach (Edge line in MapBuilderView.Lines)
                        Option(MapBuilderView.LineKey(line), () => Editor.Line = line, () => Editor.Line == line);
                    break;

                case MapTool.Spawn:
                    foreach (int slot in MapBuilderView.SpawnSlots)
                        Option(MapBuilderView.SpawnKey, () => Editor.SpawnSlot = slot, () => Editor.SpawnSlot == slot, slot);
                    break;

                case MapTool.Prop:
                    Palette();
                    break;
            }
        }

        void Option(string key, Action pick, Func<bool> chosen, params object[] args) =>
            _options.AddChild(Pressable(Ui.Say(key, args), pick, chosen));

        // a button on the options page that stays pressed while `chosen` is true
        Button Pressable(string words, Action pick, Func<bool> chosen)
        {
            Button button = Ui.Button(words, () => { pick(); Refresh(); }, true);
            button.ToggleMode = true;
            _optionUpdates.Add(() => button.SetPressedNoSignal(chosen()));
            return button;
        }

        // the palette: its categories, then its props, then the turn
        void Palette()
        {
            HFlowContainer categories = Ui.Flow(BuilderLayout.Current.Gap / 2);
            _options.AddChild(categories);

            categories.AddChild(Category(null, MapBuilderView.AllPropsKey));

            foreach (string category in Editor.Palette.Categories)
                categories.AddChild(Category(category, PropCatalogue.CategoryKey(category)));

            VBoxContainer props = Ui.Column(2);

            foreach (PropEntry prop in Editor.Showing)
            {
                string id = prop.Id;
                props.AddChild(Pressable(Ui.Say(prop.NameKey), () => Editor.Choose(id), () => Editor.PropId == id));
            }

            _options.AddChild(Ui.Scroll(props, BuilderLayout.Current.PaletteHeight));

            // the turn, in degrees: a number, not a word
            Label turn = Ui.Plain("");
            _optionUpdates.Add(() => turn.Text = $"{Editor.Turn * 90}°");
            _options.AddChild(Ui.Row(BuilderLayout.Current.Gap, Ui.Button(MapBuilderView.RotateKey, () => Builder.Rotate(1)), turn));
        }

        Button Category(string category, string key) =>
            Pressable(Ui.Say(key), () => Editor.Browse(category), () => Editor.Category == category);

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

        public void Refresh()
        {
            Options();
            Problems();

            _title.Text = $"{Builder.View.Id}{(Editor.Unsaved ? " •" : "")}";

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

        // "this map has changes that aren't saved": leave anyway, or stay
        public void AskToLeave()
        {
            _asking?.QueueFree();

            _asking = Ui.Panel(Ui.Column(BuilderLayout.Current.Gap,
                Ui.Label(MapBuilderView.UnsavedKey),
                Ui.Row(BuilderLayout.Current.Gap,
                    Ui.Button(MapBuilderView.SaveKey, () => { Builder.Save(); Dismiss(); }),
                    Ui.Button(MapBuilderView.LeaveAnywayKey, () => Builder.Leave(anyway: true)),
                    Ui.Button(MapBuilderView.StayKey, Dismiss))));

            _asking.Name = "Unsaved";
            _asking.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            AddChild(_asking);
            Ui.FocusLater(_asking);
        }

        void Dismiss()
        {
            _asking?.QueueFree();
            _asking = null;
        }

        public bool Asking => _asking != null;
    }
}
