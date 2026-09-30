using System;
using System.Collections.Generic;
using Godot;

namespace Game.Screens
{
    // DOES EVERY SCREEN FIT EVERY WINDOW? (cc_task_ui-issues-9-30.md 2)
    //
    //   godot --headless --path game res://launch.tscn -- --layout
    //   godot --headless --path game res://launch.tscn -- --begin sample_millbrook mage --start cellar --autodice --autostory --layout
    //
    // The window is made each size in turn (the canvas stretches as the game's does: 1920x1080, wider or
    // taller with the window's shape), each screen is shown and laid out, and LayoutCheck reads it.
    // The first form checks the launch screens; the second the table's HUD at the hero's first turn,
    // the pause menu and the sheet. Says "layout ok" or every problem it found.
    public partial class LayoutProbe : Node
    {
        public const string Flag = "--layout";

        // the five the task names, and a 4:3
        public static readonly Vector2I[] Sizes =
        {
            new(1280, 720), new(1366, 768), new(1920, 1080), new(2560, 1440), new(3440, 1440), new(1024, 768),
        };

        public List<(string Name, Action Show)> Screens { get; } = new();

        // what to wait for before the first look (the hero's turn), and the board's place on screen
        public Func<bool> WhenReady { get; set; } = () => true;

        public Func<Rect2?> Board { get; set; } = () => null;

        public override async void _Ready()
        {
            double waited = 0;

            while (!WhenReady())
            {
                await this.Frames(1);
                waited += GetProcessDeltaTime();

                if (waited > 180)
                {
                    GD.PrintErr("layout  never got to the moment to look at");
                    GD.Print("layout FAILED - never ready");
                    GetTree().Quit(1);
                    return;
                }
            }

            var problems = new List<string>();
            int looked = 0;

            foreach (Vector2I size in Sizes)
            {
                GetWindow().Mode = Window.ModeEnum.Windowed;
                GetWindow().Size = size;
                await this.Frames(3);

                foreach ((string name, Action show) in Screens)
                {
                    show();
                    await this.Frames(4);

                    Rect2 visible = GetViewport().GetVisibleRect();
                    List<string> found = LayoutCheck.Problems(GetTree().Root, $"{name} {size.X}x{size.Y}", visible, Board());

                    GD.Print($"layout  {name,-10} {size.X}x{size.Y} (canvas {visible.Size.X:0}x{visible.Size.Y:0}): " +
                             (found.Count == 0 ? "fits" : $"{found.Count} problem(s)"));

                    problems.AddRange(found);
                    looked++;
                }
            }

            foreach (string problem in problems) GD.PrintErr("layout  " + problem);

            GD.Print(problems.Count == 0
                ? $"layout ok - {Screens.Count} screens at {Sizes.Length} sizes, {looked} looks"
                : $"layout FAILED - {problems.Count} problem(s)");

            // a --begin run's probe saves go with it
            Game.Play.GameState.ForgetProbeSaves();
            GetTree().Quit(problems.Count == 0 ? 0 : 1);
        }
    }
}
