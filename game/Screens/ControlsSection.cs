using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Core.Words;
using Game.Access;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE CONTROLS, ON THE SETTINGS PAGE (cc_task_table-ui-minis-zoom-damage.md 2b). Every act from the
    // InputMap with the key it is on now - read back from the InputMap, so a rebinding shows - and a
    // Change button that listens for the next key (Bindings' arm-then-press: Esc lets go, a key another
    // act has is refused and said). Then the keys that are not acts, and the mouse and trackpad.
    //
    // Each row is one line of text that says what it does and what does it, so the page reads aloud
    // the way the rest of it does; the Change button carries its row's words as its accessible name.
    public partial class ControlsSection : VBoxContainer
    {
        static string K(string thing) => KeyConventions.Key(KeyConventions.UiNs, "controls", thing);

        public static readonly string TitleKey = K("title");
        public static readonly string KeyboardKey = K("keyboard");
        public static readonly string MouseKey = K("mouse");
        public static readonly string ChangeKey = K("change");
        public static readonly string TakenKey = K("taken");
        public static readonly string ReservedKey = K("reserved");
        public static readonly string NotYetKey = K("not_yet");
        public static readonly string HotkeysKey = K("hotkeys");
        public static readonly string GoOnKey = K("go_on");
        public static readonly string CancelKey = K("cancel");
        public static readonly string MousePickKey = K("mouse_pick");
        public static readonly string MouseCancelKey = K("mouse_cancel");
        public static readonly string MouseWheelKey = K("mouse_wheel");
        public static readonly string MousePinchKey = K("mouse_pinch");
        public static readonly string MouseHoverKey = K("mouse_hover");
        public static readonly string MouseTrayKey = K("mouse_tray");
        public static readonly string BeginKey = K("begin");

        public static IEnumerable<string> Keys() => new[]
        {
            TitleKey, KeyboardKey, MouseKey, ChangeKey, TakenKey, ReservedKey, NotYetKey, HotkeysKey, GoOnKey,
            CancelKey, MousePickKey, MouseCancelKey, MouseWheelKey, MousePinchKey, MouseHoverKey, MouseTrayKey, BeginKey,
        };

        // THE ACTS NOTHING IN LANORIM ANSWERS YET. They came over from the old build's Access layer and are
        // in the InputMap, but no screen here handles them (Tab moves the focus because it is Godot's own
        // ui_focus_next, not because reach_next does). Listed, and said to be not working, rather than
        // promised. Take one off this list when something answers it
        static readonly HashSet<Act> Unanswered = new()
        {
            Act.ReachNext, Act.ReachBack, Act.Help, Act.WhereAreWe, Act.ReadAloud,
        };

        // keys the table uses outside the acts, which a rebinding may not take
        static bool Reserved(Key key) => key is >= Key.Key1 and <= Key.Key9;

        // what the page listed, for the controls check: each act's row and the keys it names
        public IReadOnlyDictionary<Act, string> ActKeys => _actKeys;

        public IReadOnlyList<string> Lines => _lines;

        readonly Dictionary<Act, string> _actKeys = new();
        readonly Dictionary<Act, Button> _changes = new();
        readonly List<string> _lines = new();
        string _said = "";

        static Bindings Bindings => GameState.Access.Keys;

        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 6);
            Redraw();
        }

        void Redraw()
        {
            Ui.Clear(this);
            _actKeys.Clear();
            _changes.Clear();
            _lines.Clear();

            AddChild(Ui.Title(TitleKey));
            AddChild(Ui.Label(KeyboardKey));

            foreach (Act act in Enum.GetValues<Act>()) AddChild(ActRow(act));

            AddChild(Line(Ui.Say(HotkeysKey)));
            AddChild(Line(Ui.Say(GoOnKey, Keyboard.Named(Act.ThrowDice.Id()))));
            AddChild(Line(Ui.Say(CancelKey, Keyboard.Named("ui_cancel"))));
            AddChild(Line(Ui.Say(BeginKey, string.Join(" / ", Keyboard.Keys("ui_accept").Distinct()))));

            if (_said != "") AddChild(new Label { Text = _said, AutowrapMode = TextServer.AutowrapMode.WordSmart });

            AddChild(Ui.Label(MouseKey));

            foreach (string key in new[] { MousePickKey, MouseCancelKey, MouseWheelKey, MousePinchKey, MouseHoverKey, MouseTrayKey })
                AddChild(Line(Ui.Say(key)));
        }

        Control Line(string words)
        {
            _lines.Add(words);
            return new Label { Text = words, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        }

        Control ActRow(Act act)
        {
            bool listening = Bindings.Armed == act;

            string keys = listening ? Ui.Say(Acts.Waiting) : string.Join(" / ", Keyboard.Keys(act.Id()));
            string words = Ui.Say(act.NameKey(), keys) + (Unanswered.Contains(act) ? " " + Ui.Say(NotYetKey) : "");

            _actKeys[act] = listening ? "" : keys;
            _lines.Add(words);

            var said = new Label
            {
                Text = words,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };

            Button change = Ui.Button(ChangeKey, () => Listen(act));
            change.AccessibilityName = words + ". " + change.Text;
            _changes[act] = change;

            return Ui.Row(12, said, change);
        }

        // what Change does: the row waits for the next key
        public void Listen(Act act)
        {
            _said = "";
            Bindings.Arm(act);
            Redraw();
            Ui.FocusLater(_changes[act]);
        }

        // the next key, whatever it is, before any screen or the table can take it as a command
        public override void _Input(InputEvent @event)
        {
            if (Bindings.Armed is not { } act) return;

            if (!Keyboard.Raw(@event, out Key key, out bool shift)) return;

            GetViewport().SetInputAsHandled();

            if (Reserved(key))
            {
                _said = Ui.Say(ReservedKey, Keyboard.Label(key));
                Bindings.Disarm();
                Redraw();
                return;
            }

            switch (Bindings.Pressed(key, shift))
            {
                case Bindings.Took.Bound:
                    Keyboard.Install(Bindings);
                    GameState.SaveAccess();
                    _said = "";
                    break;

                case Bindings.Took.Taken:
                    _said = Ui.Say(TakenKey, Keyboard.Label(key));
                    Bindings.Disarm();
                    break;

                default:
                    _said = "";
                    break;
            }

            Redraw();

            // the row keeps the focus, so a keyboard player can go straight on to the next
            Ui.FocusLater(_changes[act]);
        }

        public override void _ExitTree() => Bindings.Disarm();
    }
}
