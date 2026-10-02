using Core.Space;

namespace Content.Maps
{
    // what the map builder's pointer is over, for the tool in hand: a square, or the line between two (MapEditor.Aim)
    public readonly record struct MapTarget(Cell? Square, Border? Line)
    {
        public bool IsNothing => Square == null && Line == null;

        public static readonly MapTarget Nothing = default;
    }
}
