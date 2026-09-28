namespace Core.Magic
{
    // the settings only a dispel reads. DispelHandler declares their keys
    public sealed partial class SpellEffect
    {
        // only curses, all of them, whatever their level - Remove Curse
        public bool Curses { get; init; }

        // it ends a creation of magical force at the aimed square: Disintegrate
        public bool EndsForce { get; init; }
    }
}
