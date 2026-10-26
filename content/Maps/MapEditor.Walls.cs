using System.Collections.Generic;
using System.Linq;
using Core.Space;

namespace Content.Maps
{
    // WALLS AND DOORS, ONE PICK AT A TIME, AND ONLY WHAT WE HAVE (cc_task_m3 Part 6): the Wall tool lays a wall or a door
    // (Line) in a style (Style). Picking one lets the other go, in each of the two choices; a style with no door of its
    // own is never offered for a door (WallStyles.HasDoor), so picking Door with such a style chosen moves the style to
    // the first one that has a door, and picking such a style while Door is chosen does nothing
    public sealed partial class MapEditor
    {
        // the styles the picker offers for this line: every one for a wall, the ones with a door for a door
        public static IReadOnlyList<WallStyle> StylesFor(Edge line) =>
            Screens.MapBuilderView.Styles.Where(s => line != Edge.Door || s.HasDoor()).ToList();

        public bool PickLine(Edge line)
        {
            if (!Screens.MapBuilderView.Lines.Contains(line)) return false;

            Line = line;

            if (!StylesFor(line).Contains(Style)) Style = StylesFor(line).First();

            return true;
        }

        public bool PickStyle(WallStyle style)
        {
            if (!StylesFor(Line).Contains(style)) return false;

            Style = style;
            return true;
        }
    }
}
