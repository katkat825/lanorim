namespace Core.Magic
{
    // the settings only a conjure reads. ConjureHandler declares their keys
    public sealed partial class SpellEffect
    {
        // what it makes, and how many: Goodberry's ten berries. core names the item; the content
        // layer puts it in the pack (Casting.Conjured)
        public ConjuredItem Item { get; init; }
    }
}
