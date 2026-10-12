using System.Linq;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // WHAT `--layout` LAYS OUT (checks/check-layout.ps1, cc_task_ui-issues-9-30.md 2): the LayoutProbe for the launch
    // screen (every launch screen, the book's pages from BookMenu among them, so a new page is checked too) or for the
    // table in play (at the hero's first turn: the HUD, the pause menu, the sheet and the pack, with the board's place
    // on screen so the log and the strip can be held off it). Was in Launch.cs and PlayDirector.Layout.cs until
    // cc_task_j Part 2
    public static class Layouts
    {
        public static LayoutProbe OfLaunch(Launch launch)
        {
            var probe = new LayoutProbe { Name = "LayoutProbe" };

            probe.Screens.Add(("title", launch.ShowTitle));
            probe.Screens.Add(("book", launch.ShowBook));

            foreach (IBookPage page in launch.Pages)
            {
                IBookPage one = page;
                probe.Screens.Add((one.Id, () => one.Open(launch)));
            }

            probe.Screens.Add(("create", () => QuickStart.Creating(launch, "class", null)));
            probe.Screens.Add(("spells", () => QuickStart.Creating(launch, "spells", "mage")));
            probe.Screens.Add(("abilities", () => QuickStart.Creating(launch, "abilities", "mage")));

            return probe;
        }

        public static LayoutProbe OfPlay(PlayDirector director)
        {
            var probe = new LayoutProbe
            {
                Name = "LayoutProbe",
                WhenReady = () => director.Combat.CanAct && director.Showing == null &&
                                  (director.Table.Lift?.IsResting ?? true),
                Board = () => BoardOnScreen(director.Table),
            };

            probe.Screens.Add(("fight", () => director.Showing?.Close()));
            probe.Screens.Add(("pause", () => { director.Showing?.Close(); director.Pause(); }));
            probe.Screens.Add(("sheet", () => director.Swap(new SheetScreen(Content.Sheet.SheetView.Of(GameState.Run.Hero, leveling: GameState.Run.Pack.Manifest.Rules.Leveling)))));
            probe.Screens.Add(("pack", () => director.Swap(new PackScreen(new Content.Screens.PackView(GameState.Run.Hero, GameState.Run.Items), GameState.Resolver))));

            return probe;
        }

        // the board's mat, as a box on the screen: its four corners through the camera
        static Rect2? BoardOnScreen(Game.Table.Table table)
        {
            Game.Board.Board board = table.Board;
            Camera3D camera = table.Camera;

            if (board?.Map == null || camera == null) return null;

            float w = board.MatWidth * 0.5f, d = board.MatDepth * 0.5f;
            Vector2[] corners = new[] { new Vector3(-w, 0, -d), new Vector3(w, 0, -d), new Vector3(-w, 0, d), new Vector3(w, 0, d) }
                                .Select(c => camera.UnprojectPosition(board.ToGlobal(c))).ToArray();

            Vector2 min = new(corners.Min(c => c.X), corners.Min(c => c.Y));
            Vector2 max = new(corners.Max(c => c.X), corners.Max(c => c.Y));

            return new Rect2(min, max - min);
        }
    }
}
