using Content.Creation;
using Game.Board;
using Godot;

namespace Game.Screens
{
    public partial class CreationScreen
    {
        // THE MINI, NOT "FIRST FIGURE / SECOND FIGURE" (cc_task_m2 Part 1). On the class, species and lineage pages the
        // hero's mini floats to the right of the list, turning slowly, in the screen's own small viewport (MiniPreview):
        // its class, its figure, and what its species and lineage make of it (HeroMinis: a Beastfolk's plushie, a
        // Fairy's wings). Under the class list the choice of figure is the two figures themselves: click one to choose
        // it, the chosen one pressed. Underneath it is still Hero.Figure, 0 or 1 ($hero_figure 1 or 2)
        static bool ShowsTheMini(Step step) => step is Step.Class or Step.Species or Step.Lineage;

        MiniLook LookFor(int figure) =>
            _making.Class == null
                ? null
                : HeroMinis.LookOf(_making.Class.Id, figure, _making.Species?.Id, _making.Lineage?.Id);

        Control TheMini()
        {
            var size = new Vector2I((int)Ui.Px(220), (int)Ui.Px(250));
            var mini = new MiniPreview(size) { Name = "TheMini", SizeFlagsHorizontal = SizeFlags.ShrinkCenter };

            _updates.Add(() => mini.Show(LookFor(_making.Figure)));
            return mini;
        }

        Control Figures()
        {
            var row = new HBoxContainer { Name = "Figures" };
            row.AddThemeConstantOverride("separation", 12);
            Label named = Ui.Label(ScreenWords.Figure);
            named.AutowrapMode = TextServer.AutowrapMode.Off;
            named.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(named);

            for (int figure = 0; figure < HeroMinis.Figures; figure++)
            {
                int one = figure;
                var size = new Vector2I((int)Ui.Px(64), (int)Ui.Px(80));

                // the figure itself is the button: no words on it, only the mini, still, facing you
                var card = new Button
                {
                    Name = "Figure" + (one + 1),
                    ToggleMode = true,
                    CustomMinimumSize = size + new Vector2I(12, 12),
                    TooltipText = "",
                    AccessibilityName = Ui.Say(ScreenWords.FigureKey(one)),
                };

                var mini = new MiniPreview(size) { Turning = false, Position = new Vector2(6, 6) };
                card.AddChild(mini);

                card.Pressed += () =>
                {
                    _making.Figure = one;
                    Update();
                };

                _updates.Add(() =>
                {
                    card.SetPressedNoSignal(_making.Figure == one);
                    card.Disabled = _making.Class == null;
                    mini.Show(LookFor(one));
                });

                row.AddChild(card);
            }

            return row;
        }
    }
}
