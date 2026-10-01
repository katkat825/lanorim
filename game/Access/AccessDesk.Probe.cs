using System.Collections.Generic;
using Game.Play;
using Godot;

namespace Game.Access
{
    public partial class AccessDesk
    {
        // `--access-probe`: on the table, once there is something to reach, press F2, F1 twice, Tab twice, Shift+Tab and
        // F3 twice - through Godot's own input, as a keyboard would - and say what came of each. Prints
        // "access  check passed" or "access  FAILED ..." and quits (checks/check-controls.ps1). F3 is pressed twice so
        // the player's own read-aloud setting ends as it began. Developer diagnostics, not localized
        public const string Probe = "--access-probe";

        bool _probing;
        int _step;
        double _wait;
        string _first;
        readonly List<string> _failures = new List<string>();

        void Probing(double delta)
        {
            if (Reaching?.Invoke() is not { Count: > 1 }) return;

            if ((_wait -= delta) > 0) return;
            _wait = 0.3;

            switch (_step++)
            {
                case 0:
                    Press(Key.F2);
                    Check("F2 where are we", _toast.Visible && _toast.Text.Length > 0, _toast.Text);
                    break;

                case 1:
                    Press(Key.F1);
                    Check("F1 help opens", HelpShowing, Narrator.Last);
                    break;

                case 2:
                    Press(Key.F1);
                    Check("F1 help closes", !HelpShowing, "");
                    break;

                case 3:
                    Press(Key.Tab);
                    _first = _on?.Says;
                    Check("Tab reaches something", _first != null && Narrator.Last == _first, _first);
                    break;

                case 4:
                    Press(Key.Tab);
                    Check("Tab reaches the next", _on != null && _on.Says != _first, _on?.Says);
                    break;

                case 5:
                    Press(Key.Tab, shift: true);
                    Check("Shift+Tab reaches back", _on?.Says == _first, _on?.Says);
                    break;

                case 6:
                    bool was = GameState.Settings.ReadAloud;
                    Press(Key.F3);
                    bool flipped = GameState.Settings.ReadAloud != was && Narrator.Aloud == GameState.Settings.ReadAloud;
                    Press(Key.F3);
                    Check("F3 reads aloud, and back", flipped && GameState.Settings.ReadAloud == was, Narrator.Last);
                    break;

                default:
                    GD.Print(_failures.Count == 0
                        ? "access  check passed"
                        : "access  FAILED - " + string.Join("; ", _failures));
                    _probing = false;
                    GetTree().Quit(_failures.Count == 0 ? 0 : 1);
                    break;
            }
        }

        static void Press(Key key, bool shift = false)
        {
            foreach (bool down in new[] { true, false })
                Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down, ShiftPressed = shift });

            Input.FlushBufferedEvents();
        }

        void Check(string what, bool ok, string said)
        {
            GD.Print($"access  {what}: {(ok ? "yes" : "NO")} - {said}");

            if (!ok) _failures.Add(what);
        }
    }
}
