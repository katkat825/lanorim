namespace Core.Magic
{
    // the settings only a relieve reads. RelieveHandler declares their keys
    public sealed partial class SpellEffect
    {
        // every reduction to an ability score ends - Greater Restoration
        public bool RestoresAbilities { get; init; }
    }
}
