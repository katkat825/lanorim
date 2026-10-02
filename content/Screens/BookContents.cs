using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Screens
{
    public enum ContentsKind
    {
        Continue,
        Campaign,
        Tutorials,
        MapBuilder,
        Settings,
        Credits,
        Quit,
    }

    // one line of the contents: what it opens, and its words
    public sealed class ContentsEntry
    {
        public ContentsKind Kind { get; init; }

        public string NameKey { get; init; } = "";

        // the campaign it opens, for a Campaign line
        public BookPage Page { get; init; }

        public override string ToString() => Kind == ContentsKind.Campaign ? $"{Kind} {Page?.Id}" : Kind.ToString();
    }

    // THE MENU AS THE OPEN BOOK'S TABLE OF CONTENTS (cc_task_ui-issues-10-01.md 3.4). Kathleen: "put the
    // buttons on halves of the book as if they were a table of contents". Continue first when there is
    // something to continue, then each campaign's page, then Tutorials, the Map builder, Settings, Credits and Quit
    // (cc_task_f Parts 2 and 3); the left page
    // holds the first half (the larger half, when they don't split evenly) and the right page the rest
    public sealed class BookContents
    {
        public BookContents(CampaignBook book)
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

            entries.Add(new ContentsEntry { Kind = ContentsKind.Tutorials, NameKey = CampaignBook.TutorialsKey });
            entries.Add(new ContentsEntry { Kind = ContentsKind.MapBuilder, NameKey = CampaignBook.MapBuilderKey });
            entries.Add(new ContentsEntry { Kind = ContentsKind.Settings, NameKey = CampaignBook.SettingsKey });
            entries.Add(new ContentsEntry { Kind = ContentsKind.Credits, NameKey = CampaignBook.CreditsKey });
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
