using Core.Space;

namespace Content.Maps
{
    // WHAT A WALL OR A DOOR IS MADE OF (cc_task_l Part 3.4): the dungeon's stone, a village's timber, plaster and beam or
    // fieldstone, a wooden fence or a hedge. A look, except for one rule: a fence or a hedge stops a walker and not an
    // eye, so its wall is Edge.Bars to the rules (MapDraft.Layout), the edge that already meant exactly that
    public enum WallStyle
    {
        Stone = 0,

        Timber,

        Plaster,

        Fieldstone,

        Fence,

        Hedge,
    }

    public static class WallStyles
    {
        public static readonly Letters<WallStyle> Letter = new(
            (WallStyle.Stone, 's'), (WallStyle.Timber, 't'), (WallStyle.Plaster, 'p'),
            (WallStyle.Fieldstone, 'f'), (WallStyle.Fence, 'e'), (WallStyle.Hedge, 'h'));

        // a wall of this kind blocks sight as well as movement
        public static bool BlocksSight(this WallStyle style) => style is not (WallStyle.Fence or WallStyle.Hedge);

        // what the rules see on a line drawn as `edge` in this style: a fence's or a hedge's wall is bars. a door is a
        // door whatever it hangs in (a gate in a fence is shut, and blocks the view through it, as a door does)
        public static Edge Rule(this WallStyle style, Edge edge) =>
            edge == Edge.Wall && !style.BlocksSight() ? Edge.Bars : edge;
    }
}
