using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Screens;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE COMBAT HUD ON SCREEN (combat_ux.md): the turn-order strip top centre, the action bar and
    // pips bottom centre, End Turn bottom right, the log bottom left, a line for the preview. It is
    // CombatHud (content/Screens) drawn; it decides nothing.
    public partial class CombatHudUi : Control
    {
        readonly CombatDirector _director;

        HBoxContainer _strip;
        HBoxContainer _bar;
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

            _bar = Ui.Row(6);
            _pips = new Label { ThemeTypeVariation = "HudLabel", HorizontalAlignment = HorizontalAlignment.Center };
            _preview = new Label { ThemeTypeVariation = "HudLabel", HorizontalAlignment = HorizontalAlignment.Center };

            var bottom = new CenterContainer
            {
                AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1, OffsetTop = -150, OffsetBottom = -12,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            bottom.AddChild(Ui.Column(4, _preview, Ui.Panel(Ui.Column(4, _bar, _pips), "DarkPanel")));
            AddChild(bottom);

            _endTurn = Ui.Button(CombatHud.EndTurnKey, () => _director.EndTurn());
            var corner = new MarginContainer
            {
                AnchorLeft = 1, AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1,
                OffsetLeft = -200, OffsetTop = -80, OffsetRight = -16, OffsetBottom = -16,
            };
            corner.AddChild(_endTurn);
            AddChild(corner);

            _log = Ui.Column(2);
            // bottom left, above the action bar's row so the two never overlap
            var logPanel = new MarginContainer
            {
                AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 16, OffsetTop = -330, OffsetRight = 340, OffsetBottom = -150,
                MouseFilter = MouseFilterEnum.Ignore,
                GrowVertical = GrowDirection.Begin,
            };
            logPanel.AddChild(Ui.Panel(Ui.Column(4, Ui.Button(CombatHud.LogKey, ToggleLog), _log), "DarkPanel"));
            AddChild(logPanel);
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
                        MinValue = 0, MaxValue = System.Math.Max(1, chip.MaxHitPoints), Value = System.Math.Max(0, chip.HitPoints),
                        ShowPercentage = false, CustomMinimumSize = new Vector2(48, 12),
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

            _endTurn.Disabled = !can;

            if (can) Ui.FocusFirst(_bar);
        }
    }
}
