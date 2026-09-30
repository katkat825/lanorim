using System.Linq;
using Game.Screens;
using Godot;

namespace Game.Play
{
    public partial class PlayDirector
    {
        // --- the table's layout at every window size (cc_task_ui-issues-9-30.md 2) --------------------
        //
        // `--begin ... --layout`: at the hero's first turn, the HUD, the pause menu and the sheet are each
        // laid out at every size and read by LayoutCheck, with the board's place on screen so the log and
        // the strip can be held off it

        void CheckLayoutWhenAsked()
        {
            if (!OS.GetCmdlineUserArgs().Contains(LayoutProbe.Flag)) return;

            var probe = new LayoutProbe
            {
                Name = "LayoutProbe",
                WhenReady = () => _combat.CanAct && _overlay == null && (Table.Lift?.IsResting ?? true),
                Board = BoardOnScreen,
            };

            probe.Screens.Add(("fight", () => _overlay?.Close()));
            probe.Screens.Add(("pause", (System.Action)(() => { _overlay?.Close(); Pause(); })));
            probe.Screens.Add(("sheet", () => Swap(new SheetScreen(Content.Sheet.SheetView.Of(Run.Hero)))));
            probe.Screens.Add(("pack", () => Swap(new PackScreen(new Content.Screens.PackView(Run.Hero, Run.Items), GameState.Resolver))));

            AddChild(probe);
        }

        // the board's mat, as a box on the screen: its four corners through the camera
        Rect2? BoardOnScreen()
        {
            Game.Board.Board board = Table.Board;
            Camera3D camera = Table.Camera;

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
