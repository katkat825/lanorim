using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Screens
{
    // a Page is one of the book's other pages, the game's list of them (the tutorials, the map builder, the settings,
    // the credits: game/Screens/BookPages.cs); it was a kind per page until cc_task_j Part 1, so that a new page is
    // one line in that list and no new kind here
    public enum ContentsKind
    {
        Continue,
        Campaign,
        Page,
        Quit,
    }

    // one line of the contents: what it opens, and its words
    public sealed class ContentsEntry
    {
        public ContentsKind Kind { get; init; }

        public string NameKey { get; init; } = "";

        // the campaign it opens, for a Campaign line
        public BookPage Page { get; init; }

        // which of the other pages it opens, for a Page line (the page's id: "tutorials", "credits")
        public string PageId { get; init; }

        // a page line for one of the book's other pages
        public static ContentsEntry ForPage(string id, string nameKey) =>
            new ContentsEntry { Kind = ContentsKind.Page, PageId = id, NameKey = nameKey };

        public override string ToString() =>
            Kind == ContentsKind.Campaign ? $"{Kind} {Page?.Id}"
            : Kind == ContentsKind.Page ? $"{Kind} {PageId}"
            : Kind.ToString();
    }

    // THE MENU AS THE OPEN BOOK'S TABLE OF CONTENTS (cc_task_ui-issues-10-01.md 3.4). Kathleen: "put the
    // buttons on halves of the book as if they were a table of contents". Continue first when there is
    // something to continue, then each campaign's page, then the book's other pages in the order given (the game's:
    // Tutorials, the Map builder, Settings, Credits, cc_task_f Parts 2 and 3), then Quit; the left page
    // holds the first half (the larger half, when they don't split evenly) and the right page the rest
    public sealed class BookContents
    {
        public BookContents(CampaignBook book, IEnumerable<ContentsEntry> pages = null)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));

            var entries = new List<ContentsEntry>();

            if (book.CanContinue)
                entries.Add(new ContentsEntry { Kind = ContentsKind.Continue, NameKey = CampaignBook.ContinueKey });

            entries.AddRange(book.Pages.Select(p => new ContentsEntry
            {
                Kind = ContentsKind.Campaign,
                NameKey = p.NameKey,
                Page = p,
            }));

            entries.AddRange(pages ?? Enumerable.Empty<ContentsEntry>());
            entries.Add(new ContentsEntry { Kind = ContentsKind.Quit, NameKey = CampaignBook.QuitKey });

            Entries = entries;

            int left = (entries.Count + 1) / 2;
            Left = entries.Take(left).ToList();
            Right = entries.Skip(left).ToList();
        }

        public IReadOnlyList<ContentsEntry> Entries { get; }

        public IReadOnlyList<ContentsEntry> Left { get; }

        public IReadOnlyList<ContentsEntry> Right { get; }

        public override string ToString() => $"{Left.Count} on the left, {Right.Count} on the right";
    }
}
