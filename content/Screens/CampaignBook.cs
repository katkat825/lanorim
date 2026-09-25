using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Saves;
using Core.Localization;

namespace Content.Screens
{
    // THE KEYS THE SCREENS SAY THINGS WITH - every label a screen shows that is not content's own
    // name. ui.<screen>.<thing>; English in game/locale/game.csv, and the locale audit walks All.
    public static class ScreenKeys
    {
        public const string Ns = "ui";

        public static string Key(string screen, string thing) => KeyConventions.Key(Ns, screen, thing);

        public static IEnumerable<string> All() =>
            CampaignBook.Keys()
                .Concat(TutorialPicker.Keys())
                .Concat(LevelUpView.Keys())
                .Concat(PackView.Keys())
                .Concat(DialogueView.Keys())
                .Concat(CombatHud.Keys())
                .Concat(GameSettings.Keys())
                .Concat(DeathView.Keys())
                .Concat(Maps.MapEditor.Keys())
                .Concat(Maps.PropCatalogue.Srd().Keys())
                .Distinct();
    }

    public enum PageKind
    {
        // a campaign to play
        Campaign,

        // a tutorial - reached from the tutorial picker, not the book's own pages
        Tutorial,

        // a test campaign: labelled as one, and at the back of the book
        Test,
    }

    // one of the five characters a campaign keeps
    public sealed class CharacterSlot
    {
        public int Slot { get; init; }

        public string Name { get; init; } = "";

        public string ClassId { get; init; } = "";

        public int Level { get; init; }

        public DateTime Saved { get; init; }

        public SaveShelf.Saved Newest { get; init; }
    }

    // A PAGE OF THE BOOK: one campaign, its characters, and whether a new one can start
    public sealed class BookPage
    {
        public string Id { get; init; } = "";

        public string NameKey { get; init; } = "";

        public string DescriptionKey { get; init; } = "";

        public PageKind Kind { get; init; }

        public IReadOnlyList<CharacterSlot> Characters { get; init; } = Array.Empty<CharacterSlot>();

        public bool CanStartNew => Characters.Count < SaveLibrary.CharactersPerCampaign;

        // the label on a test campaign's page: it is not a real one
        public string LabelKey => Kind == PageKind.Test ? CampaignBook.TestLabel : null;

        public override string ToString() => $"{Id} [{Kind}] {Characters.Count} characters";
    }

    // THE MAIN MENU AS A CAMPAIGN BOOK (Tier 2.8): each playable campaign a page, in the order they
    // were found; tutorials behind the tutorial picker; test campaigns at the back, labelled.
    // Continue is the newest save anywhere.
    public sealed class CampaignBook
    {
        public const string TestTag = "test";

        public static readonly string TestLabel = ScreenKeys.Key("book", "test_campaign");

        public CampaignBook(IEnumerable<Manifest> campaigns, SaveLibrary saves = null)
        {
            var pages = new List<BookPage>();

            foreach (Manifest manifest in (campaigns ?? Enumerable.Empty<Manifest>())
                                              .Where(m => m != null && m.IsPlayable))
                pages.Add(new BookPage
                {
                    Id = manifest.Id,
                    NameKey = manifest.NameKey,
                    DescriptionKey = manifest.DescriptionKey,
                    Kind = KindOf(manifest),
                    Characters = Characters(saves, manifest.Id),
                });

            // stable: real campaigns keep their found order, tests go to the back
            Pages = pages.Where(p => p.Kind == PageKind.Campaign)
                         .Concat(pages.Where(p => p.Kind == PageKind.Test))
                         .ToList();

            Tutorials = pages.Where(p => p.Kind == PageKind.Tutorial).ToList();

            Continue = saves?.Newest();
        }

        public static PageKind KindOf(Manifest manifest) =>
            manifest.Tags.Contains(TestTag) ? PageKind.Test
            : TutorialPicker.LevelOf(manifest).HasValue ? PageKind.Tutorial
            : PageKind.Campaign;

        static IReadOnlyList<CharacterSlot> Characters(SaveLibrary saves, string campaign) =>
            saves == null
                ? Array.Empty<CharacterSlot>()
                : saves.Characters(campaign)
                       .Select(pair => new CharacterSlot
                       {
                           Slot = pair.Key,
                           Name = pair.Value.Game.Hero?.Name ?? "",
                           ClassId = pair.Value.Game.Hero?.Class ?? "",
                           Level = pair.Value.Game.Hero?.Level ?? 0,
                           Saved = pair.Value.Written,
                           Newest = pair.Value,
                       })
                       .ToList();

        // the book's pages - campaigns, then the test campaigns
        public IReadOnlyList<BookPage> Pages { get; }

        public IReadOnlyList<BookPage> Tutorials { get; }

        public BookPage Page(string id) => Pages.Concat(Tutorials).FirstOrDefault(p => p.Id == id);

        public SaveShelf.Saved Continue { get; }

        public bool CanContinue => Continue != null;

        public static readonly string ContinueKey = ScreenKeys.Key("book", "continue");
        public static readonly string NewCharacterKey = ScreenKeys.Key("book", "new_character");
        public static readonly string LoadKey = ScreenKeys.Key("book", "load");
        public static readonly string TutorialsKey = ScreenKeys.Key("book", "tutorials");
        public static readonly string SettingsKey = ScreenKeys.Key("book", "settings");
        public static readonly string QuitKey = ScreenKeys.Key("book", "quit");
        public static readonly string SlotsFullKey = ScreenKeys.Key("book", "slots_full");
        public static readonly string EmptyKey = ScreenKeys.Key("book", "empty");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                TestLabel, ContinueKey, NewCharacterKey, LoadKey, TutorialsKey, SettingsKey, QuitKey,
                SlotsFullKey, EmptyKey,
            };

        public override string ToString() => $"{Pages.Count} pages, {Tutorials.Count} tutorials";
    }

    public enum TutorialLevel
    {
        Beginner,
        Intermediate,
        Advanced,
    }

    public sealed class TutorialChoice
    {
        public TutorialLevel Level { get; init; }

        public string NameKey { get; init; } = "";

        public string BlurbKey { get; init; } = "";

        // the campaign that teaches it, or empty while none is installed
        public string CampaignId { get; init; } = "";

        public bool Available => CampaignId.Length > 0;

        public string WhyNotKey => Available ? null : TutorialPicker.NotYetKey;
    }

    // THE TUTORIAL PICKER (Tier 2.8): beginner, intermediate, advanced - each a campaign tagged
    // tutorial_beginner / _intermediate / _advanced - and the test campaigns below them, labelled,
    // so a tester reaches one from here without it being a page of the book
    public sealed class TutorialPicker
    {
        public const string TagPrefix = "tutorial_";

        public static string Tag(TutorialLevel level) => TagPrefix + level.ToString().ToLowerInvariant();

        public static TutorialLevel? LevelOf(Manifest manifest)
        {
            foreach (TutorialLevel level in Enum.GetValues<TutorialLevel>())
                if (manifest.Tags.Contains(Tag(level))) return level;

            return null;
        }

        public TutorialPicker(IEnumerable<Manifest> campaigns)
        {
            List<Manifest> all = (campaigns ?? Enumerable.Empty<Manifest>())
                                 .Where(m => m != null && m.IsPlayable).ToList();

            Choices = Enum.GetValues<TutorialLevel>()
                          .Select(level => new TutorialChoice
                          {
                              Level = level,
                              NameKey = NameKey(level),
                              BlurbKey = BlurbKey(level),
                              CampaignId = all.FirstOrDefault(m => LevelOf(m) == level)?.Id ?? "",
                          })
                          .ToList();

            Tests = all.Where(m => m.Tags.Contains(CampaignBook.TestTag))
                       .Select(m => new BookPage
                       {
                           Id = m.Id,
                           NameKey = m.NameKey,
                           DescriptionKey = m.DescriptionKey,
                           Kind = PageKind.Test,
                       })
                       .ToList();
        }

        public IReadOnlyList<TutorialChoice> Choices { get; }

        public IReadOnlyList<BookPage> Tests { get; }

        public static string NameKey(TutorialLevel level) =>
            ScreenKeys.Key("tutorial", level.ToString().ToLowerInvariant());

        public static string BlurbKey(TutorialLevel level) =>
            ScreenKeys.Key("tutorial", level.ToString().ToLowerInvariant() + "_blurb");

        public static readonly string NotYetKey = ScreenKeys.Key("tutorial", "not_yet");
        public static readonly string TestsKey = ScreenKeys.Key("tutorial", "tests");
        public static readonly string TitleKey = ScreenKeys.Key("tutorial", "title");

        public static IEnumerable<string> Keys() =>
            Enum.GetValues<TutorialLevel>().SelectMany(l => new[] { NameKey(l), BlurbKey(l) })
                .Concat(new[] { NotYetKey, TestsKey, TitleKey });
    }
}
