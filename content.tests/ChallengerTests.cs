using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Challengers;
using Content.Combat;
using Content.Dialogue;
using Content.Encounters;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Core.Tables;

namespace Content.Tests
{
    // THE ARENA'S GENERATED CHALLENGERS (cc_task_cg), on the crucible fixture: its challengers/ came from
    // Fixtures/challenger_lists/Arena Fights.md by tools/import_challengers.py; its "spear_trial" is a special fight from
    // reputation 10; its "roster" node is <<challengers>>, an announcer's line, <<fight {$challenger}>> and round again
    public partial class ChallengerTests
    {
        static readonly Library Srd = Library.Srd();

        static Package Pack() => SampleCampaignTests.Sound(CrucibleTests.Folder);

        static readonly string Lists = Path.Combine(AppContext.BaseDirectory, "Fixtures", "challenger_lists");

        static CampaignRun Run(out Hero hero, int seed = 7, int level = 1, Package pack = null)
        {
            hero = SampleCampaignTests.Make(Srd, "fighter");
            if (level > 1) hero.LevelTo(level);
            return new CampaignRun(Srd, pack ?? Pack(), hero, new StandardResolver(new SeededRng(seed)), new SeededRng(seed));
        }

        static Scene On(CampaignRun run)
        {
            while (run.Now == Scene.Line) run.Next();
            return run.Now;
        }

        // the roster node, up to the roster
        static Scene ToTheRoster(CampaignRun run)
        {
            run.Start("roster");
            return On(run);
        }

        static IReadOnlyList<string> Ids(CampaignRun run) => run.Arena.Roster.Select(c => c.Id).ToList();

        // the drawing itself, without a story: a fresh arena at a reputation, drawn for a level
        // bestiary: the stat blocks the shared lists can field when no type claims them (none, as most tests want)
        static Arena Drawn(int level, int seed, int reputation = 0, Package pack = null, ArenaRules rules = null,
                           IReadOnlyList<ChallengerType> types = null, IEnumerable<string> bestiary = null)
        {
            pack ??= Pack();
            var arena = new Arena { Reputation = reputation };
            new RosterDraw(arena, types ?? pack.Challengers, Find(pack), new SeededRng(seed), bestiary)
                .Draw(level, rules ?? ArenaRules.Default, pack.SpecialChallengers);
            return arena;
        }

        static IEnumerable<string> Bestiary() => Srd.Bestiary.All.Select(m => m.Id);

        static Func<string, Content.Monsters.Monster> Find(Package pack) =>
            id => pack.Monsters.FirstOrDefault(m => m.Id == id) ?? Srd.Bestiary.Find(id);

        static ILocalizer Words() =>
            new DictionaryLocalizer(Locale.Read(File.ReadAllText(Path.Combine(CrucibleTests.Folder, "locale", "crucible_fixture.csv"))),
                                    Locale.Localizer(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game.csv"))));

        // --- Part 1: importing her lists ----------------------------------------------------------------------

        [Fact]
        public void TheImporterWritesTheTypesAndTheirKeysAndTheFixtureIsUpToDate()
        {
            string temp = Path.Combine(Path.GetTempPath(), "lanorim_challengers_" + Guid.NewGuid().ToString("N"));

            try
            {
                Copy(CrucibleTests.Folder, temp);
                Directory.Delete(Path.Combine(temp, "challengers"), true);

                (int code, string said) = BarkTests.Python("tools/import_challengers.py", temp, "--vault", Lists);
                Assert.True(code == 0, said);

                Assert.True(ChallengerReader.Entries.TryRead(File.ReadAllText(Path.Combine(temp, "challengers", "arena_fights.json")),
                                                     out IReadOnlyList<ChallengerType> types, out IReadOnlyList<string> problems),
                            string.Join("; ", problems));

                ChallengerType goblin = types.Single(t => t.Id == "goblin_warrior");
                Assert.Equal(ChallengerShape.Single, goblin.Shape);
                Assert.Equal("goblin", goblin.Monsters.Single().Monster);
                Assert.Equal((3, 2), (goblin.Names, goblin.Descriptions));

                Assert.Equal(ChallengerShape.Group, types.Single(t => t.Id == "guard_pair").Shape);
                Assert.Equal(2, types.Single(t => t.Id == "guard_pair").Monsters.Single().Count.Maximum);
                Assert.Equal(new[] { "goblin", "guard" }, types.Single(t => t.Id == "raiders").Monsters.Select(b => b.Monster));

                string[] rows = File.ReadAllLines(Path.Combine(temp, "locale", "crucible_fixture.csv"));
                Assert.Contains("challenger.goblin_warrior.name.001,Grik the Knife", rows);
                Assert.Contains("challenger.raiders.name.002,The North River Crew", rows);
                Assert.Contains("campaign.crucible_fixture.name,The Crucible (test fixture)", rows);

                // and the fixture the other tests play is what the importer makes of the lists
                (int check, string behind) = BarkTests.Python("tools/import_challengers.py", CrucibleSource(), "--vault",
                                                              SourceLists(), "--check");
                Assert.True(check == 0, behind);
            }
            finally
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        [Theory]
        [InlineData("stats: three gobbos", "'gobbos' is not a monster")]
        [InlineData("stats: a single goblin or two guards", "'or' is between single monsters")]
        [InlineData("stats: a single thug or scout", "'thug' is not a monster")]
        [InlineData("stats: team of goblin, wibbler", "'wibbler' is not a monster")]
        // humanoids only (cc_task_ca Part 1): an ogre is a giant, a wolf a beast; a goblin passes (goblinoid)
        [InlineData("stats: a single ogre", "ogre isn't a humanoid: v1's challengers are humanoids only")]
        [InlineData("stats: team of goblin, wolf", "wolf isn't a humanoid")]
        public void ABadStatsLineIsAnErrorThatNamesTheLineAndWritesNothing(string line, string says)
        {
            string temp = Path.Combine(Path.GetTempPath(), "lanorim_challengers_" + Guid.NewGuid().ToString("N"));

            try
            {
                string vault = Path.Combine(temp, "vault");
                Directory.CreateDirectory(vault);
                File.WriteAllText(Path.Combine(vault, "Bad Fights.md"),
                                  $"# gobbo\n{line}\n\n## descriptions\n1. A gobbo.\n\n## names\n1. Gob\n");

                string pack = Path.Combine(temp, "pack");
                Copy(CrucibleTests.Folder, pack);
                string before = File.ReadAllText(Path.Combine(pack, "locale", "crucible_fixture.csv"));

                (int code, string said) = BarkTests.Python("tools/import_challengers.py", pack, "--vault", vault);

                Assert.Equal(1, code);
                Assert.Contains("Bad Fights.md:2", said);
                Assert.Contains(says, said);
                Assert.False(File.Exists(Path.Combine(pack, "challengers", "bad_fights.json")));
                Assert.Equal(before, File.ReadAllText(Path.Combine(pack, "locale", "crucible_fixture.csv")));
            }
            finally
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        [Fact]
        public void ThePackReadsItsChallengersAndItsSpecialFightAndPromisesTheirKeys()
        {
            Package pack = Pack();

            Assert.Equal(10, pack.Challengers.Count);
            Assert.Equal(new[] { "generic_single", "generic_team" }, pack.Challengers.Where(t => t.Shared).Select(t => t.Id));
            Assert.Equal(new[] { "goblin", "bandit_captain" }, pack.Challengers.Single(t => t.Id == "bruiser").Monsters.Select(b => b.Monster));

            EncounterEntry special = pack.SpecialChallengers.Single();
            Assert.Equal("spear_trial", special.Id);
            Assert.Equal(10, special.Challenger.Reputation);
            Assert.True(special.Challenger.Trigger.IsAlways);

            List<string> keys = pack.Keys().ToList();
            Assert.Contains("challenger.raiders.name.002", keys);
            Assert.Contains("challenger.captain.description.001", keys);
            Assert.Contains("challenger.spear_trial.name", keys);
            Assert.Contains("challenger.spear_trial.description", keys);
        }

        [Fact]
        public void AChallengersFileWrittenWrongSaysWhatIsWrongInTheUsualWay()
        {
            ChallengerReader.Entries.TryRead(@"{""challengers"":[
                {""id"":""a"",""shape"":""single"",""monsters"":[{""monster"":""guard"",""count"":""2""}],""names"":1,""descriptions"":1},
                {""id"":""b"",""shape"":""crowd"",""monsters"":[{""monster"":""guard""}],""names"":0,""descriptions"":1,""nmaes"":3}]}",
                                     out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.StartsWith("a:") && p.Contains("say \"group\""));
            Assert.Contains(problems, p => p.StartsWith("b:") && p.Contains("'crowd' is not a shape"));
            Assert.Contains(problems, p => p.Contains("'names'"));
            Assert.Contains(problems, p => p.Contains("'nmaes'"));

            // and a pack whose type names a monster nobody ships is refused on the shelf
            Read<Manifest> manifest = ManifestReader.Parse(@"{ ""id"": ""x"", ""kind"": ""campaign"", ""format"": 1, ""engine"": ""0.1"",
                ""challengers"": { ""win"": -1, ""roster"": { ""most"": ""lots"" } } }", ManifestReader.PackFileName);

            Assert.Contains(manifest.Problems, p => p.Where == "challengers" && p.What.Contains("'win'"));
            Assert.Contains(manifest.Problems, p => p.Where == "challengers" && p.What.Contains("'most'"));
        }

        // --- Part 2: reputation --------------------------------------------------------------------------------

        [Fact]
        public void ReputationSetsTheRosterSizeAndNeverDropsBelowZero()
        {
            ArenaRules rules = ArenaRules.Default;

            Assert.Equal(2, rules.RosterSize(0));
            Assert.Equal(2, rules.RosterSize(9));
            Assert.Equal(3, rules.RosterSize(10));
            Assert.Equal(6, rules.RosterSize(45));
            Assert.Equal(6, rules.RosterSize(500));

            Assert.Equal(5, rules.Moved(0, won: true));
            Assert.Equal(0, rules.Moved(0, won: false));
            Assert.Equal(0, rules.Moved(1, won: false));
            Assert.Equal(8, rules.Moved(10, won: false));

            Assert.Equal(2, Drawn(1, 3, reputation: 0).Roster.Count);
            Assert.Equal(4, Drawn(1, 3, reputation: 20).Roster.Count);
        }

        [Fact]
        public void APackCanTuneTheArenaAndTheWorkshopWritesWhatItChanged()
        {
            const string json = @"{ ""id"": ""x"", ""kind"": ""campaign"", ""format"": 1, ""engine"": ""0.1"",
                ""chapters"": [ { ""id"": ""arena"", ""maps"": [ ""pit"" ] } ],
                ""challengers"": { ""win"": 3, ""roster"": { ""least"": 1, ""most"": 4 }, ""bout_xp"": { ""share"": 0.5 } } }";

            Read<Manifest> read = ManifestReader.Parse(json, ManifestReader.PackFileName);
            Assert.True(read.Ok, string.Join("; ", read.Problems));

            ArenaRules arena = read.Value.Rules.Arena;
            Assert.Equal((3, -2, 1, 10, 4, 0.5), (arena.Win, arena.Loss, arena.Least, arena.Every, arena.Most, arena.Share));

            ArenaRules again = ManifestReader.Parse(ManifestWriter.Write(read.Value), ManifestReader.PackFileName).Value.Rules.Arena;
            Assert.Equal(arena.ToString(), again.ToString());
            Assert.DoesNotContain("\"loss\"", ManifestWriter.Write(read.Value));

            // a pack that says nothing has the game's, and writes nothing
            Assert.True(Pack().Manifest.Rules.Arena.IsDefault);
            Assert.DoesNotContain("challengers", ManifestWriter.Write(Pack().Manifest));
        }

        [Fact]
        public void TheStoryReadsReputationAndCannotSetIt()
        {
            CampaignRun run = Run(out _);
            Assert.Equal(Scene.Roster, ToTheRoster(run));

            run.Arena.Reputation = 15;
            Assert.Equal(15f, Convert.ToSingle(ArenaVariables.Read(run.Arena, ArenaVariables.Reputation)));

            // the run's store reads it live, and a story's <<set>> of it is warned about
            DialogueBook book = DialogueBook.Of("crucible_fixture",
                ("x.yarn", "title: x\n---\n<<set $reputation to 99>>\nHi. #line:hi\n==="));
            Assert.Contains(book.Problems, p => p.Where == ArenaVariables.Reputation && p.What.Contains("read-only"));
        }

        // --- helpers ----------------------------------------------------------------------------------------------

        static void Copy(string from, string to)
        {
            foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dir.Replace(from, to));

            Directory.CreateDirectory(to);

            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(file, file.Replace(from, to), true);
        }

        // the fixture in the source tree, not the copy the build made, so the check reads what is committed
        static string CrucibleSource() => Path.Combine(Source(), "content.tests", "Fixtures", "crucible_fixture");

        static string SourceLists() => Path.Combine(Source(), "content.tests", "Fixtures", "challenger_lists");

        // the source tree: where tools/ is
        static string Source() => BarkTests.Root();
    }
}
