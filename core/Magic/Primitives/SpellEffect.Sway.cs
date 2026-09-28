namespace Core.Magic
{
    // the settings only a sway reads. SwayHandler declares their keys
    public sealed partial class SpellEffect
    {
        // the target leaves the board for the duration and comes back when it ends: Banishment,
        // Maze
        public bool Banishes { get; init; }

        // the amount raises the hit point maximum and current hit points - Aid
        public bool RaisesMaximum { get; init; }
    }
}
