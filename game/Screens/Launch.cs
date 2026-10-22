using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Saves;
using Content.Sheet;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE LAUNCH SCREEN - the game's main scene (Tier 3b). The title, then the campaign book (the main
    // menu), the book's other pages (BookMenu: tutorials, the map builder, settings, credits), and character
    // creation; picking a character, or finishing a new one, goes to the table. Everything is built in code from
    // containers and the theme, so the look is ui/lanorim_theme.tres's and the layout is the containers' - Kathleen's
    // to restyle.
    //
    // Headless: `-- --begin <campaign> [class]` makes a character with the defaults and goes straight
    // to the table, which is how check-play drives a whole campaign (the dev registry, Dev/DevHooks.cs).
    public partial class Launch : Control
    {
        MarginContainer _body;

        // the book on the table behind the screens (book.tscn): closed at the title, open under the rest
        BookTable _book;

        // the book's other pages, this screen's own set (BookMenu)
        IReadOnlyList<IBookPage> _pages;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            // the player's own keys, before anything reads one
            _ = GameState.Access;

            // the campaign book on the table, when the scene is there; a plain dark ground when it isn't
            if (ResourceLoader.Exists(BookScene) && GD.Load<PackedScene>(BookScene)?.Instantiate() is BookTable book)
            {
                _book = book;
                AddChild(_book);
            }
            else AddChild(new ColorRect { Color = new Color(0.07f, 0.06f, 0.05f), AnchorRight = 1, AnchorBottom = 1 });

            _body = new MarginContainer { AnchorRight = 1, AnchorBottom = 1 };

            foreach (string side in new[] { "left", "right", "top", "bottom" })
                _body.AddThemeConstantOverride("margin_" + side, (int)Ui.Px(48));

            AddChild(_body);

            _pages = BookMenu.Make();

            // the dev registry (Dev/DevHooks.cs) takes the start as the command line asks: a probe, a quick start, the
            // map builder, or the first screen with a picture or a probe after it. A release build has no registry,
            // and opens the first screen itself: `--show book` is the one start-up flag it keeps (check-export.ps1)
            if (!Game.Dev.DevHooks.AtLaunch(this)) ShowFirst(DevArgs.Show, DevArgs.Open);
        }

        public const string BookScene = "res://book.tscn";

        // the screen the game opens on: the title, the book (open at a campaign's pages), or one of the book's pages
        // what GameState.BackTo says for the book's main menu (the map builder's File, Main menu)
        public const string MainMenu = "book";

        internal void ShowFirst(string screen, string open)
        {
            if (GameState.BackTo == MainMenu)
            {
                GameState.BackTo = null;
                ShowBook();
                return;
            }

            // back from the table to the page that sent it there: the map builder opened from the Workshop goes back to it
            if (GameState.BackTo is { } back && PageCalled(back) is { } from)
            {
                GameState.BackTo = null;
                from.Open(this);
                return;
            }

            if (screen == "book") ShowBook(open);
            else if (PageCalled(screen) is { } page) page.Open(this, open);
            else ShowTitle();
        }

        // one of the book's other pages, by its id, or null
        internal IBookPage PageCalled(string id) => _pages.FirstOrDefault(p => p.Id == id);

        internal IEnumerable<IBookPage> Pages => _pages;

        // where the screens go (a probe looks in it)
        internal MarginContainer Body => _body;

        // --- a character -------------------------------------------------------------------------

        internal void ShowCreation(string campaign)
        {
            Package pack = GameState.Package(campaign);

            if (pack == null)
            {
                GD.PushError($"launch: no campaign '{campaign}'");
                return;
            }

            int slot = GameState.Saves.FreeSlot(pack.Id, pack.Manifest?.Rules.Characters ?? Content.Saves.SaveLibrary.CharactersPerCampaign);

            if (slot < 0) return;

            var creation = new CreationScreen(GameState.Content);
            creation.Cancelled += () => ShowBook(campaign);
            creation.Finished += hero => Play(pack, hero, slot);

            Show(Ui.Panel(creation), 900);

            // the book goes while a character is made, and Clear brings it back (cc_task_m2 Part 1)
            if (_book != null) _book.Hidden = true;
        }

        internal void Play(Package pack, Hero hero, int slot)
        {
            GameState.Begin(pack, hero, slot);
            Scenes.Go(GetTree(), Scenes.Table);
        }

        void Load(SaveShelf.Saved saved) => Scenes.Resume(GetTree(), saved);
    }
}
