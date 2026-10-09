using Godot;

namespace Game.Screens
{
    public partial class CreationScreen
    {
        // WHICH FIGURE THE MINI IS (cc_task_h Part 2): "Your mini" and two toggles under the class list. Each
        // class has two figures (Game.Board.HeroMinis): its own head, and another character's on the same body. The
        // creator shows no mini, so there's no preview: the toggle names the figure, and the table shows it
        Control Figures()
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            Label named = Ui.Label(ScreenWords.Figure);
            named.AutowrapMode = TextServer.AutowrapMode.Off;
            named.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(named);

            for (int figure = 0; figure < Game.Board.HeroMinis.Figures; figure++)
            {
                int one = figure;
                Button button = Ui.Button(ScreenWords.FigureKey(one), () =>
                {
                    _making.Figure = one;
                    Update();
                });

                button.ToggleMode = true;
                button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                _updates.Add(() => button.SetPressedNoSignal(_making.Figure == one));
                row.AddChild(button);
            }

            return row;
        }
    }
}
