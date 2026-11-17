using System;
using System.IO;
using Content.Campaigns;
using Content.Dialogue;
using Content.Play;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Content.Tests
{
    // THE STORY'S NEWER VERBS, played through a copy of the sample campaign with a node of the test's own added:
    // <<tip section>> (cc_task_nt Part 4) and <<map id>> (cc_task_tf Part 1). The copy is made in a temp folder and deleted
    // after
    public sealed partial class StoryVerbTests : IDisposable
    {
        static readonly Library Srd = Library.Srd();

        // the folder has to be called what the campaign calls itself, so it sits in a temp folder of its own
        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_story_verbs_" + Guid.NewGuid().ToString("N"));

        string _folder => Path.Combine(_root, "sample_millbrook");

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        // the sample campaign, with `yarn` (whole nodes) added to its story
        Package SampleWith(string yarn)
        {
            string from = Path.Combine(AppContext.BaseDirectory, "campaigns", "sample_millbrook");

            foreach (string path in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                string to = Path.Combine(_folder, Path.GetRelativePath(from, path));
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(path, to);
            }

            File.AppendAllText(Path.Combine(_folder, "dialogue", "story.yarn"), "\n" + yarn.Replace("\r\n", "\n"));
            return Package.Read(_folder);
        }

        // a fighter who has done their shopping, so the story starts where it's asked to
        static CampaignRun Run(Package pack)
        {
            Hero hero = Creation.QuickHero.Make(Srd, "fighter", 1, "Tess");
            hero.ShopsFirst = false;

            return new CampaignRun(Srd, pack, hero, new StandardResolver(new ScriptedRng(10)), new SeededRng(1));
        }

        // --- <<map>> (cc_task_tf Part 1) ----------------------------------------------------------------------

        const string MapNode = @"title: map_test
speaker: dm
---
<<map road_clearing>>
<<jump cellar>>
===
";

        [Fact]
        public void AMapCommandMovesTheScenesBackdropAndTheStoryGoesStraightOn()
        {
            Assert.Equal(RequestKind.Map, Request.Parse("map briarwood_inn", out string none).Kind);
            Assert.Null(none);

            CampaignRun run = Run(SampleWith(MapNode));

            Assert.Equal(Scene.Line, run.Start("map_test"));
            Assert.Equal("road_clearing", run.MapId);
            Assert.EndsWith("door_stuck", run.Talk.Saying.Key);
        }

        [Fact]
        public void AMapTheCampaignDoesntHaveIsAProblemWithItsFileNodeAndLine()
        {
            Package pack = SampleWith(MapNode.Replace("road_clearing", "nowhere"));

            ContentProblem problem = Assert.Single(Workshop.WorkshopProblems.Of(pack.Folder), p => p.What.Contains("<<map nowhere>>"));
            Assert.Equal("dialogue/story.yarn", problem.File);
            Assert.Equal("map_test", problem.Where);
            Assert.True(problem.Line > 0);
            Assert.False(problem.IsACaution);

            // played anyway, the board stays where it was and the playthrough says why
            CampaignRun run = Run(pack);
            run.Start("map_test");
            Assert.Equal("", run.MapId);
            Assert.Contains(run.Talk.Complained, c => c.Contains("no map 'nowhere'"));
        }

        // --- <<tip>> ------------------------------------------------------------------------------------------------

        // no lines of its own (a line needs a locale row): the tip, then on to the sample's cellar
        const string TipNode = @"title: tip_test
speaker: dm
---
<<tip dice>>
<<jump cellar>>
===
";

        [Fact]
        public void ATipStopsTheStoryAtThatSectionUntilTheCardIsClosed()
        {
            Package pack = SampleWith(TipNode);
            Assert.True(pack.Sound, string.Join("; ", pack.Problems));
            CampaignRun run = Run(pack);

            Assert.Equal(Scene.Tip, run.Start("tip_test"));
            Assert.Equal("dice", run.Tip);

            // nothing else moves it on
            Assert.Equal(Scene.Tip, run.Next());

            Assert.Equal(Scene.Line, run.TipClosed());
            Assert.Null(run.Tip);
            Assert.EndsWith("door_stuck", run.Talk.Saying.Key);
        }

        [Fact]
        public void AnUnknownSectionIsACampaignCheckErrorThatNamesTheLine()
        {
            Package pack = SampleWith(TipNode.Replace("<<tip dice>>", "<<tip nowhere>>"));

            // the campaign check reads the story the way a run does (WorkshopProblems, check-content)
            DialogueBook book = DialogueBook.Read(Path.Combine(pack.Folder, Package.DialogueFolder), pack.Id);
            ContentProblem problem = Assert.Single(book.Problems, p => p.What.Contains("'tip nowhere'"));
            Assert.False(problem.IsACaution);
            Assert.Equal("dialogue/story.yarn", problem.File);
            Assert.Equal("tip_test", problem.Where);
            Assert.True(problem.Line > 0);
        }

        [Fact]
        public void TheSectionsAreTheHelpCardsOwnAndAnyOtherIsRefusedByName()
        {
            foreach (string section in HelpCard.Sections)
            {
                Assert.NotNull(Request.Parse("tip " + section, out string none));
                Assert.Null(none);
                Assert.Contains(HelpCard.LineKey(section), HelpCard.Lines);
            }

            Assert.Null(Request.Parse("tip nowhere", out string problem));
            Assert.Contains("'tip nowhere'", problem);
            Assert.Contains("dice", problem);

            Assert.Null(Request.Parse("tip", out problem));
            Assert.NotNull(problem);
        }
    }
}
