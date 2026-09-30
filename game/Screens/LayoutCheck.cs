using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Screens
{
    // WHAT A SCREEN'S LAYOUT MUST NEVER DO, AT ANY WINDOW SIZE (cc_task_ui-issues-9-30.md 2), read off
    // the controls as Godot laid them out:
    // - a control on screen past the edge of the visible area (what a scroll clips is the scroll's);
    // - a wrapping label squeezed to a sliver, which reads a letter to a line;
    // - two of the table's HUD panels, or a panel and the top-right corner's words, overlapping;
    // - the log or the turn strip over the board.
    public static class LayoutCheck
    {
        const float Slack = 1.5f;

        // the narrowest a wrapping label with words in it may be
        const float Sliver = 48f;

        public static List<string> Problems(Node root, string screen, Rect2 visible, Rect2? board = null)
        {
            var problems = new List<string>();
            List<Control> shown = Nodes.Under<Control>(root).Where(OnScreen).ToList();

            foreach (Control control in shown)
            {
                Rect2 rect = control.GetGlobalRect();

                if (rect.Position.X < visible.Position.X - Slack || rect.Position.Y < visible.Position.Y - Slack ||
                    rect.End.X > visible.End.X + Slack || rect.End.Y > visible.End.Y + Slack)
                    problems.Add($"{screen}: {Name(control)} at {Box(rect)} runs past the screen ({Box(visible)})");

                if (control is Label { AutowrapMode: not TextServer.AutowrapMode.Off } label &&
                    label.Text.Trim().Length > 3 && rect.Size.X < Sliver)
                    problems.Add($"{screen}: '{Short(label.Text)}' is squeezed to {rect.Size.X:0} wide");
            }

            // the HUD's panels (the outermost of each), and the words in the top right corner
            List<Control> panels = shown.Where(c => c is PanelContainer { ThemeTypeVariation: var v } && v == "HudPanel")
                                        .Where(c => !Ancestors(c).Any(a => a is PanelContainer p && p.ThemeTypeVariation == "HudPanel"))
                                        .ToList();
            List<Control> corner = shown.Where(c => c is Label && c.GetParent()?.Name == "Corner").ToList();

            List<Control> hud = panels.Concat(corner).ToList();

            for (int i = 0; i < hud.Count; i++)
                for (int j = i + 1; j < hud.Count; j++)
                {
                    Rect2 a = hud[i].GetGlobalRect().Grow(-Slack);
                    Rect2 b = hud[j].GetGlobalRect().Grow(-Slack);

                    if (a.Intersects(b))
                        problems.Add($"{screen}: {Name(hud[i])} {Box(hud[i].GetGlobalRect())} overlaps {Name(hud[j])} {Box(hud[j].GetGlobalRect())}");
                }

            // the log (closed) and the strip keep off the board: the top panels are the ones touching the top edge
            if (board is Rect2 mat)
                foreach (Control panel in panels.Where(p => p.GetGlobalRect().Position.Y <= visible.Position.Y + 2 * Slack))
                    if (panel.GetGlobalRect().Grow(-Slack).Intersects(mat))
                        problems.Add($"{screen}: {Name(panel)} {Box(panel.GetGlobalRect())} is over the board {Box(mat)}");

            return problems;
        }

        // visible, sized, and not something a scroll container clips (the scroll itself is checked) or in
        // a popup window (the tree's root is a Window too, and everything is under it)
        static bool OnScreen(Control control) =>
            control.IsVisibleInTree() && control.Size.X > 0 && control.Size.Y > 0 &&
            !Ancestors(control).Any(a => a is ScrollContainer || a is Window && a.GetParent() != null);

        static IEnumerable<Node> Ancestors(Node node)
        {
            for (Node up = node.GetParent(); up != null; up = up.GetParent()) yield return up;
        }

        static string Name(Control control) =>
            control is Label label ? $"label '{Short(label.Text)}'"
            : control is Button button && button.Text != "" ? $"button '{Short(button.Text)}'"
            : $"{control.GetType().Name} {control.Name}";

        static string Short(string text) => text.Length > 30 ? text[..30].Replace("\n", " ") + "…" : text.Replace("\n", " ");

        static string Box(Rect2 r) => $"{r.Position.X:0},{r.Position.Y:0} {r.Size.X:0}x{r.Size.Y:0}";
    }
}
