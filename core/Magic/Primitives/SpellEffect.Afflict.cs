namespace Core.Magic
{
    // the settings only a afflict reads. AfflictHandler declares their keys
    public sealed partial class SpellEffect
    {
        // it drops what it holds as the condition lands - Fear
        public bool Disarms { get; init; }

        // the creature cannot end the condition itself - Hideous Laughter's "it can't end the Prone
        // condition on itself"
        public bool Pinned { get; init; }
    }
}
