using System;
using System.Linq;
using Content.Campaigns;
using Content.Creation;
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

        // the book on the table behind the screens (book.tscn): closed at the title, open under the rest
        BookTable _book;

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

            string[] args = OS.GetCmdlineUserArgs();

            if (args.Contains(ControlsProbe.Flag))
            {
                AddChild(new ControlsProbe { Name = "ControlsProbe" });
                return;
            }

            if (args.Contains(CreationProbe.Flag))
            {
                AddChild(new CreationProbe { Name = "CreationProbe" });
                return;
            }

            int at = Array.IndexOf(args, "--begin");

            if (at >= 0 && at + 1 < args.Length)
            {
                GameState.SaveProbesApart();
                QuickStart(args[at + 1], at + 2 < args.Length && !args[at + 2].StartsWith("--") ? args[at + 2] : "fighter",
                           Arg(args, "--learn"));
                return;
            }

            // `-- --show book|tutorials|settings|create` opens a screen directly, and `--shot` photographs
            // it - how the screens get looked at without a person clicking through
            int show = Array.IndexOf(args, "--show");
            string screen = show >= 0 && show + 1 < args.Length ? args[show + 1] : "title";

            // `--book-overlay`: the menu on the 10-01 panels over the book, as when the pages are too small;
            // `--show book --open sample_millbrook`: that campaign's two pages
            _overlayBook = args.Contains("--book-overlay");

            switch (screen)
            {
                case "book": ShowBook(Arg(args, "--open")); break;
                case "tutorials": ShowTutorials(); break;
                case "settings": ShowSettings(); break;
                case "controls": ShowSettings(); ScrollToControls(); break;
                case "create": ShowCreation(GameState.Manifests.FirstOrDefault()?.Id ?? "", Arg(args, "--page"), Arg(args, "--class")); break;
                default: ShowTitle(); break;
            }

            // `--show book --ask-delete`: the first character's Delete pressed, for a picture of the
            // question (it asks; nothing is deleted)
            if (screen == "book" && args.Contains("--ask-delete"))
                Nodes.Under<Button>(this).FirstOrDefault(b => b.Text == Ui.Say(CampaignBook.DeleteKey))
                     ?.EmitSignal(BaseButton.SignalName.Pressed);

            if (Game.Table.Shot.From(args) is { } shot) AddChild(shot);

            // every launch screen at every window size (checks/check-layout.ps1)
            if (args.Contains(LayoutProbe.Flag))
            {
                var probe = new LayoutProbe { Name = "LayoutProbe" };
                string campaign = GameState.Manifests.FirstOrDefault()?.Id ?? "";

                probe.Screens.Add(("title", ShowTitle));
                probe.Screens.Add(("book", ShowBook));
                probe.Screens.Add(("tutorials", ShowTutorials));
                probe.Screens.Add(("settings", ShowSettings));
                probe.Screens.Add(("create", () => ShowCreation(campaign, "class", null)));
                probe.Screens.Add(("spells", () => ShowCreation(campaign, "spells", "mage")));
                probe.Screens.Add(("abilities", () => ShowCreation(campaign, "abilities", "mage")));
                AddChild(probe);
            }
        }

        public const string BookScene = "res://book.tscn";

        // --- tutorials ------------------------------------------------------------------------

        void ShowTutorials()
        {
            var picker = new TutorialPicker(GameState.Manifests);
            var list = Ui.Column(10, Ui.Title(TutorialPicker.TitleKey));

            foreach (TutorialChoice choice in picker.Choices)
            {
                TutorialChoice c = choice;
                list.AddChild(Ui.Button(c.NameKey, () => ShowCreation(c.CampaignId)).Greyed(!c.Available, c.WhyNotKey));
                list.AddChild(Ui.Why(!c.Available, c.WhyNotKey));
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
            Show(Ui.Panel(SettingsPanel.Scrolled(ShowBook, 560)));
        }

        // the settings page scrolled down to its Controls (`--show controls`, for a picture of them)
        void ScrollToControls()
        {
            ScrollContainer scroll = Nodes.Under<ScrollContainer>(_body).FirstOrDefault();

            if (scroll == null) return;

            int frames = 0;

            // a few frames: the page lays itself out in the first
            GetTree().ProcessFrame += Once;

            void Once()
            {
                if (++frames < 3) return;

                GetTree().ProcessFrame -= Once;

                if (Nodes.Under<ControlsSection>(scroll).FirstOrDefault() is { } controls)
                    scroll.ScrollVertical = (int)controls.Position.Y;
            }
        }

        // --- a character -------------------------------------------------------------------------

        // `--show create --page spells --class mage`: a creation page part filled in, for a picture of it
        static string Arg(string[] args, string flag)
        {
            int at = Array.IndexOf(args, flag);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        void ShowCreation(string campaign, string page, string cls)
        {
            ShowCreation(campaign);

            if (page == null || !Core.Words.EnumWords.TryName(page, out Step step)) return;

            CreationScreen creation = Nodes.Under<CreationScreen>(this).FirstOrDefault();
            if (creation == null) return;

            Defaults(creation.Making, cls ?? "mage", learn: false);
            creation.Open(step);
        }

        // every default a quick character takes: class, human, soldier, the first skills and expertise,
        // and (learn) the first spells
        static void Defaults(Creation making, string cls, bool learn)
        {
            // picking a class starts it over, so a class already picked (with spells learned) is kept
            if (making.Class?.Id != cls) making.Pick(GameState.Content.Class(cls));
            making.Pick(GameState.Content.Kind("human"));
            making.Pick(GameState.Content.Background("soldier"));

            foreach (var skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList()) making.Train(skill);
            foreach (var skill in making.Skills.Take(making.ExpertisePicksLeft).ToList()) making.Master(skill);

            if (!learn) return;

            foreach (var spell in making.SpellChoices.ToList())
                if (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0) making.Learn(spell);
        }

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
            Scenes.Go(GetTree(), Scenes.Table);
        }

        void Load(SaveShelf.Saved saved) => Scenes.Resume(GetTree(), saved);

        // a character made with every default, for the headless checks
        // `--learn mage_armor,shield`: those spells learned first, the rest of the picks as usual (a
        // probe or a picture that needs a particular spell)
        void QuickStart(string campaign, string cls, string learn = null)
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

            foreach (string id in (learn ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (making.SpellChoices.FirstOrDefault(s => s.Id == id) is { } wanted) making.Learn(wanted);
                else GD.PushWarning($"launch: '{id}' is not a spell a level 1 {cls} can learn");

            Defaults(making, cls, learn: true);

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
