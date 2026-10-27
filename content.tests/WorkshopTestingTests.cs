using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Creation;
using Content.Dialogue;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Content.Workshop;
using Content.Workshop.Story;
using Content.Workshop.Testing;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Content.Tests
{
    // cc_task_q: the test hero is --begin's; memory presets are set before the first line; a skipped fight sets $fight and
    // drops loot as playing would; "always pass" and "always fail" bend only the hero's rolls; Back to the editor lands on
    // the right card and block; a test never writes into the campaign or the player's saves; Run it for me reaches every
    // card of the sample
    public class WorkshopTestingTests : IDisposable
    {
        static readonly Library Srd = Library.Srd();

        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_test_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        string Copy()
        {
            string folder = Path.Combine(_root, "mine" + Guid.NewGuid().ToString("N"), "sample_millbrook");

            foreach (string file in Directory.EnumerateFiles(SampleCampaignTests.Folder, "*", SearchOption.AllDirectories))
            {
                string there = Path.Combine(folder, Path.GetRelativePath(SampleCampaignTests.Folder, file));
                Directory.CreateDirectory(Path.GetDirectoryName(there));
                File.Copy(file, there);
            }

            return folder;
        }

        SaveLibrary Saves => new(Path.Combine(_root, "test_saves"));

        [Theory]
        [InlineData("fighter", 1)]
        [InlineData("mage", 5)]
        [InlineData("cleric", 3)]
        public void TheTestHeroIsTheOneBeginMakes(string cls, int level)
        {
            Hero begun = QuickHero.Make(Srd, cls, level, "Probe");
            Hero tested = new TestSetup { Class = cls, Level = level, AsAPlayer = true }.Hero(Srd, "Probe");

            Assert.Equal(begun.ToString(), tested.ToString());
            Assert.Equal(begun.Spells.Select(s => s.Id), tested.Spells.Select(s => s.Id));
            Assert.Equal(begun.Pack.Stacks.Select(s => s.ToString()), tested.Pack.Stacks.Select(s => s.ToString()));
        }

        [Fact]
        public void EverySpellAndExtrasAreTheTestHerosOwn()
        {
            Hero hero = new TestSetup { Class = "mage", Level = 3, EverySpell = true, Gold = 50, Items = { ["rope"] = 2 } }.Hero(Srd, "Probe");
            Hero plain = QuickHero.Make(Srd, "mage", 3, "Probe");

            Assert.True(hero.Spells.Count() > plain.Spells.Count());
            Assert.All(hero.Spells, s => Assert.True(s.Level <= 2));
            Assert.Equal(2, hero.Pack.CountOf("rope"));
            Assert.Equal(plain.Pack.Copper + 5000, hero.Pack.Copper);
        }

        WorkshopTest Test(string folder, TestSetup setup = null) => new(CampaignEditor.Open(folder), setup ?? new TestSetup());

        (CampaignRun Run, WorkshopTest Test) Started(TestSetup setup = null, Action<CampaignEditor> edit = null)
        {
            string folder = Copy();
            WorkshopTest test = Test(folder, setup);
            edit?.Invoke(test.Editor);

            string written = test.Write(Path.Combine(_root, "tests"), out string problem);
            Assert.Null(problem);

            Package pack = Package.Read(written);
            Assert.True(pack.Sound, string.Join("; ", pack.Problems));

            var rng = new SeededRng(7);
            var run = new CampaignRun(Srd.With(pack), pack, test.Setup.Hero(Srd, "Probe"), new StandardResolver(rng), rng, Saves, 0);
            test.Prepare(run);
            run.Start(test.Setup.Start.Length > 0 ? test.Setup.Start : null);
            return (run, test);
        }

        [Fact]
        public void WhatTheStoryRemembersIsSetBeforeTheFirstLine()
        {
            var setup = new TestSetup { Start = "the_end", Memory = { ["$tavin_was_brave"] = "true", ["$korr"] = "2" } };

            (CampaignRun run, _) = Started(setup, editor => editor.Edit("card", d =>
            {
                CardDraft end = d.Story.Card("the_end");
                end.Blocks.Insert(0, new Branch
                {
                    Arms = { new Arm { Condition = "$tavin_was_brave and $korr >= 2", Blocks = { new Say { Text = "Tavin waves.", LineId = "tavin_waves" } } } },
                    Else = new() { new Say { Text = "Nobody waves.", LineId = "nobody_waves" } },
                });
                end.Blocks.Insert(0, new Set { Variable = "$tavin_was_brave", Value = "false" });
                end.Blocks.RemoveAt(0);
                d.Story.Card("cellar").Blocks.Add(new Set { Variable = "$tavin_was_brave", Value = "true" });
                d.Story.Card("cellar").Blocks.Add(new Set { Variable = "$korr", Value = "$korr + 1" });
            }));

            Assert.Equal(Scene.Line, run.Now);
            Assert.Equal("dialogue.dm.line.sample_millbrook.tavin_waves", run.Talk.Saying.Key);
        }

        [Fact]
        public void ASkippedFightSetsFightAndDropsLootAsPlayingWould()
        {
            (CampaignRun run, _) = Started(new TestSetup { Start = "cellar" });

            for (int i = 0; i < 50 && run.Now != Scene.Fight; i++)
                if (run.Now == Scene.Line) run.Next();
                else if (run.Now == Scene.Choice) run.Choose(0);

            Assert.Equal(Scene.Fight, run.Now);
            Assert.Equal("cellar_rats", run.Fight.Entry.Id);

            run.EndFight(Outcome.HeroesWon);

            Assert.Equal("won", run.Recall("$fight"));
            Assert.NotNull(run.Spoils);
        }

        [Fact]
        public void AlwaysPassAndAlwaysFailBendOnlyTheHerosRolls()
        {
            foreach (RollRule rule in new[] { RollRule.Pass, RollRule.Fail })
            {
                (CampaignRun run, WorkshopTest test) = Started(new TestSetup { Start = "cellar" });
                test.Rolls = rule;

                Settled check = run.Referee.Settle(Request.Parse("check athletics 30", out _));
                Assert.Equal(rule == RollRule.Pass, check.Attempt.Succeeded);

                Settled save = run.Referee.Settle(Request.Parse("save dex 1", out _));
                Assert.Equal(rule == RollRule.Pass, save.Attempt.Succeeded);

                // the GM's own roll is never bent
                Settled roll = run.Referee.Settle(Request.Parse("roll 1d20", out _));
                Assert.Null(roll.Attempt);
            }

            // the next roll only, once
            (CampaignRun again, WorkshopTest once) = Started(new TestSetup { Start = "cellar" });
            once.Next = RollRule.Fail;
            Assert.False(again.Referee.Settle(Request.Parse("check athletics 1", out _)).Attempt.Succeeded);
            Assert.Null(once.Next);
        }

        [Fact]
        public void BackToTheEditorLandsOnTheCardAndTheBlockThatWasPlaying()
        {
            (CampaignRun run, WorkshopTest test) = Started(new TestSetup { Start = "road" });

            test.Saw(run, _ => true);

            Assert.Equal("road", test.Card);
            Assert.Equal("road_north", test.LastLine);
            Assert.Equal(new[] { 0 }, test.Block(test.Editor.Draft.Story));
        }

        [Fact]
        public void ATestNeverWritesIntoTheCampaignOrThePlayersSaves()
        {
            string folder = Copy();
            string before = string.Join("|", Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).OrderBy(f => f).Select(File.ReadAllText));

            WorkshopTest test = Test(folder);
            test.Editor.Edit("card", d => ((Say)d.Story.Card("the_end").Blocks[0]).Text = "Changed, unsaved.");

            string written = test.Write(Path.Combine(_root, "tests"), out _);

            Assert.NotEqual(folder, written);
            Assert.Contains("Changed, unsaved.", File.ReadAllText(Path.Combine(written, "dialogue", "story.yarn")));
            Assert.Equal(before, string.Join("|", Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).OrderBy(f => f).Select(File.ReadAllText)));
            Assert.False(Directory.Exists(Path.Combine(written, Autosave.Folder)));
        }

        [Fact]
        public void RunItForMeReachesEveryCardOfTheSample()
        {
            Package pack = Package.Read(SampleCampaignTests.Folder);
            StoryDraft story = StoryFiles.Open(SampleCampaignTests.Folder);

            RunForMe.Report report = RunForMe.All(Srd.With(pack), pack, () => QuickHero.Make(Srd, "fighter", 3, "Probe"), null, story,
                                                  everyChoice: false, Saves);

            Assert.Equal(1, report.Runs);
            Assert.Equal(new[] { "old_mill", "cellar", "road", "camp", "the_end" }, report.Reached);
            Assert.Empty(report.NeverReached);
        }

        [Fact]
        public void TryEveryChoiceFindsACardOnlyAChoiceReaches()
        {
            Package pack = Package.Read(SampleCampaignTests.Folder);
            StoryDraft story = StoryFiles.Open(SampleCampaignTests.Folder);

            // a card only the old mill's third option leads to
            var hidden = CardDraft.New("hidden");
            hidden.Blocks.Add(new Say { Text = "A trapdoor.", LineId = "hidden_1" });
            story.Cards.Add(hidden);
            ((Choice)story.Card("old_mill").Blocks.First(b => b is Choice)).Options[2].Blocks.Add(new Jump { Target = "hidden" });

            ((Choice)story.Card("old_mill").Blocks.First(b => b is Choice)).Options[2].Condition = "$hero_casts";

            RunForMe.Gate gate = Assert.Single(RunForMe.Behind(story, "hidden"));
            Assert.Equal(("old_mill", "C"), (gate.From, gate.Option));
            Assert.Equal(new[] { "$hero_casts" }, gate.Conditions);
        }
    }
}
