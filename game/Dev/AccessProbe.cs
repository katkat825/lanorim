using System.Collections.Generic;
using Game.Access;
using Game.Play;
using Godot;

namespace Game.Dev
{
    // `--access-probe`: on the table, once there is something to reach, press F2, F1 twice, Tab twice, Shift+Tab and
    // F3 twice - through Godot's own input, as a keyboard would - and say what came of each. Prints
    // "access  check passed" or "access  FAILED ..." and quits (checks/check-controls.ps1). F3 is pressed twice so
    // the player's own read-aloud setting ends as it began. Developer diagnostics, not localized.
    //
    // Was a partial of AccessDesk (AccessDesk.Probe.cs); a child node of the desk since cc_task_j Part 2, reading it
    // through what it shows (Toast, HelpShowing, Reached, Narrator). The desk is an autoload whose process mode is
    // Always, and this inherits it
    public partial class AccessProbe : Node
    {
        public AccessDesk Desk { get; set; }

        int _step;
        double _wait;
        string _first;
        readonly List<string> _failures = new List<string>();

        public override void _Process(double delta)
        {
            if (AccessDesk.Reaching?.Invoke() is not { Count: > 1 }) return;

            if ((_wait -= delta) > 0) return;
            _wait = 0.3;

            switch (_step++)
            {
                case 0:
                    Press(Key.F2);
                    Check("F2 where are we", Desk.Toast.Length > 0, Desk.Toast);
                    break;

                case 1:
                    Press(Key.F1);
                    Check("F1 help opens", Desk.HelpShowing, Desk.Narrator.Last);
                    break;

                case 2:
                    Press(Key.F1);
                    Check("F1 help closes", !Desk.HelpShowing, "");
                    break;

                case 3:
                    Press(Key.Tab);
                    _first = Desk.Reached?.Says;
                    Check("Tab reaches something", _first != null && Desk.Narrator.Last == _first, _first);
                    break;

                case 4:
                    Press(Key.Tab);
                    Check("Tab reaches the next", Desk.Reached != null && Desk.Reached.Says != _first, Desk.Reached?.Says);
                    break;

                case 5:
                    Press(Key.Tab, shift: true);
                    Check("Shift+Tab reaches back", Desk.Reached?.Says == _first, Desk.Reached?.Says);
                    break;

                case 6:
                    bool was = GameState.Settings.ReadAloud;
                    Press(Key.F3);
                    bool flipped = GameState.Settings.ReadAloud != was && Desk.Narrator.Aloud == GameState.Settings.ReadAloud;
                    Press(Key.F3);
                    Check("F3 reads aloud, and back", flipped && GameState.Settings.ReadAloud == was, Desk.Narrator.Last);
                    break;

                default:
                    GD.Print(_failures.Count == 0
                        ? "access  check passed"
                        : "access  FAILED - " + string.Join("; ", _failures));
                    SetProcess(false);
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
