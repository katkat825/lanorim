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

        // the same for an archway and a window (cc_task_m4 Part 4): the village kit's arch in timber, plaster and
        // fieldstone; the ruins' round arch (whole, broken, overgrown); a window in plaster
        // and fieldstone, and the ruins' barred window. The dungeon's stone has neither yet
        public static bool HasArchway(this WallStyle style) =>
            style is WallStyle.Timber or WallStyle.Plaster or WallStyle.Fieldstone or
                     WallStyle.Ruin or WallStyle.RuinBroken or WallStyle.RuinOvergrown;

        public static bool HasWindow(this WallStyle style) =>
            style is WallStyle.Plaster or WallStyle.Fieldstone or WallStyle.Ruin;

        // whether a line of this kind can be laid in this style: a wall always, the rest only where there's a model
        public static bool Offers(this WallStyle style, Edge line) => line switch
        {
            Edge.Wall => true,
            Edge.Door => style.HasDoor(),
            Edge.Archway => style.HasArchway(),
            Edge.Window => style.HasWindow(),
            _ => false,
        };

        // what the rules see on a line drawn as `edge` in this style: a fence's or a hedge's wall is bars. a door is a
        // door whatever it hangs in (a gate in a fence is shut, and blocks the view through it, as a door does). an
        // archway is an open way that never shuts, a window bars: walked through and seen through, and seen through only
        public static Edge Rule(this WallStyle style, Edge edge) => edge switch
        {
            Edge.Wall when !style.BlocksSight() => Edge.Bars,
            Edge.Archway => Edge.None,
            Edge.Window => Edge.Bars,
            _ => edge,
        };
    }
}
