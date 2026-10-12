using System.Collections.Generic;
using System.Linq;
using Content.Screens;

namespace Game.Screens
{
    // THE BOOK'S OTHER PAGES, in the order the contents list them, between the campaigns and Quit (cc_task_j Part 1).
    // The one list: the contents on the open book (BookContents), the fallback panel's buttons and `--show <id>` all
    // read it. A new page is a new IBookPage and one line here
    public static class BookMenu
    {
        // a fresh set for each launch screen: a page may remember things while the book is open (the map builder,
        // which campaign was picked), and forgets them when the screen goes, as Launch's own fields did
        public static IReadOnlyList<IBookPage> Make() => new IBookPage[]
        {
            new TutorialsPage(),
            new MapBuilderPage(),
            new SettingsPage(),
            new CreditsPage(),
        };

        // their lines in the contents
        public static IEnumerable<ContentsEntry> Entries(IEnumerable<IBookPage> pages) =>
            pages.Select(p => ContentsEntry.ForPage(p.Id, p.NameKey));
    }
}
