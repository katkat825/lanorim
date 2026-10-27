using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Schema;
using Content.Screens;

namespace Content.Workshop
{
    // the five heads the problems panel groups under (cc_task_m Part 5)
    public enum ProblemGroup
    {
        Story,
        Fights,
        Maps,
        Tables,
        About,
    }

    // the campaign page's tabs. M builds three; the table editors (N) and the story editor (O) add theirs, and a
    // problem in their part then gets a Go there of its own
    public enum WorkshopTab
    {
        About,
        Chapters,
        Characters,

        // the story editor (cc_task_o)
        Story,

        // the table editors (cc_task_n)
        Fights,
        Encounters,
        Loot,
        Shops,
        Lucky,
    }

    // ONE PROBLEM, IN PLAIN WORDS: its group, the tab its Go there opens (none until that part's editor exists), the
    // sentence's key and what fills it - or no key, when ProblemWords doesn't know the kind, and the problem's own text
    // is shown instead. Whether it stops the campaign loading is the problem's own (a fault, not a caution)
    public sealed record PlainProblem(ContentProblem Problem, ProblemGroup Group, WorkshopTab? Tab, string Kind, string Key, object[] Args)
    {
        public bool Known => Key != null;

        public bool IsError => Problem.IsAFault;

        public Said Said => Known ? new Said(Key, Args) : null;
    }

    // WHAT A ContentProblem SAYS, AS AN AUTHOR WOULD SAY IT (cc_task_m Part 5; WORKSHOP_DESIGN.md principle 2). The
    // readers write for someone with the JSON open ("chapters.old_mill - 'x' and there is no maps/x.map"); the Workshop
    // author never sees the JSON. A ContentProblem has no kind of its own - it's a file, a place in it and a sentence -
    // so each kind here is recognised by where it is and the shape of its sentence, and the names it quotes fill the
    // plain one. ProblemWordsTests makes each kind from a real broken pack, so a reader that rewords one is caught.
    // One that matches nothing keeps its own words, under the group its folder belongs to
    public static class ProblemWords
    {
        // a kind: where it's found (regexes over the problem's file, its place and its sentence; null is anything), its
        // group and tab, and the named groups that fill its sentence, in order
        sealed record Kind(string Name, ProblemGroup Group, WorkshopTab? Tab, string File, string Where, string What, params string[] Args);

        const string Pack = @"^pack\.json$";
        const string Id = "(?<{0}>[a-z0-9_]+)";

        static string Named(string name) => string.Format(Id, name);

        static readonly Kind[] Kinds =
        {
            // the story, and the companion's lines
            new("story_two_keys", ProblemGroup.Story, WorkshopTab.Story, @"^dialogue/", null, $@"check in '{Named("node")}' has no way past a fail", "node"),
            new("story_hero_variable", ProblemGroup.Story, WorkshopTab.Story, @"^dialogue/", null, @"is the hero's, read-only"),
            new("story_untagged_line", ProblemGroup.Story, WorkshopTab.Story, @"^dialogue/", null, @"has no '#line:' tag"),
            new("story_no_speaker", ProblemGroup.Story, WorkshopTab.Story, @"^dialogue/", null, $@"node '{Named("node")}' has no 'speaker:' header", "node"),
            new("story_missing_node", ProblemGroup.Story, WorkshopTab.Story, @"^dialogue/", null, @"(?i)no node named|missing node|undefined node"),
            new("story_barks", ProblemGroup.Story, null, @"^barks/", null, null),

            // fights
            new("fight_monster", ProblemGroup.Fights, WorkshopTab.Fights, @"^encounters/", null, $@"'{Named("monster")}' is not a monster in this campaign", "monster"),
            new("fight_map", ProblemGroup.Fights, WorkshopTab.Fights, @"^encounters/", null, $@"is fought on '{Named("map")}' and there is no", "map"),
            new("fight_loot", ProblemGroup.Fights, WorkshopTab.Fights, @"^encounters/", null, $@"leaves '{Named("loot")}' and there is no loot table", "loot"),
            new("fight_too_hard", ProblemGroup.Fights, WorkshopTab.Fights, @"^encounters/", null, $@"'{Named("fight")}' is \d+ XP, over a", "fight"),
            new("fight_too_many", ProblemGroup.Fights, WorkshopTab.Fights, @"^encounters/", null, $@"'{Named("fight")}' can field \d+ enemies", "fight"),
            new("fight_no_level", ProblemGroup.Fights, WorkshopTab.Fights, @"^encounters/", null, $@"'{Named("fight")}' says no level", "fight"),

            // maps
            new("map_missing", ProblemGroup.Maps, WorkshopTab.Chapters, Pack, $@"^chapters\.{Named("chapter")}$", $@"is played on '{Named("map")}' and there is no", "chapter", "map"),
            new("map_stand_in", ProblemGroup.Maps, WorkshopTab.Chapters, $@"^maps/{Named("map")}\.map$", "^props$", null, "map"),
            new("map_unplayable", ProblemGroup.Maps, WorkshopTab.Chapters, $@"^maps/{Named("map")}\.map$", null, null, "map"),

            // tables: loot, shops, the lucky and unlucky rolls, and anything defined twice
            new("loot_item", ProblemGroup.Tables, WorkshopTab.Loot, @"^loot/", null, $@"'{Named("item")}' is not an item in this campaign", "item"),
            new("loot_table", ProblemGroup.Tables, WorkshopTab.Loot, @"^loot/", null, $@"rolls '{Named("table")}' and there is no loot table", "table"),
            new("shop_item", ProblemGroup.Tables, WorkshopTab.Shops, @"^merchants$", null, $@"'{Named("item")}' is not an item in this campaign", "item"),
            new("armory_used_up", ProblemGroup.Tables, WorkshopTab.Shops, @"^merchants$", null, $@"'{Named("item")}' is used up, and an armory lends gear", "item"),
            new("armory_unworn", ProblemGroup.Tables, WorkshopTab.Shops, @"^merchants$", null, $@"'{Named("item")}' isn't worn or held", "item"),
            new("roll_taken", ProblemGroup.Tables, WorkshopTab.Lucky, @"^consequences$", null, @"is one of the game's own consequences"),
            new("roll_one_sided", ProblemGroup.Tables, WorkshopTab.Lucky, @"^consequences$", null, @"replaces the game's consequences and has no"),
            new("defined_twice", ProblemGroup.Tables, null, @"^(?<folder>[a-z_]+)$", null, $@"'{Named("id")}' is defined twice in", "folder", "id"),

            // about: pack.json and the folder
            new("about_no_pack", ProblemGroup.About, WorkshopTab.About, null, "^$", @"needs a pack\.json in it"),
            new("about_not_json", ProblemGroup.About, WorkshopTab.About, Pack, "^$", @"this is not JSON"),
            new("about_engine", ProblemGroup.About, WorkshopTab.About, Pack, "^engine$", @"update the game"),
            new("about_format", ProblemGroup.About, WorkshopTab.About, Pack, "^format$", @"content format"),
            new("about_folder_id", ProblemGroup.About, WorkshopTab.About, Pack, "^id$", @"they have to match"),
            new("about_written_name", ProblemGroup.About, WorkshopTab.About, Pack, "^(title|name|description)$", "is not written here"),
            new("about_gm_screen", ProblemGroup.About, WorkshopTab.About, Pack, "^gm_screen$", null),
            new("about_rule", ProblemGroup.About, WorkshopTab.About, Pack, "^(defeat|leveling|characters|starting_shop|consequences)$", null),
            new("about_tag", ProblemGroup.About, WorkshopTab.About, Pack, @"^tags\[\d+\]$", $@"^'(?<tag>[^']*)' is not a tag", "tag"),
            new("chapters_none", ProblemGroup.About, WorkshopTab.Chapters, Pack, "^chapters$", @"(needs chapters|no chapters in it)"),
            new("chapter_empty", ProblemGroup.About, WorkshopTab.Chapters, Pack, @"^chapters\[\d+\]\.maps$", $@"chapter '{Named("chapter")}' has nothing in it", "chapter"),
            new("start_missing", ProblemGroup.About, WorkshopTab.Chapters, Pack, "^start$", null),
            new("unknown_folder", ProblemGroup.About, null, $@"^{Named("folder")}$", "^$", @"is not something a campaign holds", "folder"),
            new("folder_not_loaded", ProblemGroup.About, null, $@"^{Named("folder")}$", "^$", @"does not load yet", "folder"),
            new("needs_pack", ProblemGroup.About, WorkshopTab.About, @"/pack\.json$", "^dependencies$", $@"needs the pack '{Named("pack")}'", "pack"),
            new("same_id", ProblemGroup.About, WorkshopTab.About, null, "^id$", @"is also the id of the pack in"),

            // the Workshop's own (WorkshopProblems)
            new("about_unnamed", ProblemGroup.About, WorkshopTab.About, "^locale$", "^name$", null),
            new("chapter_untitled", ProblemGroup.About, WorkshopTab.Chapters, "^locale$", $@"^chapters\.{Named("chapter")}\.title$", null, "chapter"),
        };

        // every kind's name: one sentence each in the locale (Keys)
        public static IEnumerable<string> Names => Kinds.Select(k => k.Name);

        public static string KeyFor(string kind) => K("problem_" + kind);

        static readonly Func<string, string> K = ScreenKeys.For("workshop");

        public static PlainProblem Plain(ContentProblem problem)
        {
            foreach (Kind kind in Kinds)
            {
                if (!Matches(kind.File, problem.File, out Match file) || !Matches(kind.Where, problem.Where, out Match where) ||
                    !Matches(kind.What, problem.What, out Match what))
                    continue;

                object[] args = kind.Args.Select(name => (object)First(name, where, what, file)).ToArray();

                return new PlainProblem(problem, kind.Group, kind.Tab, kind.Name, KeyFor(kind.Name), args);
            }

            return new PlainProblem(problem, GroupOf(problem.File), null, null, null, Array.Empty<object>());
        }

        public static IReadOnlyList<PlainProblem> Plain(IEnumerable<ContentProblem> problems) =>
            (problems ?? Enumerable.Empty<ContentProblem>()).Select(Plain).ToList();

        static bool Matches(string pattern, string text, out Match match)
        {
            match = null;

            if (pattern == null) return true;

            match = Regex.Match(text ?? "", pattern);
            return match.Success;
        }

        static string First(string name, params Match[] matches) =>
            matches.Where(m => m != null && m.Groups[name].Success).Select(m => m.Groups[name].Value).FirstOrDefault() ?? "";

        // a kind it doesn't know goes under its folder's group
        static ProblemGroup GroupOf(string file)
        {
            string folder = (file ?? "").Split('/')[0];

            return folder switch
            {
                Campaigns.Package.DialogueFolder or Campaigns.Package.BarksFolder => ProblemGroup.Story,
                Campaigns.Package.EncountersFolder => ProblemGroup.Fights,
                Campaigns.Package.MapsFolder => ProblemGroup.Maps,
                Campaigns.Package.LootFolder or Campaigns.Package.MerchantsFolder or Campaigns.Package.ConsequencesFolder or
                    Campaigns.Package.MonstersFolder or Campaigns.Package.ItemsFolder or Campaigns.Package.SpellsFolder => ProblemGroup.Tables,
                _ => ProblemGroup.About,
            };
        }

        public static string GroupKey(ProblemGroup group) => K("group_" + Core.Words.EnumWords.Id(group));

        public static IEnumerable<string> Keys() =>
            Names.Select(KeyFor).Concat(Enum.GetValues<ProblemGroup>().Select(GroupKey));
    }
}
