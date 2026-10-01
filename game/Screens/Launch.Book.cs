using System;
using System.Linq;
using Content.Screens;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE BOOK: the closed cover at the title, the open pages as the menu (cc_task_ui-issues-10-01.md 3).
    // Kathleen: "put Lanorim as if it's a book title on the book", "ditch the ui text block and just put a
    // Begin button on the cover", and "put the buttons on halves of the book as if they were a table of
    // contents". The title is BookTable's, Begin is BookCover's, the pages are BookPages'.
    //
    // THE FALLBACK. The pages carry the menu only while their letters can be read (BookTable.ContentsFontMin);
    // below that, or with `--book-overlay`, the menu is the 10-01 panels laid over the open book, and the log
    // says why. With no book scene at all it is the panels on a dark ground.
    public partial class Launch
    {
        // the overlay instead of the pages: asked for (`--book-overlay`), or the pages came out too small
        bool _overlayBook;

        // what stands over the whole screen (the cover's Begin, the pages' words), cleared with the screen
        Control _layer;

        void Show(Control screen, float width = 720)
        {
            Clear();
            _book?.Open();
            _body.AddChild(Ui.Centred(screen, width));
            Ui.FocusFirst(screen);
        }

        void Clear()
        {
            _onTitle = false;
            Ui.Clear(_body);
            _layer?.QueueFree();
            _layer = null;
        }

        void Layer(Control layer)
        {
            _layer = layer;
            AddChild(layer);
        }

        // --- the title: the closed book ---------------------------------------------------------------

        void ShowTitle()
        {
            Clear();

            if (_book == null)
            {
                // no book on the table: the old card, so there is still a way in
                Show(Ui.Panel(Ui.Column(24, Ui.Title(ScreenWords.GameTitle), Ui.Button(ScreenWords.Begin, ShowBook))), 520);
            }
            else
            {
                _book.Close();
                Layer(new BookCover(_book, ShowBook) { Name = "BookCover" });
            }

            _onTitle = true;
        }

        bool _onTitle;

        // on the cover, any key or click opens the book too (Enter and Space press Begin, which has the focus)
        public override void _UnhandledInput(InputEvent @event)
        {
            if (!_onTitle) return;

            if (@event is InputEventKey { Pressed: true } || @event is InputEventMouseButton { Pressed: true })
            {
                GetViewport().SetInputAsHandled();
                ShowBook();
            }
        }

        // --- the open book: the contents ----------------------------------------------------------------

        void ShowBook() => ShowBook(null);

        // the contents, or (open) that campaign's two pages
        void ShowBook(string open)
        {
            var book = new CampaignBook(GameState.Manifests, GameState.Saves);

            if (_book == null || _overlayBook)
            {
                ShowBookPanel(book, open);
                return;
            }

            if (book.Pages.FirstOrDefault(p => p.Id == open) is BookPage page)
            {
                ShowCampaign(page);
                return;
            }

            var contents = new BookContents(book);

            Spread((left, right) =>
            {
                foreach (ContentsEntry entry in contents.Left) left.AddChild(Entry(entry, book));
                foreach (ContentsEntry entry in contents.Right) right.AddChild(Entry(entry, book));
            }, () => ShowBookPanel(book, open));
        }

        // a line of the contents: its words as a plain text button, and a dotted leader to the page's edge
        Control Entry(ContentsEntry entry, CampaignBook book)
        {
            Button name = Ui.Button(entry.NameKey, () => Open(entry, book));
            name.Flat = true;
            name.Alignment = HorizontalAlignment.Left;

            var leader = new Label
            {
                Text = string.Concat(Enumerable.Repeat(" .", 120)),
                ClipText = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(1f, 1f, 1f, 0.55f),
            };

            var row = new HBoxContainer { Name = entry.ToString() };
            row.AddThemeConstantOverride("separation", 6);
            row.AddChild(name);
            row.AddChild(leader);
            return row;
        }

        void Open(ContentsEntry entry, CampaignBook book)
        {
            switch (entry.Kind)
            {
                case ContentsKind.Continue: Load(book.Continue); break;
                case ContentsKind.Campaign: ShowCampaign(entry.Page); break;
                case ContentsKind.Tutorials: ShowTutorials(); break;
                case ContentsKind.Settings: ShowSettings(); break;
                case ContentsKind.Quit: GetTree().Quit(); break;
            }
        }

        // a campaign's two pages: its name and what it is on the left, its characters on the right
        void ShowCampaign(BookPage p)
        {
            Spread((left, right) =>
            {
                left.AddChild(Ui.Title(p.NameKey));
                About(left, p);
                Heroes(right, p, () => ShowBook(p.Id));
                right.AddChild(Ui.Button(CampaignBook.ContentsKey, ShowBook));
            }, () => ShowBookPanel(new CampaignBook(GameState.Manifests, GameState.Saves), p.Id));
        }

        // the open book with words on its pages; too small to read, and fallback() shows the panels instead
        void Spread(Action<VBoxContainer, VBoxContainer> fill, Action fallback)
        {
            Clear();
            _book.Open();

            var pages = new BookPages(_book) { Name = "BookPages" };
            fill(pages.Left, pages.Right);

            pages.TooSmall += size =>
            {
                GD.Print($"book    the pages' letters would be {size} px, under ContentsFontMin " +
                         $"({_book.ContentsFontMin}): the menu goes back to panels over the book");
                _overlayBook = true;
                fallback();
            };

            Layer(pages);
            Ui.FocusFirst(pages.Left);
        }

        // --- what a campaign's page says, on the book or on a panel -----------------------------------------

        static void About(VBoxContainer page, BookPage p)
        {
            if (p.LabelKey != null) page.AddChild(Ui.Label(p.LabelKey));

            page.AddChild(Ui.Label(p.DescriptionKey));
        }

        // its characters, each with Delete, and New character; again() shows the page afresh
        void Heroes(VBoxContainer page, BookPage p, Action again)
        {
            foreach (CharacterSlot slot in p.Characters)
            {
                CharacterSlot s = slot;
                Button load = Ui.Button(Ui.Say(ScreenWords.Character, s.Name, s.Level,
                                               Ui.Say(Core.Localization.KeyConventions.ClassName(s.ClassId))),
                                        () => Load(s.Newest), true);
                load.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                load.AutowrapMode = TextServer.AutowrapMode.WordSmart;

                var row = Ui.Row(8, load);
                row.AddChild(Ui.Button(CampaignBook.DeleteKey, () => AskToDelete(row, p, s, again)));
                page.AddChild(row);
            }

            page.AddChild(Ui.Button(CampaignBook.NewCharacterKey, () => ShowCreation(p.Id))
                            .Greyed(!p.CanStartNew, CampaignBook.SlotsFullKey));
            page.AddChild(Ui.Why(!p.CanStartNew, CampaignBook.SlotsFullKey));
        }

        // DELETE A CHARACTER, ASKED FIRST (cc_task_ui-issues-9-30.md 6.2): the row becomes the question -
        // the hero's name and how many saves go with it - with Keep, the safe answer, focused
        void AskToDelete(HBoxContainer row, BookPage p, CharacterSlot s, Action again)
        {
            Ui.Clear(row);

            (string key, object[] args) = CampaignBook.DeleteQuestion(s);
            Label question = Ui.Label(key, args);
            question.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            Button keep = Ui.Button(CampaignBook.KeepKey, again);
            Button delete = Ui.Button(CampaignBook.DeleteKey, () =>
            {
                int gone = CampaignBook.Delete(GameState.Saves, p.Id, s);
                GD.Print($"launch  deleted {s.Name} from {p.Id} slot {s.Slot}: {gone} saves");
                again();
            });

            row.AddChild(question);
            row.AddChild(delete);
            row.AddChild(keep);
            Ui.FocusLater(keep);
        }

        // --- the fallback: the 10-01 panels over the open book -------------------------------------------

        void ShowBookPanel(CampaignBook book, string open)
        {
            var top = Ui.Row(12);

            if (book.CanContinue)
                top.AddChild(Ui.Button(CampaignBook.ContinueKey, () => Load(book.Continue)));

            top.AddChild(Ui.Button(CampaignBook.TutorialsKey, ShowTutorials));
            top.AddChild(Ui.Button(CampaignBook.SettingsKey, ShowSettings));
            top.AddChild(Ui.Button(CampaignBook.QuitKey, () => GetTree().Quit()));

            var contents = Ui.Column(6);
            var page = Ui.Column(10);
            page.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            if (book.Pages.Count == 0) contents.AddChild(Ui.Label(CampaignBook.EmptyKey));

            foreach (BookPage one in book.Pages)
            {
                BookPage p = one;
                // what the page's label says is on the page itself, not on hover
                contents.AddChild(Ui.Button(p.NameKey, () => ShowPanelPage(page, p)));
            }

            BookPage first = book.Pages.FirstOrDefault(p => p.Id == open) ?? book.Pages.FirstOrDefault();

            if (first != null) ShowPanelPage(page, first);

            contents.CustomMinimumSize = new Vector2(Ui.Px(300), 0);

            PanelContainer list = Ui.Panel(Ui.Scroll(contents, 340));
            PanelContainer opened = Ui.Panel(Ui.Scroll(page, 340));
            opened.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            Show(Ui.Panel(Ui.Column(16,
                Ui.Title(ScreenWords.GameTitle),
                top,
                Ui.Row(24, list, opened))), 1040);
        }

        void ShowPanelPage(VBoxContainer page, BookPage p)
        {
            Ui.Clear(page);

            page.AddChild(Ui.Title(p.NameKey));
            About(page, p);
            Heroes(page, p, () => ShowBook(p.Id));
        }
    }
}
