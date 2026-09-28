namespace Core.Magic
{
    // the settings only a direct reads. DirectHandler declares their keys
    public sealed partial class SpellEffect
    {
        // the word: Command's approach, drop, flee, grovel or halt
        public Command Command { get; init; }
    }
}
