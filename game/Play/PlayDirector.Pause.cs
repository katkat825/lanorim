using Content.Saves;
using Content.Screens;
using Game.Screens;
using Godot;

namespace Game.Play
{
    public partial class PlayDirector
    {
        // --- the pause menu -------------------------------------------------------------------------

        void Pause()
        {
            bool idle = !_rules.Busy;

            Open(new MenuCard(ScreenWords.PauseTitle, null, true,
                (ScreenWords.Resume, () => _overlay?.Close(), true),
                (ScreenWords.Save, SaveNow, idle),
                (ScreenWords.Sheet, () => Swap(new SheetScreen(Content.Sheet.SheetView.Of(Run.Hero))), idle),
                (ScreenWords.Pack, () => Swap(new PackScreen(new PackView(Run.Hero, Run.Items), GameState.Resolver)), idle),
                (CampaignBook.SettingsKey, () => Swap(SettingsCard()), true),
                (ScreenWords.ToTheBook, ToTheBook, true)), Refresh);
        }

        void Swap(Overlay next)
        {
            Overlay was = _overlay;
            _overlay = null;
            was?.QueueFree();
            Open(next, Refresh);
        }

        Overlay SettingsCard()
        {
            var card = new SettingsOverlay();
            return card;
        }

        void SaveNow()
        {
            string path = Run.Save(SaveKind.Manual);
            GD.Print("play    saved " + path);
            _overlay?.Close();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // a click on the tray throws what is waiting on it, the way Space does
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click &&
                _dice.Waiting != null && !_dice.Thrown && Table.Tray.Under(Table.Camera, click.Position))
            {
                _dice.Go();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

            // Space: throw what the tray is waiting on; otherwise the fight's "go on"
            if (key.IsActionPressed("throw_dice"))
            {
                if (_dice.Waiting != null && !_dice.Thrown)
                {
                    _dice.Go();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (_overlay == null && _combat.GoOn())
                {
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // never the table's demonstration throw while a campaign is played
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.IsActionPressed("ui_cancel") && _overlay == null)
            {
                Pause();
                GetViewport().SetInputAsHandled();
            }
        }

        public override void _Process(double delta)
        {
            AgeVerdict();

            // a throw asked for while the tray was still settling the last one goes as soon as it can
            if (_auto && _dice.Waiting != null && !_dice.Thrown) _dice.Go();

            if (_auto)
            {
                _clock += delta;

                // every half minute, where it is - a stalled headless run says what it waits on
                if ((int)(_clock / 30) != (int)((_clock - delta) / 30))
                    GD.Print($"play    {_clock:0}s: {Run.Now}, rules {(_rules.Busy ? "busy" : "idle")}, " +
                             $"dice {(_dice.Waiting == null ? "-" : _dice.Thrown ? "thrown" : "waiting")}, " +
                             $"tray {(Table.Tray.IsThrowing ? "throwing" : "still")}, {_combat.Describe()}");

                if (_clock > 900)
                {
                    GD.PrintErr($"play    FAILED - still going after {_clock:0} s, at {Run}");
                    Quit(1);
                }
            }

            if (_pauseOnTurn && _overlay == null && _combat.CanAct)
            {
                _pauseOnTurn = false;
                Pause();
            }

            if (_rules.Failed != null && !_failed)
            {
                _failed = true;
                GD.PrintErr("play    FAILED - " + _rules.Failed);
                if (_auto) Quit(1);
            }
        }
    }
}
