using System.Collections.Generic;
using Core.Space;

namespace Content.Maps
{
    // A PIECE OF MAP, COPIED (cc_task_l Part 4.3): a rectangle's squares (rule and look), its lines (walls, doors and
    // their styles, the outline's included), the props standing in it and the markers on it, every place counted from
    // its top-left corner so it can be put down anywhere (MapDraft.Paste)
    public sealed class MapClip
    {
        internal MapClip(int width, int height) => (Width, Height) = (width, height);

        public int Width { get; }

        public int Height { get; }

        internal readonly Dictionary<Cell, (Tile Tile, Ground Ground)> Squares = new();

        internal readonly Dictionary<Border, (Edge Edge, WallStyle Style)> Lines = new();

        internal readonly List<Prop> Props = new();

        internal readonly Dictionary<int, Cell> Spawns = new();

        internal Cell? Start;

        public int PropCount => Props.Count;

        // debug only, never localized
        public override string ToString() => $"{Width} x {Height} clip, {Props.Count} props, {Lines.Count} lines";
    }
}
