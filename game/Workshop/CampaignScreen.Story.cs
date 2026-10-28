using System;
using System.Collections.Generic;
using System.Linq;
using Content.Workshop;
using Content.Workshop.Story;
using Game.Access;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE STORY TAB (cc_task_o Part 1): the board, with New card, Fit all, zoom, search across all the story's words, and
    // Go to card; a card opened shows its blocks (CampaignScreen.Card.cs). Positions, colours and the board's view are
    // the draft's (workshop.json), never the Yarn's
    public partial class CampaignScreen
    {
        // the card open in the editor ("" the board), and the block to highlight in it
        string _openStory = "";
        int[] _highlight;
        StoryBoard _board;
        IReadOnlyList<StoryProblems.OnCard> _storyProblems = Array.Empty<StoryProblems.OnCard>();

        public StoryBoard Board => _board;

        public string OpenStoryCard => _openStory;

        void Story(VBoxContainer page)
        {
            _storyProblems = StoryProblems.Of(Draft.Story, Editor.Problems, Editor.Folder, Draft.Chapters.Select(c => c.Id));

            if (_openStory.Length > 0 && Draft.Story.Card(_openStory) != null)
            {
                Card(page, _openStory);
                return;
            }

            _openStory = "";

            page.AddChild(Forms.Hint(StoryWords.StoryHintKey));
            page.AddChild(StoryTools());

            if (Report() is { } report) page.AddChild(report);
            if (Seen() is { } seen) page.AddChild(seen);

            // a chapter with no starting card: a problem, with Make it
            foreach (ChapterDraft chapter in Draft.Chapters.Where(c => Draft.Story.Card(c.Id) == null))
            {
                string id = chapter.Id;
                Label missing = Ui.Label(StoryWords.NoStartKey, chapter.Title.Length > 0 ? chapter.Title : id);
                missing.AddThemeColorOverride("font_color", Forms.Wrong);
                missing.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                Button make = Ui.Button(StoryWords.MakeItKey, () => Change(() => Editor.MakeChapterStart(id, Ui.Say(WorkshopWords.StoryStartsKey), 40, 40) != null));
                make.Name = "MakeStart_" + id;
                var row = Ui.Row(8, missing, make);
                row.Name = "NoStart_" + id;
                page.AddChild(row);
            }

            _board = new StoryBoard
            {
                Name = "Board",
                Story = Draft.Story,
                DotOf = t => StoryProblems.DotOf(_storyProblems, t),
                IsStart = t => Draft.Chapter(t) != null,
            };
            _board.Look(Draft.Story.View);
            _board.Opened += t => OpenStory(t);
            _board.NewCardAt += at => NewCardAt(at, null);
            _board.ArrowToEmpty += (from, at) => NewCardAt(at, from);
            _board.ArrowToCard += (from, to) => Change(() => Editor.AddBlock(from, Array.Empty<int>(), -1, (_, _) => new Jump { Target = to }) != null);
            _board.Moved += (t, at) => Editor.MoveCard(t, at.X, at.Y);
            _board.Duplicate += t => Change(() => Editor.DuplicateCard(t) != null);
            _board.Delete += AskToDeleteCard;
            _board.Viewed += () => Draft.Story.View = _board.View;
            _board.Ready += () => _board.Build();

            page.AddChild(_board);
            page.AddChild(MemoryList());
        }

        // New card, Fit all, + and -, search, Go to card
        HBoxContainer StoryTools()
        {
            LineEdit name = Forms.Line("NewCardName", "", _ => { }, StoryWords.CardNameKey);
            name.CustomMinimumSize = new Vector2(Ui.Px(180), 0);

            void Make()
            {
                Vector2 at = _board == null ? Vector2.Zero : (_board.Size / 2 - new Vector2(_board.View.X, _board.View.Y)) / _board.Zoom - StoryBoard.Tile / 2;
                string title = Editor.NewCard(string.IsNullOrWhiteSpace(name.Text) ? Ui.Say(StoryWords.NewCardKey) : name.Text.Trim(), at.X, at.Y);
                GD.Print($"workshop card {title} added to {Editor.Id}");
                Redraw();
            }

            name.TextSubmitted += _ => Make();
            Button make = Ui.Button(StoryWords.NewCardKey, Make);
            make.Name = "NewCard";

            LineEdit search = Forms.Line("SearchStory", "", _ => { }, StoryWords.SearchStoryKey);
            search.CustomMinimumSize = new Vector2(Ui.Px(200), 0);
            search.TextSubmitted += words =>
            {
                CardDraft found = Draft.Story.Cards.FirstOrDefault(c => (c.IsText ? c.Text : string.Join("\n", Blocks.Lines(c.Blocks).Select(l => l.Text)))
                                                                         .Contains(words, StringComparison.OrdinalIgnoreCase));
                if (found != null) _board?.Show(found.Title);
            };

            var titles = Draft.Story.Cards.Select(c => c.Title).ToList();
            var go = new OptionButton { Name = "GoToCard", FitToLongestItem = false, ClipText = true, CustomMinimumSize = new Vector2(Ui.Px(180), 0) };
            go.AddItem(Ui.Say(StoryWords.GoToCardKey));
            foreach (string title in titles) go.AddItem(Draft.Story.NameOf(title));
            go.ItemSelected += i => { if (i > 0) _board?.Show(titles[(int)i - 1]); };

            Button fit = Ui.Button(StoryWords.FitAllKey, () => _board?.FitAll());
            fit.Name = "FitAll";
            Button bigger = Ui.Button(StoryWords.ZoomInKey, () => _board?.ZoomBy(1.2f));
            bigger.Name = "ZoomIn";
            Button smaller = Ui.Button(StoryWords.ZoomOutKey, () => _board?.ZoomBy(1 / 1.2f));
            smaller.Name = "ZoomOut";

            return Ui.Row(8, name, make, fit, bigger, smaller, search, go);
        }

        // a card made where the board was double-clicked, or at the end of an arrow dragged from `from` (and the go-to to
        // it added to that card in the same step)
        void NewCardAt(Vector2 at, string from)
        {
            string title = Editor.NewCard(Ui.Say(StoryWords.NewCardKey), at.X, at.Y);

            if (from != null) Editor.AddBlock(from, Array.Empty<int>(), -1, (_, _) => new Jump { Target = title });

            GD.Print($"workshop card {title} added to {Editor.Id}{(from != null ? $", led to from {from}" : "")}");
            Redraw();
        }

        // a card opened in the editor; `block`, when given, highlighted (Go there, back from a test)
        public void OpenStory(string title, int[] block = null)
        {
            // another card (or the board) starts at its top; the same card keeps its place
            if ((title ?? "") != _openStory) _scrolled["!" + WorkshopTab.Story] = 0;

            _openStory = title ?? "";
            _highlight = block;
            Tab = WorkshopTab.Story;
            Redraw();
        }

        void AskToDeleteCard(string title)
        {
            if (_board == null) return;

            var row = Ui.Row(8, Ui.Label(StoryWords.DeleteCardKey, Draft.Story.NameOf(title)));
            row.Name = "DeleteCardQuestion";
            Button yes = Ui.Button(WorkshopWords.DeleteKey, () => Change(() => Editor.DeleteCard(title)));
            yes.Name = "ReallyDeleteCard";
            Button keep = Ui.Button(WorkshopWords.KeepKey, Redraw);
            row.AddChild(yes);
            row.AddChild(keep);
            _board.GetParent().AddChild(row);
            _board.GetParent().MoveChild(row, _board.GetIndex());
            Ui.FocusLater(keep);
        }

        // Home: the whole board in view, while the board is up
        bool StoryKeys(Act? act)
        {
            if (Tab != WorkshopTab.Story || _openStory.Length > 0 || _board == null || !IsInstanceValid(_board)) return false;

            if (act == Act.FitBoard)
            {
                _board.FitAll();
                return true;
            }

            return false;
        }
    }
}
