using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Dialogue;
using Content.Play;
using Content.Schema;
using Content.Sheet;
using Content.Workshop;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Content.Tests
{
    // THE FEAST (cc_task_tf, Kathleen's one-shot, written into campaigns/the_feast by tools/make_the_feast.py): it loads,
    // the campaign check has no errors in it, its four fights are within the solo budget, and its scenes move the board
    // with <<map>>
    public partial class FeastCampaignTests
    {
        static readonly Library Srd = Library.Srd();

        internal static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "campaigns", "the_feast");

        [Fact]
        public void TheFeastLoadsAndTheCampaignCheckHasNoErrors()
        {
            Package pack = SampleCampaignTests.Sound(Folder);
            IReadOnlyList<ContentProblem> problems = WorkshopProblems.Of(Folder);

            Assert.True(WorkshopProblems.Errors(problems) == 0, string.Join("\n", problems.Where(p => p.IsAFault)));
            Assert.Equal(11, pack.Maps.Count);
        }

        // the game's own map check (the map builder's): no prop off the edge or in a wall, none over the start or a spawn, every
        // spawn reachable; a warning is allowed (cc_task_tf Part 3.3)
        [Fact]
        public void EveryMapIsSoundByTheMapBuildersOwnCheck()
        {
            foreach (string path in Directory.EnumerateFiles(Path.Combine(Folder, "maps"), "*.map"))
            {
                Assert.True(Content.Maps.MapDraft.TryRead(File.ReadAllText(path), out Content.Maps.MapDraft draft, out string problem),
                            $"{Path.GetFileName(path)}: {problem}");
                Assert.True(draft.Sound, $"{Path.GetFileName(path)}: " + string.Join("; ", draft.Problems()));
            }
        }

        [Theory]
        [InlineData("mill_collectors", 2)]
        [InlineData("pond_bear", 1)]
        [InlineData("kitchen_hound", 1)]
        [InlineData("kitchen_guards", 2)]
        public void ItsFightsAreLevelOneAndWithinTheSoloCap(string id, int enemies)
        {
            Package pack = SampleCampaignTests.Sound(Folder);
            Core.Tables.EncounterEntry fight = pack.Encounters.SelectMany(t => t.Entries).Single(e => e.Id == id);

            Assert.Equal(1, fight.Level);
            Assert.Equal(enemies, fight.Monsters.Sum(b => b.Count.Maximum));
            Assert.True(enemies <= Encounters.FightBudget.Cap(1));
            Assert.True(pack.Maps.ContainsKey(fight.Map), fight.Map);
        }
    }
}
