using System;
using System.Collections.Generic;
using System.Linq;
using Content.Screens;
using Godot;

namespace Game.Screens
{
    // THE DIALOGUE POPUP (Tier 3b): a card along the bottom of the table - who speaks, the line in the
    // storytelling face, and Continue; or the choices. Reads DialogueView; a press goes back through
    // it to the run (on the rules thread, via the director's hooks, because a Continue can roll dice).
    public partial class DialoguePopup : PanelContainer
    {
        Label _speaker;
        RichTextLabel _line;
        VBoxContainer _buttons;

        public Action Continue { get; set; }

        public Action<int> Choose { get; set; }

        public override void _Ready()
        {
            AnchorLeft = 0.12f;
            AnchorRight = 0.88f;
            AnchorTop = 1f;
            AnchorBottom = 1f;
            OffsetTop = -Ui.Px(176);
            OffsetBottom = -Ui.Px(16);
            GrowVertical = GrowDirection.Begin;

            _speaker = new Label();
            _line = new RichTextLabel
            {
                ThemeTypeVariation = "NarrationLabel",
                FitContent = true,
                BbcodeEnabled = false,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, Ui.Px(48)),
            };
            _buttons = Ui.Column(6);

            AddChild(Ui.Column(8, _speaker, _line, _buttons));
            Visible = false;
        }

        public void Show(DialogueView view)
        {
            if (view == null || !view.Showing)
            {
                Visible = false;
                return;
            }

            string nameKey = view.SpeakerNameKey;

            // a choice line has no line of its own to show; the choices are the card
            string line = view.LineKey == null ? "" : Ui.Say(view.LineKey, view.Substitutions.ToArray());

            Fill(nameKey == null ? null : Ui.Say(nameKey), line, view.CanContinue,
                 view.Choices.Select(c => (Ui.Say(c.Key, c.Substitutions.ToArray()), c.Offered, (Action)(() => Choose?.Invoke(c.Option)))),
                 () => Continue?.Invoke());
        }

        // THE POPUP OVER ANY WORDS (cc_task_o Part 5.1, the story editor's Preview): who speaks (null: the narrator, no
        // plate), the line, and Continue - or the choices, each with what pressing it does. The table's Show is this over
        // a running campaign's DialogueView
        public void Fill(string speaker, string line, bool canContinue, IEnumerable<(string Words, bool Offered, Action Pick)> choices, Action next)
        {
            Visible = true;

            _speaker.Text = speaker ?? "";
            _speaker.Visible = speaker != null;
            _line.Text = line ?? "";

            Ui.Clear(_buttons);

            if (canContinue)
            {
                _buttons.AddChild(Ui.Button(DialogueView.ContinueKey, () => next?.Invoke()));
            }
            else
            {
                foreach ((string words, bool offered, Action pick) in choices)
                {
                    Button button = Ui.Button(words, () => pick?.Invoke(), true);
                    button.Disabled = !offered;
                    _buttons.AddChild(button);
                }
            }

            Ui.FocusFirst(_buttons);
        }
    }
}
