using System;
using System.Collections.Generic;
using System.Linq;
using Content.Screens;
using Core.Words;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Access
{
    // THE FIVE KEYS THAT DID NOTHING (cc_task_open-questions-answers.md 4.2; Kathleen: "build them"). The old build's
    // Access layer came over without its handlers, so Tab/Shift+Tab, F1, F2 and F3 were in the InputMap and on the
    // Controls page as "(not working yet)", and the Narrator was in no scene at all. This desk answers all five, from
    // any screen, and owns the Narrator. It is an autoload (project.godot), so it is there before any screen is.
    //
    //   Tab / Shift+Tab  reach the next / last thing on the table: each piece, each button on the bar, the dice tray.
    //                    What it is reached is said aloud (and printed), and shown: the piece's square lights, the
    //                    button takes the focus. Enter on a piece clicks it, as the mouse would. With nothing on the
    //                    table to reach (the book, a menu), Tab is left to Godot, which walks the focus as it always did
    //   F1               the Help card: the rules in a few lines, and the keys. F1 or Esc closes it
    //   F2               where are we: said, and shown for a few seconds (Content.Screens.Whereabouts)
    //   F3               the reader on and off, kept in the settings (GameSettings.ReadAloud)
    //
    // What there is to reach and where we are come from whichever screen is up: they set Reaching and Where.
    // `--access-probe` presses each key in turn on the table and prints what came of it (checks/check-controls.ps1)
    public partial class AccessDesk : Node
    {
        public static AccessDesk Instance { get; private set; }

        // what Tab can reach right now, nearest first; null or empty when there is nothing (Tab is left to Godot)
        public static Func<IReadOnlyList<Reachable>> Reaching { get; set; }

        // where we are, as whole sentences; null is the book
        public static Func<IReadOnlyList<Said>> Where { get; set; }

        public Narrator Narrator { get; private set; }

        int _at = -1;
        Reachable _on;

        CanvasLayer _layer;
        PanelContainer _help;
        Label _toast;
        double _toastLeft;

        [Export] public float ToastSeconds { get; set; } = 6f;

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;

            Narrator = new Narrator { Name = "Narrator", Aloud = GameState.Settings.ReadAloud };
            AddChild(Narrator);

            _layer = new CanvasLayer { Name = "Access", Layer = 90 };
            AddChild(_layer);

            _toast = new Label
            {
                Name = "WhereAreWe",
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _toast.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
            _toast.CustomMinimumSize = new Vector2(900, 0);
            _toast.Position = new Vector2(-450, 110);
            _layer.AddChild(_toast);

            if (OS.GetCmdlineUserArgs().Contains(Probe)) _probing = true;
        }

        public override void _ExitTree()
        {
            if (Instance == this) Instance = null;
        }

        // before the GUI: Tab is Godot's ui_focus_next too, and the table's reach has to see it first
        public override void _Input(InputEvent @event)
        {
            Act? act = Keyboard.Pressed(@event);

            if (act is null) return;

            if (Answer(act.Value)) GetViewport().SetInputAsHandled();
        }

        // what a key does; true when it was this desk's to do
        public bool Answer(Act act)
        {
            switch (act)
            {
                case Act.ReadAloud:
                    ReadAloud();
                    return true;

                case Act.WhereAreWe:
                    WhereAreWe();
                    return true;

                case Act.Help:
                    Help(!(_help?.Visible ?? false));
                    return true;

                case Act.ReachNext:
                case Act.ReachBack:
                    return Reach(act == Act.ReachNext ? 1 : -1);

                case Act.Touch when _on?.Touch != null && Reaching?.Invoke() is { Count: > 0 }:
                    _on.Touch();
                    return true;

                default:
                    return false;
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (_help?.Visible == true && @event.IsActionPressed("ui_cancel"))
            {
                Help(false);
                GetViewport().SetInputAsHandled();
            }
        }

        // --- F3 ---------------------------------------------------------------------------------------------------

        public void ReadAloud()
        {
            bool on = !GameState.Settings.ReadAloud;

            GameState.Settings.ReadAloud = on;
            GameState.SaveSettings();

            // said with the voice on, so turning it on is heard; turning it off is said before it goes quiet
            if (on) Narrator.Listening(true);
            Say(Ui.Say(on ? AccessWords.ReadingOnKey : AccessWords.ReadingOffKey));
            if (!on) Narrator.Listening(false);
        }

        // --- F2 ---------------------------------------------------------------------------------------------------

        public string WhereAreWe()
        {
            IReadOnlyList<Said> where = Where?.Invoke() ?? Whereabouts.AtTheBook();

            string[] lines = where.Select(Words).ToArray();

            Say(lines);

            _toast.Text = string.Join(" ", lines);
            _toast.Visible = true;
            _toastLeft = ToastSeconds;

            return _toast.Text;
        }

        // a sentence, its arguments said first when they are keys themselves (a campaign's name, a monster's)
        public static string Words(Said said) =>
            Ui.Say(said.Key, said.Args.Select(a => a is string s && Spoken.IsAKey(s) ? Ui.Say(s) : a).ToArray());

        // --- F1 ---------------------------------------------------------------------------------------------------

        public bool HelpShowing => _help?.Visible == true;

        public void Help(bool show)
        {
            if (show) _help = BuildHelp();
            else if (_help != null) _help.Visible = false;

            if (show) Say(new[] { Ui.Say(HelpCard.TitleKey) }.Concat(HelpCard.Lines.Select(k => Ui.Say(k))).ToArray());
        }

        PanelContainer BuildHelp()
        {
            _help?.QueueFree();

            var card = new PanelContainer { Name = "Help" };
            card.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            card.CustomMinimumSize = new Vector2(820, 0);
            card.Position = new Vector2(-410, -330);

            var column = Ui.Column(8, Ui.Title(HelpCard.TitleKey));

            foreach (string line in HelpCard.Lines)
                column.AddChild(Ui.Label(line));

            // the keys, each as the Controls page says it
            foreach (Act act in Enum.GetValues<Act>())
                column.AddChild(Ui.Label(act.NameKey(), string.Join(" / ", Keyboard.Keys(act.Id()))));

            card.AddChild(column);
            _layer.AddChild(card);

            return card;
        }

        // --- Tab / Shift+Tab --------------------------------------------------------------------------------------

        public Reachable Reached => _on;

        bool Reach(int step)
        {
            IReadOnlyList<Reachable> things = Reaching?.Invoke();

            if (things == null || things.Count == 0) return false;

            _at = ((_at < 0 ? (step > 0 ? -1 : 0) : _at) + step + things.Count) % things.Count;
            _on = things[_at];

            _on.Focus?.Invoke();
            Say(_on.Says);

            return true;
        }

        // --- saying -----------------------------------------------------------------------------------------------

        void Say(params string[] lines) => Narrator.Say(Spoken.Of(lines));

        public override void _Process(double delta)
        {
            if (_toastLeft > 0 && (_toastLeft -= delta) <= 0) _toast.Visible = false;

            if (_probing) Probing(delta);
        }
    }
}
