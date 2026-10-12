using Core.Space;

namespace Content.Maps
{
    // WHAT THE PAINT AND FILL TOOLS LAY (cc_task_l Part 2.1): a ground, which sets how the square looks and brings its
    // own rule (grass is floor, snow is difficult), or a rule alone (difficult, rock, a chasm), which leaves the look
    // as it was: a rocky patch in the grass, a pool gone deep
    public readonly record struct Brush(Ground? Look, Tile? Rule)
    {
        public static Brush Of(Ground ground) => new(ground, null);

        public static Brush Of(Tile tile) => new(null, tile);

        // what a square of this rule and look becomes under the brush
        public (Tile Tile, Ground Ground) On(Tile tile, Ground ground) =>
            Look is Ground look ? (look.Rule(), look) : (Rule ?? tile, ground);
    }
}
