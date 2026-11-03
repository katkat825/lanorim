using System;
using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Screens;
using Content.Sheet;
using Game.Access;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE FULL CHARACTER SHEET (cc_task_cs Part 1; docs/character_sheet_decisions.md), the size of the window, laid out
    // like a printed sheet on the theme's parchment: two sides, as a 1280 x 720 window holds one at a time. The front is
    // who, the basics, the abilities and saves, the skills, the turn and the weapons; the back is the spells and the
    // special features. It replaced the short form (SheetScreen), which nothing else needed.
    //
    // BOUND TO SheetView AND NOTHING ELSE (agreement #9): every number is the view's, every line SheetReading's, so the
    // screen reader says what the paper shows (Read aloud, and on opening when reading is on). Its two buttons are
    // SheetActs': in a fight Swap and End concentration hand the fight's own option to the combat director's Pick, the
    // path the HUD's More actions takes; outside one Swap is the pack's Equip. Opening it spends nothing.
    //
    // The look is Kathleen's: sizes and fonts are the theme's (CardTitle, CardLabel, TitleLabel); the ability boxes'
    // outline is the one style drawn here (Box), a stand-in until the paper is on the table (the run log says what that
    // takes)
    public partial class SheetPage : Overlay
    {
        readonly Func<SheetView> _read;
        readonly Func<SheetActs> _acts;
        readonly Action<ActionOption> _pick;
        readonly Func<bool> _busy;

        SheetView _sheet;
        SheetActs _act;
        bool _back;
        bool _waiting;

        // `pick`: in a fight, the combat director's Pick (the option goes to the rules thread, and the sheet redraws once
        // `busy` says it's done). Outside one both are null
        public SheetPage(Func<SheetView> read, Func<SheetActs> acts, Action<ActionOption> pick = null,
                         Func<bool> busy = null, bool back = false)
        {
            _read = read ?? throw new ArgumentNullException(nameof(read));
            _acts = acts ?? throw new ArgumentNullException(nameof(acts));
            _pick = pick;
            _busy = busy ?? (() => false);
            _back = back;
            FillsTheScreen = true;
        }

        // which side is up (a probe turns it)
        public bool ShowingBack => _back;

        public void Turn(bool back)
        {
            if (_back == back) return;

            _back = back;
            Redraw();
            ReadIfReading();
        }

        public override void _Ready()
        {
            base._Ready();
            Ui.FocusFirst(Body);
            ReadIfReading();
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            // a Swap or an End concentration handed to the rules: drawn again once they've done it
            if (_waiting && !_busy())
            {
                _waiting = false;
                Redraw();
            }
        }

        protected override void Draw()
        {
            _sheet = _read();
            _act = _acts();

            Body.AddChild(Header());

            Control side = _back ? BackSide() : FrontSide();
            side.SizeFlagsVertical = SizeFlags.ExpandFill;
            Body.AddChild(side);
        }

        // the name across the top, and the side to turn to, Read aloud and Close
        Control Header()
        {
            Label name = Ui.Plain(_sheet.Name);
            name.ThemeTypeVariation = "TitleLabel";
            name.AutowrapMode = TextServer.AutowrapMode.Off;
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            Button front = Side(SheetWords.FrontKey, false);
            Button back = Side(SheetWords.BackKey, true);

            return Ui.Row(12, name, front, back,
                          Ui.Button(SheetWords.ReadAloudKey, Read),
                          Ui.Button(SheetWords.CloseKey, Close));
        }

        Button Side(string key, bool back)
        {
            Button side = Ui.Button(key, () => Turn(back));
            side.ToggleMode = true;
            side.SetPressedNoSignal(_back == back);
            return side;
        }

        // --- the pieces both sides are made of --------------------------------------------------------------

        // a section: its heading, then its lines, each a sentence of SheetReading's
        Control Section(SheetReading.Section section, params Control[] after)
        {
            var column = Ui.Column(2, Heading(SheetReading.HeadingKey(section, _sheet)));

            foreach (Said line in SheetReading.Lines(section, _sheet)) column.AddChild(Line(AccessDesk.Words(line)));

            foreach (Control more in after) if (more != null) column.AddChild(more);

            return column;
        }

        static Label Heading(string key)
        {
            Label heading = Ui.Label(key);
            heading.ThemeTypeVariation = "CardTitle";
            return heading;
        }

        static Label Line(string words)
        {
            Label line = Ui.Plain(words);
            line.ThemeTypeVariation = "CardLabel";
            return line;
        }

        // a column of the sheet: an even share of the width, or `share` of them
        static VBoxContainer Column(float share, params Control[] children)
        {
            VBoxContainer column = Ui.Column(18, children);
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsStretchRatio = share;
            return column;
        }

        // a button on the sheet that may be grey, and the reason written under it (no hover text, cc_task_ui-issues-9-30 3.1)
        static Control Act(string words, Action press, bool can, string whyKey)
        {
            Button button = Ui.Button(words, press, true).Greyed(!can, whyKey);
            button.Alignment = HorizontalAlignment.Left;
            return Ui.Column(2, button, Ui.Why(!can, whyKey));
        }

        // a name on the sheet that opens its card
        static Button Opens(string words, Action open)
        {
            Button name = Ui.Button(words, open, true);
            name.Flat = true;
            name.Alignment = HorizontalAlignment.Left;
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            return name;
        }

        // --- the two buttons ---------------------------------------------------------------------------------

        void Swap(string itemId)
        {
            if (_act.InAFight)
            {
                ActionOption option = _act.SwapOption(itemId);

                if (option == null || !option.Enabled || _pick == null) return;

                _pick(option);
                _waiting = true;
                return;
            }

            if (_act.SwapNow(itemId)) Redraw();
        }

        void EndConcentration()
        {
            ActionOption option = _act.EndConcentrationOption;

            if (option == null || !option.Enabled || _pick == null) return;

            _pick(option);
            _waiting = true;
        }

        // --- read aloud ----------------------------------------------------------------------------------------

        // the side that's up, section by section: each heading, then its lines, in the order the paper has them
        public IReadOnlyList<string> Aloud()
        {
            SheetView sheet = _sheet ?? _read();
            var lines = new List<string> { sheet.Name };

            foreach (SheetReading.Section section in _back ? SheetReading.Back(sheet) : SheetReading.Front)
            {
                lines.Add(Ui.Say(SheetReading.HeadingKey(section, sheet)));
                lines.AddRange(SheetReading.Lines(section, sheet).Select(AccessDesk.Words));
            }

            return lines;
        }

        void Read() => AccessDesk.Instance?.Narrator.Say(Spoken.Of(Aloud().ToArray()));

        void ReadIfReading()
        {
            if (GameState.Settings.ReadAloud) Read();
        }
    }
}
