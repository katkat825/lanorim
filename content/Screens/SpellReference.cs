using System;
using System.Collections.Generic;
using System.Linq;
using Content.Spells;
using Core.Localization;
using Core.Magic;

namespace Content.Screens
{
    // THE SPELLS PAGE (cc_task_cs 2.3): every spell the game ships, by level, narrowed by class, by level and by what's
    // typed in the search, each opening its card. The checklist's "~339 SRD" reference cards are the SRD's whole
    // list; only v1's spells are in the game's data, so this lists those (docs/v1_spell_list.md), and a spell M4 cuts
    // leaves the page when it leaves the data.
    //
    // `say` turns a key into the player's words: the search matches the names they read, in their language
    public sealed class SpellReference
    {
        readonly SpellBook _book;
        readonly Func<string, string> _say;

        public SpellReference(SpellBook book, Func<string, string> say)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));
            _say = say ?? (key => key);
        }

        public int Count => _book.All.Count();

        // the classes with a spell list, in the order the classes file gives them is not known here: by name
        public IReadOnlyList<string> Classes =>
            _book.All.SelectMany(s => s.Classes)
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .OrderBy(c => _say(KeyConventions.ClassName(c)), StringComparer.CurrentCulture)
                 .ToList();

        public IReadOnlyList<int> Levels => _book.All.Select(s => s.Level).Distinct().OrderBy(l => l).ToList();

        // null is every class, every level
        public string Class { get; set; }

        public int? Level { get; set; }

        public string Search { get; set; } = "";

        // what the page shows: a heading per level, the spells under it by name
        public IReadOnlyList<(int Level, IReadOnlyList<Spell> Spells)> Shown =>
            _book.All.Where(Matches)
                 .GroupBy(s => s.Level)
                 .OrderBy(g => g.Key)
                 .Select(g => (g.Key, (IReadOnlyList<Spell>)g.OrderBy(s => _say(s.NameKey), StringComparer.CurrentCulture)
                                                               .ToList()))
                 .ToList();

        public int ShownCount => Shown.Sum(g => g.Spells.Count);

        bool Matches(Spell spell) =>
            (Class == null || spell.Classes.Contains(Class, StringComparer.OrdinalIgnoreCase)) &&
            (Level == null || spell.Level == Level) &&
            (string.IsNullOrWhiteSpace(Search) ||
             _say(spell.NameKey).Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase));

        public static readonly string TitleKey = ScreenKeys.Key("spells", "title");
        public static readonly string SearchKey = ScreenKeys.Key("spells", "search");
        public static readonly string AllClassesKey = ScreenKeys.Key("spells", "all_classes");
        public static readonly string AllLevelsKey = ScreenKeys.Key("spells", "all_levels");
        public static readonly string CountKey = ScreenKeys.Key("spells", "count");
        public static readonly string NoneKey = ScreenKeys.Key("spells", "none");

        public static IEnumerable<string> Keys() => new[] { TitleKey, SearchKey, AllClassesKey, AllLevelsKey, CountKey, NoneKey };
    }
}
