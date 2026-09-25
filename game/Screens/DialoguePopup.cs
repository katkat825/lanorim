using System;
using System.Linq;
using Content.Play;
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
            OffsetTop = -176;
            OffsetBottom = -16;
            GrowVertical = GrowDirection.Begin;

            _speaker = new Label();
            _line = new RichTextLabel
            {
                ThemeTypeVariation = "NarrationLabel",
                FitContent = true,
                BbcodeEnabled = false,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 48),
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

            Visible = true;

            string nameKey = view.SpeakerNameKey;
            _speaker.Text = nameKey == null ? "" : Ui.Say(nameKey);
            _speaker.Visible = nameKey != null;

            _line.Text = view.LineKey == null ? "" : Ui.Say(view.LineKey, view.Substitutions.ToArray());

            Ui.Clear(_buttons);

            if (view.CanContinue)
            {
                _buttons.AddChild(Ui.Button(DialogueView.ContinueKey, () => Continue?.Invoke()));
            }
            else
            {
                // a choice line has no line of its own to show; the choices are the card
                if (view.LineKey == null) _line.Text = "";

                foreach (ChoiceRow choice in view.Choices)
                {
                    ChoiceRow c = choice;
                    Button button = Ui.Button(Ui.Say(c.Key, c.Substitutions.ToArray()), () => Choose?.Invoke(c.Option), true);
                    button.Disabled = !c.Offered;
                    _buttons.AddChild(button);
                }
            }

            Ui.FocusFirst(_buttons);
        }
    }
}
