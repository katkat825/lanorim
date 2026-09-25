using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Dialogue;
using Content.Schema;
using Core.Localization;
using Xunit;

namespace Content.Tests
{
    // The 'companion' speaker: one line a base-game campaign writes once, said by whichever
    // companion the player brought, with a companion's own row winning where the campaign wrote one.
    // It follows the DM's floor under a beat, so these tests are shaped like that promise - the
    // generic line always reaches the player, and the specific one is only ever flavour on top.
    public class CompanionSpeakerTests
    {
        static readonly Library Srd = Library.Srd();

        const string Campaign = "greyhollow";

        const string Source = @"title: the_track
speaker: companion
---
Somebody's been through here. #line:somebody_passed
Fresh, too. #line:fresh_too #speaker:bound_imp
===
";

        static DialogueBook Book() => DialogueBook.Of(Campaign, ("the_track.yarn", Source));

        static DialogueLine Generic() => Book().Line("line:somebody_passed");

        const string GenericKey = "dialogue.companion.line.greyhollow.somebody_passed";

        const string WolfKey = "dialogue.bonded_wolf.line.greyhollow.somebody_passed";

        static ILocalizer Locale(params string[] keys) =>
            new DictionaryLocalizer(keys.ToDictionary(k => k, k => "[" + k + "]"));

        static Conversation.Said Said() =>
            new Conversation.Said(Generic(), System.Array.Empty<string>());


        // THE VALIDATION HALF: 'companion' is a speaker the book keys like any other, the way 'dm'
        // is a voice the spine keys like any other - reserved by meaning, not refused by the loader
        [Fact]
        public void TheBookAcceptsCompanionAsASpeaker()
        {
            DialogueBook book = Book();

            Assert.Empty(book.Problems);
            Assert.Equal(DialogueLine.AnyCompanion, book.SpeakerOf("the_track"));
            Assert.Equal(GenericKey, Generic().Key);
            Assert.True(KeyConventions.IsWellFormed(Generic().Key));
            Assert.True(Generic().ForAnyCompanion);
        }

        [Fact]
        public void TheAliasIsSpelledTheWayTheDmIs()
        {
            Assert.Equal("companion", DialogueLine.AnyCompanion);
            Assert.Equal("dm", Beat.TheDm);
        }

        // a line some particular creature says is never re-voiced, even in a companion's node
        [Fact]
        public void ANamedSpeakerIsNotTheAlias()
        {
            DialogueLine imp = Book().Line("line:fresh_too");

            Assert.False(imp.ForAnyCompanion);
            Assert.Equal("bound_imp", imp.SpeakerFor("bonded_wolf"));
            Assert.Equal(imp.Key, imp.KeyFor("bonded_wolf", _ => true));
        }


        // EVERY CLASS HEARS IT, in its own companion's mouth, from the one generic row
        [Fact]
        public void TheGenericLineGoesToEveryClasssCompanion()
        {
            ILocalizer locale = Locale(GenericKey);

            foreach (CharacterClass cls in Srd.Classes)
            {
                Assert.Equal(cls.Companion, Said().SpeakerFor(cls.Companion));
                Assert.Equal(GenericKey, Generic().KeyFor(cls.Companion, locale.Has));
                Assert.Equal("[" + GenericKey + "]", Said().Text(locale, cls.Companion));
            }
        }

        // the wolf's own row wins for the wolf - and so for both classes that bring it - and
        // nobody else is handed the wolf's words
        [Fact]
        public void ACompanionsOwnRowOverridesTheGenericOne()
        {
            ILocalizer locale = Locale(GenericKey, WolfKey);

            Assert.Equal("[" + WolfKey + "]", Said().Text(locale, Srd.Class("barbarian").Companion));
            Assert.Equal("[" + WolfKey + "]", Said().Text(locale, Srd.Class("fighter").Companion));
            Assert.Equal("[" + GenericKey + "]", Said().Text(locale, Srd.Class("rogue").Companion));
        }

        // a companion from a pack the campaign predates has no row, and still gets the line
        [Fact]
        public void AnUnknownCompanionFallsBackToTheGenericLine()
        {
            ILocalizer locale = Locale(GenericKey, WolfKey);

            Assert.Equal("[" + GenericKey + "]", Said().Text(locale, "clockwork_owl"));
            Assert.Equal("clockwork_owl", Said().SpeakerFor("clockwork_owl"));
        }

        // no companion at all, or an id that could never be a speaker, is the generic line as written
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Not An Id")]
        [InlineData("companion")]
        public void NoUsableCompanionLeavesTheLineAsWritten(string companion)
        {
            ILocalizer locale = Locale(GenericKey, WolfKey);

            Assert.Equal(GenericKey, Generic().KeyFor(companion, locale.Has));
            Assert.Equal(DialogueLine.AnyCompanion, Said().SpeakerFor(companion));
            Assert.Equal(Said().Text(locale), Said().Text(locale, companion));
        }

        // the locale audit is told the overrides are real keys, and every one of them is well formed
        [Fact]
        public void EveryOverrideIsAKeyTheAuditKnows()
        {
            string[] companions = Srd.Classes.Select(c => c.Companion).Distinct().ToArray();

            List<string> keys = Book().KeysFor(companions).ToList();

            Assert.Contains(GenericKey, keys);
            Assert.Contains(WolfKey, keys);
            Assert.All(keys, key => Assert.True(KeyConventions.IsWellFormed(key), key));

            // the imp's own line is not generic, so it gains no overrides
            Assert.DoesNotContain("dialogue.bonded_wolf.line.greyhollow.fresh_too", keys);
            Assert.Equal(Book().Count + companions.Length, keys.Count);
        }

        // a conversation run through the real runtime hands the line to the companion at the table
        [Fact]
        public void AConversationSpeaksInTheCompanionAtTheTable()
        {
            var talk = new Conversation(Book());

            Assert.True(talk.Start("the_track"));
            Assert.Equal(DialogueLine.AnyCompanion, talk.Saying.Speaker);
            Assert.Equal("saints_fragment", talk.Saying.SpeakerFor("saints_fragment"));
            Assert.Equal("[" + WolfKey + "]", talk.Saying.Text(Locale(GenericKey, WolfKey), "bonded_wolf"));
        }
    }
}
