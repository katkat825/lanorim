using Core.Space;

namespace Content.Maps
{
    // WHAT A WALL OR A DOOR IS MADE OF (cc_task_l Part 3.4): the dungeon's stone, a village's timber, plaster and beam or
    // fieldstone, a wooden fence or a hedge. A look, except for one rule: a fence or a hedge stops a walker and not an
    // eye, so its wall is Edge.Bars to the rules (MapDraft.Layout), the edge that already meant exactly that.
    //
    // cc_task_m3 Part 6: the walls and fences that stood in Props, a piece in the middle of its square that joined nothing,
    // are styles now, drawn on the squares' edges like the rest: an iron fence, a town wall, and the ruins' walls (whole,
    // broken, overgrown, and low). A new style is a new value at the end and a new letter (Letter); a map that never used
    // one never needs it
    public enum WallStyle
    {
        Stone = 0,

        Timber,

        Plaster,

        Fieldstone,

        Fence,

        Hedge,

        IronFence,

        TownWall,

        Ruin,

        RuinBroken,

        RuinOvergrown,

        RuinLow,
    }

    public static class WallStyles
    {
        public static readonly Letters<WallStyle> Letter = new(
            (WallStyle.Stone, 's'), (WallStyle.Timber, 't'), (WallStyle.Plaster, 'p'),
            (WallStyle.Fieldstone, 'f'), (WallStyle.Fence, 'e'), (WallStyle.Hedge, 'h'),
            (WallStyle.IronFence, 'i'), (WallStyle.TownWall, 'w'), (WallStyle.Ruin, 'r'),
            (WallStyle.RuinBroken, 'b'), (WallStyle.RuinOvergrown, 'o'), (WallStyle.RuinLow, 'l'));

        // a wall of this kind blocks sight as well as movement: a fence, a hedge, iron railings and a ruin's low stub
        // don't
        public static bool BlocksSight(this WallStyle style) =>
            style is not (WallStyle.Fence or WallStyle.Hedge or WallStyle.IronFence or WallStyle.RuinLow);

        // ONLY OFFER WHAT WE HAVE (cc_task_m3 Part 6.2): a style with no door of its own offers no Door in the builder, and
        // a door is never drawn in it. The town wall's gate is the gatehouse, a building; a low ruin is a stub of wall
        public static bool HasDoor(this WallStyle style) => style is not (WallStyle.TownWall or WallStyle.RuinLow);

        // what the rules see on a line drawn as `edge` in this style: a fence's or a hedge's wall is bars. a door is a
        // door whatever it hangs in (a gate in a fence is shut, and blocks the view through it, as a door does)
        public static Edge Rule(this WallStyle style, Edge edge) =>
            edge == Edge.Wall && !style.BlocksSight() ? Edge.Bars : edge;
    }
}
