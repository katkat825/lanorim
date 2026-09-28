using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Screens;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE COMBAT HUD ON SCREEN (combat_ux.md): the turn-order strip top centre, the action bar and
    // pips bottom centre, End Turn bottom right, the log top left, a line for the preview. It is
    // CombatHud (content/Screens) drawn; it decides nothing.
    //
    // 2026-09-28 (cc_ui_issues_9-25-2026.md): the log sat bottom left over the map, so it is top left
    // and narrower; the bar wraps onto a second row instead of running under End Turn; and off the
    // hero's turn the bar's panel is hidden rather than left as an empty block.
    public partial class CombatHudUi : Control
    {
        readonly CombatDirector _director;

        HBoxContainer _strip;
        HFlowContainer _bar;
        Control _barPanel;
        Label _pips;
        Label _preview;
        Label _status;
        VBoxContainer _log;
        Button _endTurn;
        bool _logOpen;

        readonly List<string> _lines = new List<string>();

        public CombatHudUi(CombatDirector director) => _director = director;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            _logOpen = GameState.Settings.LogOpen;

            _strip = Ui.Row(10);
            var top = new CenterContainer { AnchorRight = 1, OffsetTop = 12, MouseFilter = MouseFilterEnum.Ignore };
            top.AddChild(Ui.Panel(Ui.Column(4, _strip, _status = new Label { ThemeTypeVariation = "HudLabel", HorizontalAlignment = HorizontalAlignment.Center }), "DarkPanel"));
            AddChild(top);

            // a flow, not a row: nine options and a Rage run past the screen's width, and a row would
            // run them under End Turn
            _bar = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
            _bar.AddThemeConstantOverride("h_separation", 6);
            _bar.AddThemeConstantOverride("v_separation", 6);
            _pips = new Label { ThemeTypeVariation = "HudLabel", HorizontalAlignment = HorizontalAlignment.Center };
            _preview = new Label { ThemeTypeVariation = "HudLabel", HorizontalAlignment = HorizontalAlignment.Center };

            // the width left of End Turn's corner, and grown upwards from the bottom as the bar wraps
            var bottom = new VBoxContainer
            {
                AnchorTop = 1,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetLeft = 16,
                OffsetRight = -EndTurnWidth - 24,
                OffsetBottom = -12,
                GrowVertical = GrowDirection.Begin,
                Alignment = BoxContainer.AlignmentMode.End,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            bottom.AddThemeConstantOverride("separation", 4);
            bottom.AddChild(_preview);
            bottom.AddChild(_barPanel = Ui.Panel(Ui.Column(4, _bar, _pips), "DarkPanel"));
            _barPanel.Visible = false;
            AddChild(bottom);

            _endTurn = Ui.Button(CombatHud.EndTurnKey, () => _director.EndTurn());
            var corner = new MarginContainer
            {
                AnchorLeft = 1,
                AnchorTop = 1,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetLeft = -EndTurnWidth - 16,
                OffsetTop = -80,
                OffsetRight = -16,
                OffsetBottom = -16,
            };
            corner.AddChild(_endTurn);
            AddChild(corner);

            _log = Ui.Column(2);
            // top left, beside the turn strip and clear of the board, which the camera centres
            var logPanel = new MarginContainer
            {
                OffsetLeft = 16,
                OffsetTop = 12,
                OffsetRight = 16 + LogWidth,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            logPanel.AddChild(Ui.Panel(Ui.Column(4, Ui.Button(CombatHud.LogKey, ToggleLog), _log), "DarkPanel"));
            AddChild(logPanel);
        }

        const int EndTurnWidth = 184;

        const int LogWidth = 260;

        void ToggleLog()
        {
            _logOpen = !_logOpen;
            GameState.Settings.LogOpen = _logOpen;
            GameState.SaveSettings();
            DrawLog();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.L })
            {
                ToggleLog();
                GetViewport().SetInputAsHandled();
            }
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

            Ui.Clear(_log);

            foreach (string line in _lines.Skip(System.Math.Max(0, _lines.Count - (_logOpen ? 14 : 3))))
                _log.AddChild(new Label { Text = line, ThemeTypeVariation = "HudLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        }

        public void Preview(string words)
        {
            if (_preview != null) _preview.Text = words ?? "";
        }

        public void RefreshStrip()
        {
            CombatSession session = _director.Session;

            if (session == null || _strip == null) return;

            Ui.Clear(_strip);

            var hud = new CombatHud(session);

            foreach (TurnChip chip in hud.Order)
            {
                string name = chip.Hero ? chip.Name : chip.NameKey != null ? Ui.Say(chip.NameKey) : chip.Actor.Id;

                var label = new Label
                {
                    // the hero's numbers are the player's; a foe's health is a bar only (combat_ux.md)
                    Text = $"{(chip.Current ? "▶ " : "")}{name}{(chip.Hero ? $" {chip.HitPoints}/{chip.MaxHitPoints}" : "")}",
                    ThemeTypeVariation = "HudLabel",
                };

                var one = Ui.Row(4, label);
                one.Modulate = chip.Down ? new Color(1, 1, 1, 0.4f) : Colors.White;

                if (!chip.Hero)
                    one.AddChild(new ProgressBar
                    {
                        MinValue = 0,
                        MaxValue = System.Math.Max(1, chip.MaxHitPoints),
                        Value = System.Math.Max(0, chip.HitPoints),
                        ShowPercentage = false,
                        CustomMinimumSize = new Vector2(48, 12),
                        SizeFlagsVertical = SizeFlags.ShrinkCenter,
                    });

                _strip.AddChild(one);
            }

            _status.Text = Ui.Say(CombatHud.RoundKey, hud.Round) + "   " +
                           (_director.CanAct ? Ui.Say(Game.Screens.ScreenWords.YourTurn) : Ui.Say(CombatHud.EnemyTurnKey));
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
                foreach (ActionOption option in hud.Bar.Where(o => o.Kind != OptionKind.EndTurn))
                {
                    ActionOption o = option;
                    string label = (o.Hotkey > 0 ? o.Hotkey + " " : "") + Ui.Say(o.NameKey);
                    Button button = Ui.Button(label, () => _director.Pick(o), true)
                                      .Greyed(!o.Enabled, o.WhyNotKey);

                    if (ReferenceEquals(session.Selected, o) || session.Selected?.Id == o.Id)
                        button.Modulate = new Color(1.2f, 1.1f, 0.8f);

                    _bar.AddChild(button);
                }
            }

            _pips.Text = can
                ? $"{Ui.Say(CombatHud.ActionsKey)} {new string('●', hud.Actions)}   " +
                  $"{Ui.Say(CombatHud.BonusKey)} {new string('●', hud.BonusActions)}   " +
                  $"{Ui.Say(CombatHud.ReactionKey)} {new string('●', hud.Reactions)}   " +
                  Ui.Say(CombatHud.MovementKey, hud.SquaresLeft)
                : "";

            _barPanel.Visible = can;
            _endTurn.Disabled = !can;

            if (can) Ui.FocusFirst(_bar);
        }
    }
}
