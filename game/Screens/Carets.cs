using Godot;

namespace Game.Screens
{
    // EVERY TEXT BOX'S CARET BLINKS (cc_task_m2 Part 3): a LineEdit, a TextEdit, a SpinBox's own LineEdit, the Workshop's
    // and the map builder's boxes. Its colour and width are the theme's (LineEdit and TextEdit caret_color and
    // caret_width in ui/lanorim_theme.tres); the blink isn't a theme item in Godot but each box's own setting, so it is
    // set here, once, on every box as it enters the tree - whatever made it, in whatever scene
    public static class Carets
    {
        public static void Watch(SceneTree tree)
        {
            tree.NodeAdded += Blink;

            // and any already standing when the watch began
            foreach (Node node in Nodes.Under<Node>(tree.Root)) Blink(node);
        }

        static void Blink(Node node)
        {
            switch (node)
            {
                case LineEdit line: line.CaretBlink = true; break;
                case TextEdit text: text.CaretBlink = true; break;
            }
        }
    }
}
