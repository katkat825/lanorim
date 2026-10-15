using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Maps;
using Content.Schema;
using Content.Workshop;

namespace Content.Tests
{
    // cc_task_m Part 5: the problems panel says each ContentProblem in plain words, grouped Story, Fights, Maps, Tables
    // and About, with Go there. A ContentProblem has no kind, so each kind is recognised from real broken packs here:
    // a reader that rewords one fails this test rather than quietly falling back to its own words
    public class ProblemWordsTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_problems_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        string Write(string campaign, string file, string text)
        {
            string path = Path.Combine(_root, campaign, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }

        static string Map(string props = "") =>
            MapBuilderViewSave(props);

        static string MapBuilderViewSave(string props)
        {
            MapDraft draft = Screens.MapBuilderView.Fresh(8, 6, "dungeon_room").Draft;
            string text = draft.Save();
            return props.Length == 0 ? text : text.Replace("\"props\": []", $"\"props\": [ {props} ]");
        }

        IReadOnlyList<PlainProblem> Plain(string campaign) => ProblemWords.Plain(WorkshopProblems.Of(Path.Combine(_root, campaign)));

        static void Has(IReadOnlyList<PlainProblem> plain, string kind, ProblemGroup group, params object[] args)
        {
            PlainProblem found = plain.FirstOrDefault(p => p.Kind == kind);

            Assert.True(found != null, $"no '{kind}' among: " + string.Join("\n", plain.Select(p => $"{p.Kind ?? "UNKNOWN"}: {p.Problem}")));
            Assert.Equal(group, found.Group);
            Assert.Equal(ProblemWords.KeyFor(kind), found.Key);
            if (args.Length > 0) Assert.Equal(args, found.Args);
        }

        [Fact]
        public void APackJsonsProblemsAreSaidPlainly()
        {
            Write("broken_folder", "pack.json",
                "{ \"id\": \"broken_about\", \"kind\": \"campaign\", \"format\": 1, \"engine\": \"9.9\", \"name\": \"x\", " +
                "\"tags\": [ \"Two Words\" ], \"gm_screen\": \"lava\", \"defeat\": \"explode\", " +
                "\"chapters\": [ { \"id\": \"one\", \"maps\": [] } ], \"start\": \"nope\" }");

            var plain = Plain("broken_folder");

            Has(plain, "about_engine", ProblemGroup.About);
            Has(plain, "about_written_name", ProblemGroup.About);
            Has(plain, "about_tag", ProblemGroup.About, "Two Words");
            Has(plain, "about_gm_screen", ProblemGroup.About);
            Has(plain, "about_rule", ProblemGroup.About);
            Has(plain, "chapter_empty", ProblemGroup.About, "one");
            Has(plain, "start_missing", ProblemGroup.About);
            Has(plain, "about_folder_id", ProblemGroup.About);
            Assert.All(plain, p => Assert.True(p.Known, p.Problem.ToString()));
            Assert.All(plain, p => Assert.True(p.IsError));

            Write("not_json", "pack.json", "{ not json");
            Has(Plain("not_json"), "about_not_json", ProblemGroup.About);

            Directory.CreateDirectory(Path.Combine(_root, "no_pack"));
            Has(Plain("no_pack"), "about_no_pack", ProblemGroup.About);

            Write("no_chapters", "pack.json", "{ \"id\": \"no_chapters\", \"kind\": \"campaign\", \"format\": 1, \"engine\": \"0.1\" }");
            Has(Plain("no_chapters"), "chapters_none", ProblemGroup.About);
        }

        [Fact]
        public void AContentFoldersProblemsAreSaidPlainlyUnderTheirGroups()
        {
            const string id = "broken_content";

            Write(id, "pack.json",
                "{ \"id\": \"broken_content\", \"kind\": \"campaign\", \"format\": 1, \"engine\": \"0.1\", " +
                "\"chapters\": [ { \"id\": \"one\", \"maps\": [ \"room\", \"nowhere\" ] } ] }");
            Write(id, "locale/broken_content.csv", "keys,en\ncampaign.broken_content.name,\n");
            Write(id, "maps/room.map", Map());
            Write(id, "maps/stand.map", Map("{ \"id\": \"not_a_prop\", \"x\": 2, \"y\": 2, \"turn\": 0 }"));
            Write(id, "maps/broken.map", "this is not a map");
            Write(id, "monster/goblin.json", "{}");
            Write(id, "minis/thing.txt", "x");
            Write(id, "encounters/fights.json",
                "{ \"tables\": [ { \"id\": \"set\", \"entries\": [ " +
                "{ \"id\": \"bad\", \"kind\": \"fight\", \"level\": 1, \"map\": \"gone\", \"loot\": \"nothing\", \"monsters\": [ { \"monster\": \"dragon_x\" } ] }, " +
                "{ \"id\": \"hard\", \"kind\": \"fight\", \"level\": 1, \"monsters\": [ { \"monster\": \"ogre\" } ] }, " +
                "{ \"id\": \"crowd\", \"kind\": \"fight\", \"level\": 20, \"monsters\": [ { \"monster\": \"goblin\", \"count\": \"9\" } ] }, " +
                "{ \"id\": \"loose\", \"kind\": \"fight\", \"monsters\": [ { \"monster\": \"goblin\" } ] } ] } ] }");
            Write(id, "encounters/again.json", "{ \"tables\": [ { \"id\": \"set\", \"entries\": [ { \"id\": \"x\", \"kind\": \"fight\", \"level\": 1, \"monsters\": [ { \"monster\": \"goblin\" } ] } ] } ] }");
            Write(id, "loot/loot.json",
                "{ \"tables\": [ { \"id\": \"chest\", \"entries\": [ { \"id\": \"a\", \"kind\": \"find\", \"items\": [ { \"item\": \"nope_item\" } ] }, " +
                "{ \"id\": \"b\", \"kind\": \"table\", \"table\": \"no_table\" } ] } ] }");
            Write(id, "merchants/shops.json",
                "{ \"merchants\": [ { \"id\": \"shop\", \"stock\": [ \"nope_item\" ] }, " +
                "{ \"id\": \"armory\", \"armory\": true, \"stock\": [ \"potion_of_healing\", \"rope\" ] } ] }");
            Write(id, "consequences/mine.json",
                "{ \"consequences\": [ { \"id\": \"stubbed_toe\", \"polarity\": \"bane\", \"kind\": \"health\", \"amount\": \"1d4\", \"weight\": 1 } ] }");
            Write(id, "dialogue/story.yarn",
                "title: one\nspeaker: dm\n---\nA door. #line:door\n<<check athletics 12>>\n<<if $passed>>\n    It opens. #line:opens\n    <<jump two>>\n<<endif>>\n===\n" +
                "title: two\nspeaker: dm\n---\n<<set $hero_level to 3>>\nNo tag on this one.\n===\n" +
                "title: three\n---\nNobody speaks this. #line:nobody\n===\n");

            var plain = Plain(id);

            Has(plain, "map_missing", ProblemGroup.Maps, "one", "nowhere");
            Has(plain, "map_unplayable", ProblemGroup.Maps, "broken");
            Has(plain, "map_stand_in", ProblemGroup.Maps, "stand");
            Has(plain, "unknown_folder", ProblemGroup.About, "monster");
            Has(plain, "folder_not_loaded", ProblemGroup.About, "minis");
            Has(plain, "fight_monster", ProblemGroup.Fights, "dragon_x");
            Has(plain, "fight_map", ProblemGroup.Fights, "gone");
            Has(plain, "fight_loot", ProblemGroup.Fights, "nothing");
            Has(plain, "fight_too_hard", ProblemGroup.Fights, "hard");
            Has(plain, "fight_too_many", ProblemGroup.Fights, "crowd");
            Has(plain, "fight_no_level", ProblemGroup.Fights, "loose");
            Has(plain, "defined_twice", ProblemGroup.Tables, "encounters", "set");
            Has(plain, "loot_item", ProblemGroup.Tables, "nope_item");
            Has(plain, "loot_table", ProblemGroup.Tables, "no_table");
            Has(plain, "shop_item", ProblemGroup.Tables, "nope_item");
            Has(plain, "armory_used_up", ProblemGroup.Tables, "potion_of_healing");
            Has(plain, "armory_unworn", ProblemGroup.Tables, "rope");
            Has(plain, "roll_taken", ProblemGroup.Tables);
            Has(plain, "about_unnamed", ProblemGroup.About);
            Has(plain, "chapter_untitled", ProblemGroup.About, "one");

            // a caution looks different from an error, and the card counts only the errors
            Assert.False(plain.Single(p => p.Kind == "map_stand_in").IsError);
            Assert.True(plain.Single(p => p.Kind == "map_missing").IsError);
        }

        [Fact]
        public void TheStorysProblemsAreSaidPlainly()
        {
            const string id = "broken_story";

            Write(id, "pack.json",
                "{ \"id\": \"broken_story\", \"kind\": \"campaign\", \"format\": 1, \"engine\": \"0.1\", " +
                "\"chapters\": [ { \"id\": \"one\", \"maps\": [ \"room\" ] } ] }");
            Write(id, "maps/room.map", Map());
            Write(id, "dialogue/story.yarn",
                "title: one\nspeaker: dm\n---\nA door. #line:door\n<<check athletics 12>>\n<<if $passed>>\n    It opens. #line:opens\n    <<jump two>>\n<<endif>>\n===\n" +
                "title: two\nspeaker: dm\n---\n<<set $hero_level to 3>>\nNo tag on this one.\n===\n");

            var plain = Plain(id);

            Has(plain, "story_two_keys", ProblemGroup.Story, "one");
            Has(plain, "story_hero_variable", ProblemGroup.Story);
            Has(plain, "story_untagged_line", ProblemGroup.Story);

            Write(id, "dialogue/story.yarn", "title: one\n---\nNobody speaks this. #line:nobody\n<<jump nowhere>>\n===\n");
            plain = Plain(id);
            Has(plain, "story_no_speaker", ProblemGroup.Story, "one");
            Has(plain, "story_missing_node", ProblemGroup.Story);
        }

        [Fact]
        public void AReplacedPoolWithOneSideAndAKindNobodyKnows()
        {
            const string id = "one_sided";

            Write(id, "pack.json",
                "{ \"id\": \"one_sided\", \"kind\": \"campaign\", \"format\": 1, \"engine\": \"0.1\", \"consequences\": \"replace\", " +
                "\"chapters\": [ { \"id\": \"one\", \"maps\": [ \"room\" ] } ] }");
            Write(id, "maps/room.map", Map());
            Write(id, "consequences/mine.json",
                "{ \"consequences\": [ { \"id\": \"my_toe\", \"polarity\": \"bane\", \"kind\": \"health\", \"amount\": \"1d4\", \"weight\": 1 } ] }");

            Has(Plain(id), "roll_one_sided", ProblemGroup.Tables);

            // a sentence no kind knows keeps its own words, under its folder's group
            PlainProblem unknown = ProblemWords.Plain(new ContentProblem("loot/x.json", "", "something new went wrong"));
            Assert.False(unknown.Known);
            Assert.Null(unknown.Said);
            Assert.Equal(ProblemGroup.Tables, unknown.Group);
        }
    }
}
