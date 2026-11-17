using System.Linq;
using Content.Campaigns;
using Content.Screens;

namespace Content.Tests
{
    // THE ARENA'S OWN LINE IN THE CONTENTS (cc_task_ca Part 5; Kathleen 2026-11-16, "its own entry"): a campaign tagged
    // "arena" is named by its own name beside Tutorials, and isn't among the ordinary campaigns; none, no line; more
    // than one, an "Arenas" line that opens a short picker
    public partial class ScreenTests
    {
        [Fact]
        public void AnArenaHasItsOwnLineBesideTutorialsUnderItsOwnName()
        {
            Manifest[] all = { Manifest("the_goat"), Manifest("crucible", "arena") };
            var book = new CampaignBook(all);
            var contents = new BookContents(book, OtherPages);

            Assert.Equal(new[] { "the_goat" }, book.Pages.Select(p => p.Id));
            Assert.Equal("crucible", book.Arenas.Single().Id);
            Assert.Same(book.Arenas.Single(), book.Find("crucible"));

            int arena = contents.Entries.ToList().FindIndex(e => e.Page?.Id == "crucible");
            Assert.Equal(ContentsKind.Campaign, contents.Entries[arena].Kind);
            Assert.Equal(all[1].NameKey, contents.Entries[arena].NameKey);
            Assert.Equal("tutorials", contents.Entries[arena + 1].PageId);
        }

        [Fact]
        public void NoArenaNoLineAndTwoArenasAPicker()
        {
            Assert.Empty(new CampaignBook(new[] { Manifest("the_goat") }).ArenaEntries);

            var two = new CampaignBook(new[] { Manifest("crucible", "arena"), Manifest("pit", "arena") });
            ContentsEntry line = Assert.Single(two.ArenaEntries);

            Assert.Equal(ContentsKind.Page, line.Kind);
            Assert.Equal(CampaignBook.ArenasPageId, line.PageId);
            Assert.Equal(CampaignBook.ArenasKey, line.NameKey);
            Assert.Equal(new[] { "crucible", "pit" }, two.Arenas.Select(p => p.Id));
            Assert.Empty(two.Pages);
        }
    }
}
