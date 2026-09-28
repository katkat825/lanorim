namespace Core.Magic
{
    // the settings only a heal reads. HealHandler declares their keys
    public sealed partial class SpellEffect
    {
        // it works on the dead, bringing them back with the amount - Raise Dead's 1
        public bool Revives { get; init; }
    }
}
