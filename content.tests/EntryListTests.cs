using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Items;
using Content.Monsters;
using Content.Schema;
using Content.Species;
using Content.Spells;
using Xunit;

namespace Content.Tests
{
    // the rules every SRD list file keeps (content/Schema/EntryList.cs), held once for all eight
    // readers that share them. three of them used to let a stray top-level key through
    // (cc_task_d-seams-and-duplication.md §1)
    public class EntryListTests
    {
        static IReadOnlyList<string> Problems(string list, string text)
        {
            IReadOnlyList<string> problems = null;

            switch (list)
            {
                case "classes": ClassReader.TryRead(text, out _, out problems); break;
                case "monsters": MonsterReader.TryRead(text, out _, out problems); break;
                case "items": ItemReader.TryRead(text, out _, out problems); break;
                case "species": SpeciesReader.TryRead(text, out _, out problems); break;
                case "backgrounds": BackgroundReader.TryRead(text, out _, out problems); break;
                case "forms": FormReader.TryRead(text, out _, out problems); break;
                case "consequences": ConsequenceReader.TryRead(text, out _, out problems); break;
                case "spells": SpellReader.TryRead(text, out _, out problems); break;
            }

            return problems;
        }

        public static IEnumerable<object[]> Lists() =>
            new[] { "classes", "monsters", "items", "species", "backgrounds", "forms", "consequences", "spells" }
                .Select(l => new object[] { l });

        [Theory]
        [MemberData(nameof(Lists))]
        public void AStrayTopLevelKeyIsRefused(string list)
        {
            Assert.Contains(Problems(list, $@"{{ ""{list}"": [], ""extras"": [] }}"),
                            p => p.Contains("'extras' is not a key here"));
        }

        [Theory]
        [MemberData(nameof(Lists))]
        public void AnEmptyListIsRefusedWithTheShapeItShouldHave(string list)
        {
            Assert.Contains(Problems(list, $@"{{ ""{list}"": [] }}"),
                            p => p.StartsWith($"no {list} in it") && p.Contains($"'{list}' array"));
        }

        [Theory]
        [MemberData(nameof(Lists))]
        public void ABadIdIsRefusedTheSameWayEverywhere(string list)
        {
            Assert.Contains(Problems(list, $@"{{ ""{list}"": [ {{ ""id"": ""Not An Id"" }} ] }}"),
                            p => p.Contains("'Not An Id' is not a") &&
                                 p.Contains("id - lowercase a-z, 0-9 and underscore only"));
        }

        [Fact]
        public void ASpellFileMayStillBeABareArray()
        {
            Assert.Contains(Problems("spells", "[]"), p => p.StartsWith("no spells in it - the file is an array, or"));
        }

        // a campaign's folder says which entry a problem is in, not only which file (§6)
        [Fact]
        public void ALocatedProblemSaysWhichEntry()
        {
            var problems = new List<ContentProblem>();

            new EntryList<Background>("backgrounds", "background", BackgroundReader.Keys,
                                      (entry, id, trouble) => { trouble.Add($"{id}: wrong"); return null; })
                .Read(@"{ ""backgrounds"": [ { ""id"": ""sage"" }, { ""id"": ""Bad"" } ] }", problems);

            Assert.Contains(problems, p => p.Where == "backgrounds.sage" && p.What == "wrong");
            Assert.Contains(problems, p => p.Where == "backgrounds[1]" && p.What.Contains("'Bad'"));
        }

        // a catalogue said nothing when two things had one id: the last quietly won (§7)
        [Fact]
        public void ACatalogueSaysWhenAnIdIsDefinedTwiceAndKeepsTheFirst()
        {
            var srd = Library.Srd();
            Monsters.Monster goblin = srd.Bestiary.Find("goblin");

            var bestiary = new Monsters.Bestiary(new[] { goblin, goblin });
            Assert.Contains(bestiary.Problems, p => p.Contains("'goblin' is defined twice"));
            Assert.Equal(1, bestiary.Count);

            BackgroundReader.TryRead(@"{ ""backgrounds"": [
                { ""id"": ""sage"", ""skills"": [""arcana"", ""history""], ""abilities"": [""int"", ""wis"", ""con""] },
                { ""id"": ""sage"", ""skills"": [""stealth"", ""history""], ""abilities"": [""int"", ""wis"", ""con""] } ] }",
                                     out IReadOnlyList<Background> twice, out _);

            var listing = new Listing<Background>(twice, b => b.Id, b => b.Keys());

            Assert.Contains(listing.Problems, p => p.Contains("'sage' is defined twice"));
            Assert.Single(listing);
            Assert.Same(twice[0], listing.Find("sage"));
            Assert.Same(twice[0], listing[0]);
        }

        [Fact]
        public void TheSrdLibraryFindsClassesSpeciesAndBackgroundsById()
        {
            Library srd = Library.Srd();

            Assert.True(srd.Sound, string.Join("\n", srd.Problems));
            Assert.Equal("fighter", srd.Class("fighter").Id);
            Assert.Equal("elf", srd.Kind("elf").Id);
            Assert.Null(srd.Background("nobody"));
            Assert.Equal(srd.Classes.Count, srd.Classes.All.Count());
        }
    }
}
