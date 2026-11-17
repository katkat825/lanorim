using System.Linq;
using Content.Screens;
using Xunit;

namespace Content.Tests
{
    // cc_task_nt Part 3 (Kathleen, 2026-11-16: "second page"): the book's character page holds five, so a campaign that
    // allows more runs onto further pages, five to a page; five or fewer is one page, as before
    public class BookPageSheetTests
    {
        static BookPage With(int characters, int slots = 20) => new BookPage
        {
            Id = "c",
            Slots = slots,
            Characters = Enumerable.Range(0, characters).Select(i => new CharacterSlot { Slot = i, Name = $"Hero {i + 1}" }).ToList(),
        };

        [Theory]
        [InlineData(0, 1)]
        [InlineData(5, 1)]
        [InlineData(6, 2)]
        [InlineData(10, 2)]
        [InlineData(11, 3)]
        [InlineData(20, 4)]
        public void FiveToAPage(int characters, int sheets) => Assert.Equal(sheets, With(characters).Sheets);

        [Fact]
        public void EachPageHoldsItsOwnFiveAndThePageNumberIsKeptInBounds()
        {
            BookPage page = With(12);

            Assert.Equal(new[] { "Hero 1", "Hero 2", "Hero 3", "Hero 4", "Hero 5" }, page.OnSheet(0).Select(c => c.Name));
            Assert.Equal(new[] { "Hero 11", "Hero 12" }, page.OnSheet(2).Select(c => c.Name));
            Assert.Equal(page.OnSheet(2).Select(c => c.Name), page.OnSheet(9).Select(c => c.Name));
            Assert.Equal(page.OnSheet(0).Select(c => c.Name), page.OnSheet(-1).Select(c => c.Name));
        }

        [Fact]
        public void AnOrdinaryCampaignIsOnePageWithEveryCharacterOnIt()
        {
            BookPage page = With(5, slots: 5);

            Assert.Equal(1, page.Sheets);
            Assert.Equal(page.Characters, page.OnSheet(0));
        }
    }
}
