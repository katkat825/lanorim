using System.Collections.Generic;
using Content.Encounters;
using Content.Loot;

namespace Content.Tests
{
    // the rules every GM table file keeps (content/Schema/TableReader.cs), held once for both of the
    // readers that share them. EncounterTests and LootTests each had a copy of these
    public class TableReaderTests
    {
        static IReadOnlyList<string> Problems(string reader, string text)
        {
            IReadOnlyList<string> problems;

            if (reader == "encounters") EncounterReader.TryRead(text, out _, out problems);
            else LootReader.TryRead(text, out _, out problems);

            return problems;
        }

        [Theory]
        [InlineData("encounters")]
        [InlineData("loot")]
        public void AnEmptyTableIsRefused(string reader)
        {
            Assert.Contains(Problems(reader, @"{ ""tables"": [ { ""id"": ""north_road"", ""entries"": [] } ] }"),
                            p => p.Contains("empty table"));
        }

        [Theory]
        [InlineData("encounters")]
        [InlineData("loot")]
        public void HiddenOrShownAndNothingElse(string reader)
        {
            Assert.Contains(Problems(reader, @"{ ""tables"": [ { ""id"": ""north_road"", ""rolled"": ""secret"",
                                                ""entries"": [ { ""id"": ""quiet"", ""kind"": ""nothing"" } ] } ] }"),
                            p => p.Contains("'hidden' or 'shown'"));
        }
    }
}
