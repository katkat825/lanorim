using System;
using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // THE MAP BUILDER'S SCREEN (cc_task_f Part 2; cc_task_l; cc_task_m2 Part 8, reorganised so nothing covers the map).
    // A thin MENU BAR along the top - File, Edit, View - with where the map lives and its name, and the problems folded
    // to a button with their count; a BAR along the bottom like the combat HUD's - Select, Terrain, Spawn, Buildings,
    // Walls and doors, Props, Leave (MapBuilderUi.Bars.cs); and above the bar, only while it's wanted, a picker of
    // buildings or props, the walls-and-doors pop-up or the problems (MapBuilderUi.Pickers.cs), and the questions over
    // the middle (MapBuilderUi.Files.cs, TrialPanel). Every menu item and bar button is also a key (MapBuilder.Do).
    // Built plain from the Ui helpers and the theme, every number in BuilderLayout: how it looks is Kathleen's (#5)
    public partial class MapBuilderUi : Control
    {
        public MapBuilder Builder { get; set; }

        MapEditor Editor => Builder.Editor;

        Label _title;
        Label _notice;
        Control _asking;

        // what each button checks to show itself pressed or greyed
        readonly List<Action> _updates = new();

        double _noticeLeft;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            AddChild(TopBar());
            AddChild(BottomBar());

            BuilderLayout layout = BuilderLayout.Current;

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
                OffsetTop = -layout.BottomBarHeight - layout.EdgeMargin - 80,
                OffsetBottom = -layout.BottomBarHeight - layout.EdgeMargin,
                Visible = false,
            };
            AddChild(_notice);

            Refresh();
        }

        // --- the whole screen, again --------------------------------------------------------------------------------

        // "My maps / Mill Yard •": where it lives, its name, and a dot while there are changes not saved
        public string Title =>
            $"{Ui.Say(Builder.View.Place.NameKey ?? Builder.View.Place.Id ?? "")} / {Builder.View.Name}{(Editor.Unsaved ? " •" : "")}";

        public void Refresh()
        {
            _title.Text = Title;

            Problems();
            Context();

            foreach (Action update in _updates.Concat(_openUpdates)) update();
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
            Close();

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

        // "this map has changes that aren't saved": leave anyway, or stay. `toMainMenu` says where leaving goes
        public void AskToLeave(bool toMainMenu = false) =>
            Ask(Ui.Panel(Ui.Column(BuilderLayout.Current.Gap,
                Ui.Label(MapBuilderView.UnsavedKey),
                Ui.Row(BuilderLayout.Current.Gap,
                    Ui.Button(MapBuilderView.SaveKey, () => { Builder.Save(); Dismiss(); }),
                    Ui.Button(MapBuilderView.LeaveAnywayKey, () => Builder.Leave(anyway: true, toMainMenu)),
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

        // ESCAPE'S SECOND STEP (MapEditor.Escape): a question, a picker or a pop-up is open
        public bool AnythingOpen => Asking || Open != null;

        // and closes it
        public void CloseWhatsOpen()
        {
            if (Asking)
            {
                Dismiss();
                return;
            }

            Close();
        }
    }
}
