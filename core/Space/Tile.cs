namespace Core.Space
{
    // three kinds, and no wall among them - walls live on the lines now (see Edge)
    public enum Tile
    {
        // off-map or solid rock - impassable and opaque, so Route and Sight need no bounds check
        Void = 0,

        Floor,

        Rough,

        // deep water or a chasm (cc_task_l Part 2.2): nobody crosses it, everybody sees across it - Void with the
        // sight left in. what it looks like (water, a drop) is the square's ground, not this
        Chasm,
    }

    public static class Tiles
    {
        public static bool IsPassable(this Tile tile) => tile == Tile.Floor || tile == Tile.Rough;

        // separate from IsPassable on purpose - a chasm is transparent but impassable, a thicket the reverse
        public static bool IsTransparent(this Tile tile) => tile is Tile.Floor or Tile.Rough or Tile.Chasm;

        public static int MoveCost(this Tile tile) => tile == Tile.Rough ? 2 : 1;

        public const int MinimumCost = 1;
    }
}
