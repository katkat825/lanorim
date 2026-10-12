using System.Collections.Generic;
using System.Linq;
using Core.Space;

namespace Content.Maps
{
    // HOW A MAP LOOKS, BESIDE WHAT IT IS (cc_task_l Part 2): each square's ground and each line's wall style. The rules'
    // map (MapLayout) has no use for either, so this rides beside it to the board the way the props do (Package.LookOn,
    // MapDraft.Look). Immutable: a door opened mid-fight changes the rules' map, never this
    public sealed class MapLook
    {
        readonly Ground[] _ground;
        readonly Dictionary<Border, WallStyle> _styles;

        public MapLook(Extent extent, Ground[] ground = null, IReadOnlyDictionary<Border, WallStyle> styles = null)
        {
            Extent = extent;
            _ground = ground != null && ground.Length == extent.Count ? (Ground[])ground.Clone() : new Ground[extent.Count];
            _styles = styles?.Where(s => s.Value != WallStyle.Stone).ToDictionary(s => s.Key, s => s.Value)
                      ?? new Dictionary<Border, WallStyle>();
        }

        // a map with no look of its own: stone everywhere, the dungeon's walls
        public static MapLook Plain(Extent extent) => new(extent);

        public Extent Extent { get; }

        public Ground At(Cell cell) => Extent.At(_ground, cell);

        public WallStyle At(Border border) => _styles.TryGetValue(border, out WallStyle style) ? style : WallStyle.Stone;

        // every ground the map uses: what the board has to make a material for
        public IEnumerable<Ground> Grounds => _ground.Distinct();

        public override string ToString() =>
            $"{string.Join("/", Grounds)} ground, {_styles.Count} styled lines";
    }
}
