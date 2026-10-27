using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Dialogue;
using Content.Schema;
using Content.Workshop;
using Content.Workshop.Story;

namespace Content.Tests
{
    // cc_task_o: cards and blocks to Yarn and back; every .yarn in the repo opens and saves back identical; stable line
    // ids; options with nested blocks; "and then" fall-through; Text cards byte for byte; the locale rows added, updated
    // and removed
    public class WorkshopStoryTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_story_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        // THE CORPUS: the sample, the crucible fixture, and the old build's campaigns (read only, where the repo has them)
        public static IEnumerable<object[]> Corpus()
        {
            yield return new object[] { Path.Combine(SampleCampaignTests.Folder, "dialogue", "story.yarn") };
            yield return new object[] { Path.Combine(CrucibleTests.Folder, "dialogue", "arena.yarn") };

            string old = Old();
            if (old == null) yield break;

            foreach (string file in Directory.EnumerateFiles(old, "*.yarn", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")).OrderBy(f => f))
                yield return new object[] { file };
        }

        // the old build beside lanorim, if the checkout has it
        static string Old()
        {
            for (DirectoryInfo at = new DirectoryInfo(AppContext.BaseDirectory); at != null; at = at.Parent)
                if (Directory.Exists(Path.Combine(at.FullName, "solo_ttrpg_game", "campaigns"))) return Path.Combine(at.FullName, "solo_ttrpg_game");

            return null;
        }

        [Theory]
        [MemberData(nameof(Corpus))]
        public void EveryYarnFileOpensIntoCardsAndWritesBackIdentical(string file)
        {
            string text = File.ReadAllText(file);
            YarnText yarn = YarnText.Of(text);

            Assert.Equal(text, yarn.Text);

            var cards = yarn.Nodes.Select(n => CardDraft.Of(n, "dialogue/x.yarn")).ToList();
            Assert.NotEmpty(cards);
            Assert.Equal(text, StoryFiles.Text(text, cards));

            // and each card written fresh from its blocks is its node exactly (the subset's definition)
            foreach (CardDraft card in cards.Where(c => !c.IsText)) Assert.Equal(card.Read, card.Write(yarn.Newline));
        }

        [Fact]
        public void TheSampleOpensAsCardsEveryOne()
        {
            StoryDraft story = StoryFiles.Open(SampleCampaignTests.Folder);

            Assert.Equal(new[] { "old_mill", "cellar", "road", "camp", "the_end" }, story.Cards.Select(c => c.Title));
            Assert.All(story.Cards, c => Assert.False(c.IsText, $"{c.Title} opens as a Text card"));

            CardDraft mill = story.Card("old_mill");
            Choice note = mill.Blocks.OfType<Choice>().Single();
            Assert.Equal(3, note.Options.Count);
            Assert.Equal("opt_supplies", note.Options[1].LineId);
            Assert.Equal(new[] { typeof(Say), typeof(Do), typeof(Say) }, note.Options[1].Blocks.Select(b => b.GetType()));

            // after the options the card carries on: "and then", the line and the go-to after them
            Assert.IsType<Jump>(mill.Blocks.Last());
            Assert.Equal(new[] { ("cellar", "") }, StoryDraft.Leads(mill));

            // a check and the "Only if" after it are one block
            Do check = story.Card("cellar").Blocks.OfType<Do>().First();
            Assert.Equal("check", check.Verb);
            Assert.Equal("$passed", check.After.Arms[0].Condition);
            Assert.NotNull(check.After.Else);
        }

        [Fact]
        public void AHandWrittenNodeOutsideTheSubsetIsATextCardKeptByteForByte()
        {
            const string yarn = "title: odd\nspeaker: dm\n---\nA line with no tag.\n<<wait 2>>\n\tTabbed.\n===\n";

            CardDraft card = YarnText.Of(yarn).Nodes.Select(n => CardDraft.Of(n, "dialogue/odd.yarn")).Single();

            Assert.True(card.IsText);
            Assert.Equal(yarn, card.Write("\n"));
            Assert.Equal(yarn, StoryFiles.Text(yarn, new[] { card }));
        }

        [Fact]
        public void CardsWriteAsTheSamplesYarnAndCompile()
        {
            CardDraft gate = CardDraft.New("gate");
            gate.Blocks.Add(new Say { Text = "The gate is shut.", Tags = { "line:gate_1" } });
            gate.Blocks.Add(new Choice
            {
                Options =
                {
                    new Option { Text = "Knock.", Tags = { "line:gate_2" }, Blocks = { new Say { Text = "Nobody comes.", Tags = { "line:gate_3" } }, new Jump { Target = "road" } } },
                    new Option { Text = "Climb.", Tags = { "line:gate_4" }, Blocks = { new Say { Text = "Up and over.", Tags = { "line:gate_5", "speaker:companion" } } } },
                },
            });
            gate.Blocks.Add(new Say { Text = "And then you're in.", Tags = { "line:gate_6" } });
            gate.Blocks.Add(new Stop());

            CardDraft road = CardDraft.New("road");
            road.Blocks.Add(new Say { Text = "The road.", Tags = { "line:road_1" } });
            road.Blocks.Add(new Stop());

            string text = gate.Write("\n") + road.Write("\n");

            Assert.Equal("""
                title: gate
                speaker: dm
                ---
                The gate is shut. #line:gate_1
                -> Knock. #line:gate_2
                    Nobody comes. #line:gate_3
                    <<jump road>>
                -> Climb. #line:gate_4
                    Up and over. #line:gate_5 #speaker:companion
                And then you're in. #line:gate_6
                <<stop>>
                ===

                """.Replace("\r\n", "\n"), gate.Write("\n"));

            DialogueBook book = DialogueBook.Of("probe", ("story.yarn", text));
            Assert.True(book.Problems.All(p => p.IsACaution), string.Join("; ", book.Problems));
            Assert.Equal("dialogue.companion.line.probe.gate_5", book.Line("line:gate_5").Key);

            // and it reads back as the same blocks
            CardDraft again = YarnText.Of(text).Nodes.Select(n => CardDraft.Of(n, "x")).First();
            Assert.False(again.IsText);
            Assert.Equal(gate.Write("\n"), again.Write("\n"));
        }

        [Fact]
        public void ALinesIdIsMadeOnceAndEditingItsWordsKeepsIt()
        {
            var story = new StoryDraft();
            CardDraft card = CardDraft.New("arrive");
            story.Cards.Add(card);

            string first = story.NewLineId("arrive");
            card.Blocks.Add(new Say { Text = "Hello.", LineId = first });
            string second = story.NewLineId("arrive");

            Assert.Equal("arrive_1", first);
            Assert.Equal("arrive_2", second);

            ((Say)card.Blocks[0]).Text = "Hello again.";
            Assert.Equal("arrive_1", ((Say)card.Blocks[0]).LineId);

            // a Text card's ids are taken too
            story.Cards.Add(new CardDraft { Text = "title: x\n---\nHi #line:arrive_2\n===\n" });
            Assert.Equal("arrive_3", story.NewLineId("arrive"));
        }

        [Fact]
        public void SavingWritesOnlyTheChangedNodeAndKeepsTheLocaleInStep()
        {
            string folder = Path.Combine(_root, "sample_millbrook");
            foreach (string file in Directory.EnumerateFiles(SampleCampaignTests.Folder, "*", SearchOption.AllDirectories))
            {
                string there = Path.Combine(folder, Path.GetRelativePath(SampleCampaignTests.Folder, file));
                Directory.CreateDirectory(Path.GetDirectoryName(there));
                File.Copy(file, there);
            }

            string yarnPath = Path.Combine(folder, "dialogue", "story.yarn");
            string csvPath = Path.Combine(folder, "locale", "sample_millbrook.csv");
            string before = File.ReadAllText(yarnPath);
            LocaleSheet sheet = LocaleSheet.Of(File.ReadAllText(csvPath));

            StoryDraft story = StoryFiles.Open(folder);

            // nothing changed: nothing written
            StoryFiles.Save(story, folder, "sample_millbrook", sheet);
            Assert.Equal(before, File.ReadAllText(yarnPath));
            Assert.Equal(File.ReadAllText(csvPath), sheet.Text());

            // one line's words changed, one line taken out, one speaker changed, a new card added
            CardDraft end = story.Card("the_end");
            ((Say)end.Blocks[0]).Text = "You walk back into Millbrook at dusk.";
            end.Blocks.RemoveAll(b => b is Say s && s.LineId == "comp_beds");
            ((Say)end.Blocks.First(b => b is Say s && s.LineId == "test_over")).Speaker = "oda";

            CardDraft epilogue = CardDraft.New("epilogue");
            epilogue.Blocks.Add(new Say { Text = "Years later, the mill still turns.", LineId = story.NewLineId("epilogue") });
            epilogue.Blocks.Add(new Stop());
            story.Cards.Add(epilogue);

            StoryFiles.Save(story, folder, "sample_millbrook", sheet);
            string after = File.ReadAllText(yarnPath);

            // the other four nodes are as they were, byte for byte
            foreach (string title in new[] { "old_mill", "cellar", "road", "camp" })
                Assert.Contains(YarnText.Of(before).Nodes.Single(n => n.Title == title).Raw, after);

            Assert.Contains("You walk back into Millbrook at dusk. #line:wheel_turns", after);
            Assert.DoesNotContain("comp_beds", after);
            Assert.EndsWith("title: epilogue\nspeaker: dm\n---\nYears later, the mill still turns. #line:epilogue_1\n<<stop>>\n===\n", after.Replace("\r\n", "\n"));

            Assert.Equal("You walk back into Millbrook at dusk.", sheet.Get("dialogue.dm.line.sample_millbrook.wheel_turns"));
            Assert.False(sheet.Has("dialogue.companion.line.sample_millbrook.comp_beds"));
            Assert.False(sheet.Has("dialogue.dm.line.sample_millbrook.test_over"));
            Assert.Equal("This is the end of the test campaign.", sheet.Get("dialogue.oda.line.sample_millbrook.test_over"));
            Assert.Equal("Years later, the mill still turns.", sheet.Get("dialogue.dm.line.sample_millbrook.epilogue_1"));

            // rows stay in key order, and the campaign still compiles
            List<string> keys = sheet.Keys.ToList();
            Assert.Equal(keys.OrderBy(k => k, StringComparer.Ordinal), keys);

            File.WriteAllText(csvPath, sheet.Text());
            DialogueBook book = DialogueBook.Read(Path.Combine(folder, "dialogue"), "sample_millbrook");
            Assert.True(book.Problems.All(p => p.IsACaution), string.Join("; ", book.Problems));
        }

        [Fact]
        public void AHerosNameInALineIsANumberedPlaceholderInItsRow()
        {
            var story = new StoryDraft();
            CardDraft card = CardDraft.New("gate");
            card.Blocks.Add(new Say { Text = "Locked, {$hero_name}. Again, {$hero_name}.", LineId = "gate_1" });
            story.Cards.Add(card);

            Assert.Equal("Locked, {0}. Again, {1}.", StoryFiles.Keys(story, "probe")["dialogue.dm.line.probe.gate_1"]);

            // and the game reads it so: the compiler hands the line its substitutions in that order
            DialogueBook book = DialogueBook.Of("probe", ("x.yarn", card.Write("\n")));
            Assert.True(book.Problems.All(p => p.IsACaution), string.Join("; ", book.Problems));
        }

        [Fact]
        public void TheStoryRidesInTheDraftsSnapshotWithItsBlocks()
        {
            var draft = new CampaignDraft { Id = "x" };
            draft.Story = StoryFiles.Open(SampleCampaignTests.Folder);

            CampaignDraft again = CampaignDraft.FromSnapshot(draft.Snapshot());

            Assert.Equal(draft.Snapshot(), again.Snapshot());
            Assert.IsType<Choice>(again.Story.Card("old_mill").Blocks.First(b => b is Choice));
        }
    }
}
