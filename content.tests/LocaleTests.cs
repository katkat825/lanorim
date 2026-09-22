using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;
using Content.Spells;
using Core.Localization;

namespace Content.Tests
{
    // the check-locale of the old build, moved inside dotnet test so it cannot be forgotten. the
    // csv is copied beside the test assembly by the project file.
    public class LocaleTests
    {
        const string Csv = "game.csv";

        static string Read() => File.Exists(Csv) ? File.ReadAllText(Csv) : "";

        [Fact]
        public void TheEnglishLocaleIsThere() =>
            Assert.True(File.Exists(Csv),
                        "no game.csv beside the tests - run 'dotnet run --project sim -- locale'");

        [Fact]
        public void EveryKeyTheEngineEmitsHasEnglish()
        {
            IReadOnlyList<string> problems = Locale.Audit(Read(), EngineKeys.Sorted());

            Assert.True(problems.Count == 0,
                        string.Join("\n", problems.Take(30)) +
                        (problems.Count > 30 ? $"\n...and {problems.Count - 30} more" : "") +
                        "\nrun 'dotnet run --project sim -- locale' to scaffold the missing rows");
        }

        [Fact]
        public void EveryKeyTheEngineEmitsFitsTheGrammar()
        {
            string[] malformed = EngineKeys.Malformed().ToArray();

            Assert.True(malformed.Length == 0, string.Join("\n", malformed));
        }

        [Fact]
        public void NothingInTheLocaleHasBeenOrphaned()
        {
            // an orphan is not a bug on its own - the UI's own keys are not in EngineKeys yet -
            // so this watches the engine's namespaces only
            string[] mine = { "ability.", "skill.", "condition.", "damage.", "difficulty.", "spell." };

            string[] orphans = Locale.Orphans(Read(), EngineKeys.Sorted())
                                     .Where(k => mine.Any(k.StartsWith))
                                     .ToArray();

            Assert.True(orphans.Length == 0,
                        "in the locale but nothing asks for it: " + string.Join(", ", orphans));
        }

        [Fact]
        public void TheLocalizerAnswersWithEnglishAndEchoesWhatItDoesNotKnow()
        {
            ILocalizer english = Locale.Localizer(Read());

            Assert.Equal("Strength", english.Get("ability.str.name"));
            Assert.Equal("Fireball", english.Get("spell.fireball.name"));

            // a missing string shows as the raw key, which is how it gets noticed on screen
            Assert.Equal("spell.nothing.name", english.Get("spell.nothing.name"));
        }

        [Fact]
        public void ACsvRoundTripsThroughQuotesAndCommas()
        {
            string csv = Locale.Write(new[]
            {
                ("ui.test.plain", "no punctuation"),
                ("ui.test.comma", "one, two, three"),
                ("ui.test.quote", "she said \"no\""),
            });

            IReadOnlyDictionary<string, string> read = Locale.Read(csv);

            Assert.Equal("one, two, three", read["ui.test.comma"]);
            Assert.Equal("she said \"no\"", read["ui.test.quote"]);
        }

        [Fact]
        public void EnglishIsNeverHardcodedInCore()
        {
            // the spell names live in the locale, not in the catalogue: a Spell carries a key
            SpellBook book = SpellBook.Srd();

            Assert.Equal("spell.fireball.name", book.Find("fireball").NameKey);
        }
    }
}
