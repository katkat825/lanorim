using Core.Space;

namespace Content.Maps
{
    // WHAT A SQUARE LOOKS LIKE (cc_task_l Part 2.1), apart from its rule. The rule is the Tile (floor, difficult, rock,
    // chasm), which Route and Sight read; the ground is only what the board draws there. Stone is the dungeon's floor,
    // the bare mat it always was, so every map written before grounds existed reads as stone and looks as it did
    public enum Ground
    {
        Stone = 0,

        Planks,

        Grass,

        Dirt,

        Cobble,

        Sand,

        Snow,

        // shallow: wading. deep water is this ground over a Chasm square, which nobody crosses
        Water,
    }

    public static class Grounds
    {
        // one letter a square in a map file's "ground" layer (MapFile)
        public static readonly Letters<Ground> Letter = new(
            (Ground.Stone, 's'), (Ground.Planks, 'p'), (Ground.Grass, 'g'), (Ground.Dirt, 'd'),
            (Ground.Cobble, 'c'), (Ground.Sand, 'a'), (Ground.Snow, 'n'), (Ground.Water, 'w'));

        // the rule a ground brings when it is painted: wading and snow are slow going, the rest is floor. the Paint
        // tool can still lay rough or rock on top ("a rocky patch in the grass")
        public static Tile Rule(this Ground ground) => ground is Ground.Water or Ground.Snow ? Tile.Rough : Tile.Floor;
    }
}
