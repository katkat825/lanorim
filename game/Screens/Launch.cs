using System;
using System.Linq;
using Content.Campaigns;
using Content.Saves;
using Content.Screens;
using Content.Sheet;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE LAUNCH SCREEN - the game's main scene (Tier 3b). The title, then the campaign book (the main
    // menu), the tutorial picker, settings, and character creation; picking a character, or finishing
    // a new one, goes to the table. Everything is built in code from containers and the theme, so
    // the look is ui/lanorim_theme.tres's and the layout is the containers' - Kathleen's to restyle.
    //
    // Headless: `-- --begin <campaign> [class]` makes a character with the defaults and goes straight
    // to the table, which is how check-play drives a whole campaign.
    public partial class Launch : Control
    {
        MarginContainer _body;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            AddChild(new ColorRect { Color = new Color(0.07f, 0.06f, 0.05f), AnchorRight = 1, AnchorBottom = 1 });

            _body = new MarginContainer { AnchorRight = 1, AnchorBottom = 1 };

            foreach (string side in new[] { "left", "right", "top", "bottom" })
                _body.AddThemeConstantOverride("margin_" + side, 48);

            AddChild(_body);

            string[] args = OS.GetCmdlineUserArgs();
            int at = Array.IndexOf(args, "--begin");

            if (at >= 0 && at + 1 < args.Length)
            {
                GameState.SaveProbesApart();
                QuickStart(args[at + 1], at + 2 < args.Length && !args[at + 2].StartsWith("--") ? args[at + 2] : "fighter");
                return;
            }

            // `-- --show book|tutorials|settings|create` opens a screen directly, and `--shot` photographs
            // it - how the screens get looked at without a person clicking through
            int show = Array.IndexOf(args, "--show");
            string screen = show >= 0 && show + 1 < args.Length ? args[show + 1] : "title";

            switch (screen)
            {
                case "book": ShowBook(); break;
                case "tutorials": ShowTutorials(); break;
                case "settings": ShowSettings(); break;
                case "create": ShowCreation(GameState.Manifests.FirstOrDefault()?.Id ?? ""); break;
                default: ShowTitle(); break;
            }

            if (Game.Table.Shot.RequestedFrom(args, out string path, out int after))
                AddChild(new Game.Table.Shot { Name = "Shot", Path = path, After = after });
        }

        void Show(Control screen, float width = 720)
        {
            _onTitle = false;
            Ui.Clear(_body);
            _body.AddChild(Ui.Centred(screen, width));
            Ui.FocusFirst(screen);
        }

        // --- the title --------------------------------------------------------------------------

        void ShowTitle()
        {
            Show(Ui.Panel(Ui.Column(24,
                Ui.Title(ScreenWords.GameTitle),
                Ui.Label(ScreenWords.PressToBegin),
                Ui.Button(ScreenWords.Begin, ShowBook))), 520);
            _onTitle = true;
        }

        bool _onTitle;

        // the title card: any key or click opens the book
        public override void _UnhandledInput(InputEvent @event)
        {
            if (!_onTitle) return;

            if (@event is InputEventKey { Pressed: true } || @event is InputEventMouseButton { Pressed: true })
            {
                GetViewport().SetInputAsHandled();
                ShowBook();
            }
        }

        // --- the book -------------------------------------------------------------------------

        void ShowBook() => ShowBook(null);

        void ShowBook(string open)
        {
            var book = new CampaignBook(GameState.Manifests, GameState.Saves);

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
                Button button = Ui.Button(p.NameKey, () => ShowPage(page, p));

                if (p.LabelKey != null) button.TooltipText = Ui.Say(p.LabelKey);

                contents.AddChild(button);
            }

            BookPage first = book.Pages.FirstOrDefault(p => p.Id == open) ?? book.Pages.FirstOrDefault();

            if (first != null) ShowPage(page, first);

            contents.CustomMinimumSize = new Vector2(300, 0);

            PanelContainer list = Ui.Panel(Ui.Scroll(contents, 340));
            PanelContainer opened = Ui.Panel(Ui.Scroll(page, 340));
            opened.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            Show(Ui.Panel(Ui.Column(16,
                Ui.Title(ScreenWords.GameTitle),
                top,
                Ui.Row(24, list, opened))), 1040);
        }

        void ShowPage(VBoxContainer page, BookPage p)
        {
            Ui.Clear(page);

            page.AddChild(Ui.Title(p.NameKey));

            if (p.LabelKey != null) page.AddChild(Ui.Label(p.LabelKey));

            page.AddChild(Ui.Label(p.DescriptionKey));

            foreach (CharacterSlot slot in p.Characters)
            {
                CharacterSlot s = slot;
                page.AddChild(Ui.Button(Ui.Say(ScreenWords.Character, s.Name, s.Level,
                                               Ui.Say(Core.Localization.KeyConventions.ClassName(s.ClassId))),
                                        () => Load(s.Newest), true));
            }

            page.AddChild(Ui.Button(CampaignBook.NewCharacterKey, () => ShowCreation(p.Id))
                            .Greyed(!p.CanStartNew, CampaignBook.SlotsFullKey));
        }

        // --- tutorials ------------------------------------------------------------------------

        void ShowTutorials()
        {
            var picker = new TutorialPicker(GameState.Manifests);
            var list = Ui.Column(10, Ui.Title(TutorialPicker.TitleKey));

            foreach (TutorialChoice choice in picker.Choices)
            {
                TutorialChoice c = choice;
                list.AddChild(Ui.Button(c.NameKey, () => ShowCreation(c.CampaignId)).Greyed(!c.Available, c.WhyNotKey));
                list.AddChild(Ui.Label(c.BlurbKey));
            }

            if (picker.Tests.Count > 0)
            {
                list.AddChild(Ui.Label(TutorialPicker.TestsKey));

                foreach (BookPage test in picker.Tests)
                {
                    BookPage t = test;
                    list.AddChild(Ui.Button(t.NameKey, () => ShowCreation(t.Id)));
                }
            }

            list.AddChild(Ui.Button(ScreenWords.Back, ShowBook));

            Show(Ui.Panel(list));
        }

        // --- settings -------------------------------------------------------------------------

        void ShowSettings()
        {
            var settings = new SettingsPanel();
            settings.Done += ShowBook;
            Show(Ui.Panel(Ui.Scroll(settings, 560)));
        }

        // --- a character -------------------------------------------------------------------------

        void ShowCreation(string campaign)
        {
            Package pack = GameState.Package(campaign);

            if (pack == null)
            {
                GD.PushError($"launch: no campaign '{campaign}'");
                return;
            }

            int slot = GameState.Saves.FreeSlot(pack.Id);

            if (slot < 0) return;

            var creation = new CreationScreen(GameState.Content);
            creation.Cancelled += () => ShowBook(campaign);
            creation.Finished += hero => Play(pack, hero, slot);

            Show(Ui.Panel(creation), 760);
        }

        void Play(Package pack, Hero hero, int slot)
        {
            GameState.Begin(pack, hero, slot);
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://table.tscn");
        }

        void Load(SaveShelf.Saved saved)
        {
            if (saved == null || GameState.Resume(saved.Game) == null) return;

            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://table.tscn");
        }

        // a character made with every default, for the headless checks
        void QuickStart(string campaign, string cls)
        {
            Package pack = GameState.Package(campaign);

            if (pack == null)
            {
                GD.PushError($"launch: no campaign '{campaign}' - found " +
                             string.Join(", ", GameState.Manifests.Select(m => m.Id)));
                GetTree().Quit(1);
                return;
            }

            var creation = new CreationScreen(GameState.Content);
            var making = creation.Making;

            making.Pick(GameState.Content.Class(cls));
            making.Pick(GameState.Content.Kind("human"));
            making.Pick(GameState.Content.Background("soldier"));

            foreach (var skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList()) making.Train(skill);
            foreach (var skill in making.Skills.Take(making.ExpertisePicksLeft).ToList()) making.Master(skill);
            foreach (var spell in making.SpellChoices.ToList())
                if (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0) making.Learn(spell);

            making.Call("Probe");

            Hero hero = making.Finish();

            if (hero == null)
            {
                GD.PushError("launch: " + string.Join("; ", making.Problems));
                GetTree().Quit(1);
                return;
            }

            GD.Print($"launch  {hero} in {pack.Id}");

            Play(pack, hero, 0);
        }
    }
}
