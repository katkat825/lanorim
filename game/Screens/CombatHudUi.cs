using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Screens;
using Core.Words;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE COMBAT HUD ON SCREEN (combat_ux.md): the turn-order strip top centre, the action bar with its
    // pips and End Turn bottom centre, the log top left, a line for the preview. It is CombatHud
    // (content/Screens) drawn; it decides nothing.
    //
    // 2026-09-28 (cc_ui_issues_9-25-2026.md): the log sat bottom left over the map, so it is top left;
    // off the hero's turn the bar's panel is hidden rather than left as an empty block.
    //
    // 2026-09-30 (cc_task_table-ui-minis-zoom-damage.md 3): less frame, more board. The panels are the
    // theme's flat HudPanel, flush to their edges; End Turn is the bar's right end rather than a box of its
    // own; an option that can't be taken is left off the bar (HudLayout.HideUnavailable) or shown faded
    // without its number; the log and the pips are sentence case (HudText), and small caps are for
    // headings. Every size is HudLayout's (res://ui/hud_layout.tres), every colour and font the theme's.
    public partial class CombatHudUi : Control
    {
        readonly CombatDirector _director;

        HFlowContainer _strip;
        HFlowContainer _bar;
        Control _barPanel;
        Label _pips;
        Label _preview;
        Label _aim;
        Label _status;
        VBoxContainer _log;
        Button _logTitle;
        Button _endTurn;
        bool _logOpen;

        readonly List<string> _lines = new List<string>();

        static HudLayout Layout => HudLayout.Current;

        public CombatHudUi(CombatDirector director) => _director = director;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            _logOpen = GameState.Settings.LogOpen;

            BuildStrip();
            BuildBar();
            BuildLog();

            // a window resized live: the strip and the bar wrap to the new width
            GetViewport().SizeChanged += Wrap;
        }

        public override void _ExitTree()
        {
            if (GetViewport() != null) GetViewport().SizeChanged -= Wrap;
        }

        // the room the strip has between the two top corners; the bar, at the bottom, has the screen's
        // width. Both wrap past it rather than run off the screen (check-layout holds them to it)
        float Between => GetViewportRect().Size.X - 2 * Layout.EdgeMargin - Layout.LogWidth - Layout.HintWidth
                         - 4 * Layout.BarGap - 2 * PanelPadding;

        float Across => GetViewportRect().Size.X - 2 * Layout.EdgeMargin - 2 * PanelPadding;

        const float PanelPadding = 24;

        void Wrap()
        {
            if (_strip != null) Ui.Fit(_strip, Between);
            if (_bar != null) Ui.Fit(_bar, Across - Layout.EndTurnMinWidth - 6 * Layout.BarGap);
        }

        // top centre, flush to the top edge
        void BuildStrip()
        {
            _strip = Ui.Flow(Layout.BarGap * 2);
            _status = new Label { ThemeTypeVariation = "HudLabel", HorizontalAlignment = HorizontalAlignment.Center };

            // between the log's corner and the hint's, so none of the three can overlap
            var top = new CenterContainer
            {
                AnchorRight = 1,
                OffsetLeft = Layout.EdgeMargin + Layout.LogWidth + 2 * Layout.BarGap,
                OffsetRight = -(Layout.EdgeMargin + Layout.HintWidth + 2 * Layout.BarGap),
                OffsetTop = Layout.EdgeMargin,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            top.AddChild(Ui.Panel(Ui.Column(2, _strip, _status), "HudPanel"));
            AddChild(top);
        }

        // bottom centre, flush to the bottom edge: the options, End Turn at the right end, the pips under
        void BuildBar()
        {
            _bar = Ui.Flow(Layout.BarGap);

            _endTurn = Ui.Button(CombatHud.EndTurnKey, () => _director.EndTurn());
            _endTurn.CustomMinimumSize = new Vector2(Layout.EndTurnMinWidth, 0);

            _pips = new Label { ThemeTypeVariation = "HudText", HorizontalAlignment = HorizontalAlignment.Center };
            _preview = new Label { ThemeTypeVariation = "HudText", HorizontalAlignment = HorizontalAlignment.Center };
            _aim = new Label { ThemeTypeVariation = "HudText", HorizontalAlignment = HorizontalAlignment.Center, Visible = false };

            // the options take the room; a gap of their own keeps End Turn apart from the last of them
            var row = Ui.Row(Layout.BarGap * 3, _bar, new VSeparator(), _endTurn);

            var bottom = new VBoxContainer
            {
                AnchorTop = 1,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetBottom = -Layout.EdgeMargin,
                GrowVertical = GrowDirection.Begin,
                Alignment = BoxContainer.AlignmentMode.End,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            bottom.AddThemeConstantOverride("separation", 4);
            bottom.AddChild(_aim);
            bottom.AddChild(_preview);

            var centre = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
            centre.AddChild(_barPanel = Ui.Panel(Ui.Column(4, row, _pips), "HudPanel"));
            bottom.AddChild(centre);

            _barPanel.Visible = false;
            AddChild(bottom);
        }

        // top left, flush to the corner, clear of the board the camera centres. The title opens and
        // closes it (so does L), and looks like the heading it is rather than a button
        void BuildLog()
        {
            _log = Ui.Column(2);

            _logTitle = Ui.Button("", ToggleLog, true);
            _logTitle.ThemeTypeVariation = "HudHeading";
            _logTitle.Alignment = HorizontalAlignment.Left;
            _logTitle.FocusMode = FocusModeEnum.None;

            var logPanel = new MarginContainer
            {
                OffsetLeft = Layout.EdgeMargin,
                OffsetTop = Layout.EdgeMargin,
                OffsetRight = Layout.EdgeMargin + Layout.LogWidth,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            logPanel.AddChild(Ui.Panel(Ui.Column(2, _logTitle, _log), "HudPanel"));
            AddChild(logPanel);

            DrawLog();
        }

        void ToggleLog()
        {
            _logOpen = !_logOpen;
            GameState.Settings.LogOpen = _logOpen;
            GameState.SaveSettings();
            DrawLog();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Visible && @event is InputEventKey { Pressed: true, Echo: false } &&
                @event.IsActionPressed(Game.Access.Act.ToggleLog.Id()))
            {
                ToggleLog();
                GetViewport().SetInputAsHandled();
            }
        }

        // a new fight's log starts empty; the last fight's lines were read when it ended
        public void ClearLog()
        {
            _lines.Clear();
            DrawLog();
        }

        public void Write(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            _lines.Add(line);
            DrawLog();
        }

        void DrawLog()
        {
            if (_log == null) return;

            _logTitle.Text = Ui.Say(CombatHud.LogKey) + (_logOpen ? "  ▾" : "  ▸");

            Ui.Clear(_log);

            int shown = _logOpen ? Layout.LogLinesOpen : Layout.LogLinesClosed;

            foreach (string line in _lines.Skip(System.Math.Max(0, _lines.Count - shown)))
                _log.AddChild(new Label { Text = line, ThemeTypeVariation = "HudText", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        }

        public void Preview(string words)
        {
            if (_preview != null) _preview.Text = words ?? "";
        }

        // --- the turn-order strip --------------------------------------------------------------------

        public void RefreshStrip()
        {
            CombatSession session = _director.Session;

            if (session == null || _strip == null) return;

            Ui.Clear(_strip);

            var hud = new CombatHud(session);

            foreach (TurnChip chip in hud.Order) _strip.AddChild(Chip(chip));

            Ui.Fit(_strip, Between);

            // whose turn it is, not whether the bar is up: while the hero's own dice are on the tray the
            // bar is down, and the strip said "Enemy turn"
            _status.Text = Ui.Say(CombatHud.RoundKey, hud.Round) + "   " +
                           (hud.HerosTurn ? Ui.Say(ScreenWords.YourTurn) : Ui.Say(CombatHud.EnemyTurnKey));
        }

        // a name and a health bar, the hero's and a foe's alike; the hero's numbers are the player's to
        // read beside it (combat_ux.md: a foe's health is a bar only)
        Control Chip(TurnChip chip)
        {
            string name = chip.Hero ? chip.Name : chip.NameKey != null ? Ui.Say(chip.NameKey) : chip.Actor.Id;

            var one = Ui.Row(6,
                new Label { Text = (chip.Current ? "▸ " : "") + name, ThemeTypeVariation = "HudLabel" },
                new ProgressBar
                {
                    MinValue = 0,
                    MaxValue = System.Math.Max(1, chip.MaxHitPoints),
                    Value = System.Math.Max(0, chip.HitPoints),
                    ShowPercentage = false,
                    CustomMinimumSize = Layout.HealthBarSize,
                    SizeFlagsVertical = SizeFlags.ShrinkCenter,
                },
                chip.Hero ? new Label { Text = $"{chip.HitPoints}/{chip.MaxHitPoints}", ThemeTypeVariation = "HudText" } : null);

            one.Modulate = chip.Down ? new Color(1, 1, 1, 0.4f) : Colors.White;

            return one;
        }

        // --- the action bar -----------------------------------------------------------------------------

        static readonly Color Chosen = new Color(1.2f, 1.1f, 0.8f);

        // the number is only on an option you can take now: a greyed one has none to press
        static string Label(ActionOption o) => (o.Hotkey > 0 && o.Enabled ? o.Hotkey + " " : "") + Ui.Say(o.NameKey);

        static bool IsSelected(CombatSession session, ActionOption o) =>
            ReferenceEquals(session.Selected, o) || session.Selected?.Id == o.Id;

        // a menu of options on the bar - the spells, the manoeuvres. A greyed one stays in it, disabled,
        // with its reason after its name: a menu is where you look for what exists
        void AddMenu(CombatSession session, string key, IReadOnlyList<ActionOption> options)
        {
            if (options.Count == 0) return;

            var menu = new MenuButton { Text = Ui.Say(key) + " ▾", Flat = false, FocusMode = FocusModeEnum.All };
            PopupMenu popup = menu.GetPopup();

            for (int i = 0; i < options.Count; i++)
            {
                ActionOption o = options[i];

                // a greyed one says why in its own words, not on hover (cc_task_ui-issues-9-30.md 3.1)
                popup.AddItem(!o.Enabled && o.WhyNotKey != null ? $"{Label(o)}  ({Ui.Say(o.WhyNotKey)})" : Label(o), i);
                popup.SetItemDisabled(i, !o.Enabled);

                if (IsSelected(session, o)) menu.Modulate = Chosen;
            }

            popup.IdPressed += id => _director.Pick(options[(int)id]);

            _bar.AddChild(menu);
        }

        Button OptionButton(CombatSession session, ActionOption o)
        {
            Button button = Ui.Button(Label(o), () => _director.Pick(o), true).Greyed(!o.Enabled, o.WhyNotKey);
            button.ThemeTypeVariation = "HudButton";

            if (!o.Enabled) button.Modulate = new Color(1, 1, 1, Layout.UnavailableAlpha);
            else if (IsSelected(session, o)) button.Modulate = Chosen;

            return button;
        }

        public void Refresh()
        {
            CombatSession session = _director.Session;

            if (session == null || _bar == null) return;

            RefreshStrip();

            Ui.Clear(_bar);

            var hud = new CombatHud(session);
            bool can = _director.CanAct;

            if (can)
            {
                // what can't be taken now is off the bar, and the pips say why (no action left); or, with
                // HideUnavailable off, on it faded and without its number
                foreach (ActionOption option in hud.Buttons.Where(o => o.Enabled || !Layout.HideUnavailable))
                    _bar.AddChild(OptionButton(session, option));

                AddMenu(session, CombatHud.SpellsMenuKey, hud.Spells);
                AddMenu(session, CombatHud.ManoeuvresMenuKey, hud.Manoeuvres);

                // aiming a spell that can go on the hero: cast it on yourself without finding your mini
                if (_director.CanAimAtSelf)
                {
                    Button self = Ui.Button(Ui.Say(ScreenWords.OnYourself), _director.OnYourself, true);
                    self.ThemeTypeVariation = "HudButton";
                    self.Modulate = Chosen;
                    _bar.AddChild(self);
                    _bar.MoveChild(self, 0);
                }
            }

            _pips.Text = can
                ? $"{Ui.Say(CombatHud.ActionsKey)} {CombatHud.Pips(hud.Actions, hud.ActionsGiven)}    " +
                  $"{Ui.Say(CombatHud.BonusKey)} {CombatHud.Pips(hud.BonusActions, hud.BonusActionsGiven)}    " +
                  $"{Ui.Say(CombatHud.ReactionKey)} {CombatHud.Pips(hud.Reactions, hud.ReactionsGiven)}    " +
                  Ui.Say(CombatHud.MovementKey, hud.SquaresMoved, hud.SquaresGiven)
                : "";

            // aiming a line or a cone: what turns it, what zooms, what casts it
            bool aiming = can && session.Selected?.Targeting == Targeting.Direction;
            _aim.Visible = aiming;

            if (aiming)
                _aim.Text = Ui.Say(ScreenWords.AimHint, Game.Access.Keyboard.Named("turn_left"),
                                   Game.Access.Keyboard.Named("turn_right"), Game.Access.Keyboard.Named("touch"));

            Wrap();
            _barPanel.Visible = can;
            _endTurn.Disabled = !can;

            if (can) Ui.FocusFirst(_bar);
        }
    }
}
