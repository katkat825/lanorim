using Xunit;

namespace Content.Tests
{
    // THE SOLO SWITCHES ARE GLOBAL (Content.SoloPlay.SoloRules), so a test that flips one runs in this collection,
    // alone, after the rest: no other test can be mid-fight when a rule changes under it. Each puts back what it
    // found
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class SoloRulesCollection
    {
        public const string Name = "solo rules";
    }
}
