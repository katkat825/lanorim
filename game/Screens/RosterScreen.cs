using System;
using Content.Challengers;
using Content.Screens;
using Godot;

namespace Game.Screens
{
    // THE ARENA'S ROSTER (cc_task_cg Part 6): the challengers on offer down the left, each by name with its difficulty
    // and a mark for a special fight; the one selected on the right, with its description, whether it's one opponent,
    // a group or a team, and who steps into the arena; Fight picks it. Back to the story leaves without picking, so the
    // player can see the quartermaster first. A view over RosterView; how it looks is Kathleen's
    public partial class RosterScreen : Overlay
    {
        readonly RosterView _view;
        readonly Action<string> _fight;
        readonly Action _back;

        public RosterScreen(RosterView view, Action<string> fight, Action back)
        {
            _view = view;
            _fight = fight;
            _back = back;
            Width = 900;
        }

        // Esc is Back to the story
        protected override bool CanCancel => false;

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!@event.IsActionPressed("ui_cancel")) return;

            GetViewport().SetInputAsHandled();
            Back();
        }

        protected override void Draw()
        {
            Body.AddChild(Ui.Title(RosterView.TitleKey));
            Body.AddChild(Ui.Label(RosterView.ReputationKey, _view.Reputation));

            if (_view.Rows.Count == 0)
            {
                Body.AddChild(Ui.Label(RosterView.EmptyKey));
            }
            else
            {
                Body.AddChild(Ui.Label(RosterView.ChooseKey));

                VBoxContainer details = Details(_view.Selected);
                details.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                Control list = Ui.Scroll(List(), 420);
                list.CustomMinimumSize = new Vector2(Ui.Px(380), list.CustomMinimumSize.Y);

                Body.AddChild(Ui.Row(24, list, details));
            }

            Body.AddChild(Ui.Button(RosterView.BackKey, Back));
        }

        // one button a challenger: its name, its difficulty, and whether it's special; the selected one held down
        VBoxContainer List()
        {
            var list = Ui.Column(4);

            foreach (Challenger row in _view.Rows)
            {
                string words = $"{Ui.Say(row.NameKey)}  ·  {Ui.Say(RosterView.DifficultyKey(row))}" +
                               (row.Special ? $"  ·  {Ui.Say(RosterView.SpecialKey)}" : "");

                string id = row.Id;
                Button button = Ui.Button(words, () => { _view.Select(id); Redraw(); }, true);
                button.ToggleMode = true;
                button.ButtonPressed = ReferenceEquals(row, _view.Selected);
                button.Alignment = HorizontalAlignment.Left;
                button.Name = "Challenger_" + id;
                list.AddChild(button);
            }

            return list;
        }

        VBoxContainer Details(Challenger one)
        {
            var details = Ui.Column(8);

            if (one == null) return details;

            Label name = Ui.Label(one.NameKey);
            name.ThemeTypeVariation = "CardTitle";
            details.AddChild(name);

            string band = $"{Ui.Say(RosterView.DifficultyKey(one))}  ·  {Ui.Say(RosterView.ShapeKey(one))}" +
                          (one.Special ? $"  ·  {Ui.Say(RosterView.SpecialKey)}" : "");
            details.AddChild(Ui.Panel(Ui.Plain(band), "DarkPanel"));

            Label description = Ui.Label(one.DescriptionKey);
            description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            details.AddChild(description);

            details.AddChild(Ui.Label(RosterView.WhoKey));

            foreach ((int count, string monster) in _view.Members(one))
                details.AddChild(Ui.Plain(count == 1 ? Ui.Say(monster) : Ui.Say(RosterView.MemberKey, count, Ui.Say(monster))));

            Button fight = Ui.Button(RosterView.FightKey, () => Fight(one.Id));
            fight.Name = "Fight";
            details.AddChild(fight);

            return details;
        }

        bool _done;

        void Fight(string id)
        {
            if (_done) return;

            _done = true;
            Close();
            _fight?.Invoke(id);
        }

        void Back()
        {
            if (_done) return;

            _done = true;
            Close();
            _back?.Invoke();
        }
    }
}
