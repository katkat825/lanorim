using System.Linq;
using Content.Campaigns;
using Content.Saves;
using Content.Screens;

namespace Content.Tests
{
    // THE MENU AS THE OPEN BOOK'S CONTENTS (cc_task_ui-issues-10-01.md 3.4): Continue (when there is one),
    // the campaigns, Tutorials, Settings, Quit - the first half on the left page, the rest on the right
    public partial class ScreenTests
    {
        [Fact]
        public void TheContentsAreTheMenuSplitAcrossTheTwoPages()
        {
            var saves = new SaveLibrary(_root);
            saves.Save(new SaveGame { Campaign = "the_goat", Slot = 1, Hero = HeroSaves.Capture(Made("fighter")) },
                       SaveKind.ChapterStart);

            Manifest[] all = { Manifest("the_goat"), Manifest("long_road"), Manifest("sample", "test") };
            var contents = new BookContents(new CampaignBook(all, saves));

            Assert.Equal(new[]
            {
                ContentsKind.Continue, ContentsKind.Campaign, ContentsKind.Campaign, ContentsKind.Campaign,
                ContentsKind.Tutorials, ContentsKind.Settings, ContentsKind.Quit,
            }, contents.Entries.Select(e => e.Kind));

            Assert.Equal(new[] { "the_goat", "long_road", "sample" },
                         contents.Entries.Where(e => e.Kind == ContentsKind.Campaign).Select(e => e.Page.Id));

            // seven lines: four on the left, three on the right, in order
            Assert.Equal(4, contents.Left.Count);
            Assert.Equal(3, contents.Right.Count);
            Assert.Equal(contents.Entries, contents.Left.Concat(contents.Right));
        }

        [Fact]
        public void NothingToContinueLeavesContinueOut()
        {
            var contents = new BookContents(new CampaignBook(new[] { Manifest("the_goat") }, new SaveLibrary(_root)));

            Assert.DoesNotContain(contents.Entries, e => e.Kind == ContentsKind.Continue);
            Assert.Equal(ContentsKind.Campaign, contents.Left[0].Kind);
            Assert.Equal(ContentsKind.Quit, contents.Right.Last().Kind);
            Assert.Equal(2, contents.Left.Count);
            Assert.Equal(2, contents.Right.Count);
        }

        [Fact]
        public void EveryContentsLineHasWords()
        {
            var contents = new BookContents(new CampaignBook(new[] { Manifest("the_goat") }));

            Assert.All(contents.Entries, e => Assert.False(string.IsNullOrEmpty(e.NameKey)));
            Assert.Contains(CampaignBook.ContentsKey, CampaignBook.Keys());
        }
    }
}
