using System.Linq;
using Content.Dialogue;
using Core.Localization;
using Xunit;

namespace Content.Tests
{
    // The dialogue runtime is YarnSpinner's, not ours, so what is worth testing is the SEAM: that a
    // campaign's .yarn source compiles, that every line it produces carries a key the locale audit
    // will recognise, and that a node says who is speaking.
    //
    // Wiring this to anything is Phase 7. Phase 0's job was to get it across and compiling.
    public class DialogueTests
    {
        // EVERY LINE CARRIES A #line: TAG, and the loader refuses one that does not. Without a tag
        // a line is named by a hash of its own text, so rewording it orphans every translation of
        // it - which is the kind of fault nobody notices until a language ships.
        const string Source = @"title: yard_arrival
speaker: wolf
topic: quiet
---
It smells of ash out here. #line:smells_of_ash
Nothing moved all night. #line:nothing_moved
===
";

        static DialogueBook Book() =>
            DialogueBook.Of("ash_yard", ("yard_arrival.yarn", Source));

        [Fact]
        public void ACampaignsYarnSourceCompiles()
        {
            DialogueBook book = Book();

            Assert.Empty(book.Problems);
            Assert.NotNull(book.Program);
            Assert.Contains("yard_arrival", book.Nodes);
        }

        [Fact]
        public void ANodeSaysWhoIsSpeaking()
        {
            Assert.Equal("wolf", Book().SpeakerOf("yard_arrival"));
        }

        [Fact]
        public void ANodeCarriesTheTopicItIsOfferedOn()
        {
            Assert.Equal(Topic.Quiet, Book().TopicOf("yard_arrival"));
        }

        // EVERY LINE IS A KEY, and the locale audit holds the campaign's CSV to exactly these. A
        // line whose key the grammar would refuse is a line nobody can translate.
        [Fact]
        public void EveryLineCarriesAWellFormedKey()
        {
            DialogueBook book = Book();

            Assert.Equal(2, book.Count);
            Assert.All(book.Keys(), key => Assert.True(KeyConventions.IsWellFormed(key), key));
        }

        [Fact]
        public void SourceThatDoesNotCompileIsAProblemRatherThanAThrow()
        {
            DialogueBook book = DialogueBook.Of("ash_yard", ("broken.yarn", "this is not yarn"));

            Assert.NotEmpty(book.Problems);
        }

        // the shuffle bag is the old build's, and it is the thing that stops a companion saying the
        // same line twice in a row out of a bank of forty
        [Fact]
        public void AShuffleBagExhaustsBeforeItRepeats()
        {
            var bag = new ShuffleBag(6, new Core.Dice.SeededRng(11));

            var drawn = Enumerable.Range(0, 6).Select(_ => bag.Next()).ToList();

            Assert.Equal(6, drawn.Distinct().Count());
        }
    }
}
