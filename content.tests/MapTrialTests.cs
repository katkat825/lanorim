using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Creation;
using Content.Encounters;
using Content.Maps;
using Content.Play;
using Content.Schema;
using Content.Sheet;
using Core.Dice;
using Core.Resolution;
using Core.Space;
using Core.Tables;

namespace Content.Tests
{
    // cc_task_l Part 5, Play this map: the hero at a level, who stands at each spawn, the solo budget as warnings, and a
    // throwaway campaign whose one line of story starts the fight on the map
    public class MapTrialTests : IDisposable
    {
        static readonly Library Srd = Library.Srd();

        readonly string _folder = Path.Combine(Path.GetTempPath(), "lanorim_trial_" + Guid.NewGuid().ToString("N"), MapTrial.CampaignId);

        public void Dispose()
        {
            string root = Path.GetDirectoryName(_folder);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        static MapDraft Field()
        {
            MapDraft draft = Screens.MapBuilderView.Fresh(10, 8, "empty_field").Draft;
            draft.PlaceStart(new Cell(1, 4));
            draft.PlaceSpawn(2, new Cell(8, 2));
            draft.PlaceSpawn(5, new Cell(8, 6));
            return draft;
        }

        [Fact]
        public void EachSpawnIsAGoblinUntilToldAndTheReadoutWarnsNeverRefuses()
        {
            var trial = new MapTrial(Field(), Srd);

            Assert.Equal(new[] { 2, 5 }, trial.Slots);
            Assert.All(trial.Slots, s => Assert.Equal("goblin", trial.At(s)));
            Assert.Empty(trial.Warnings);
            Assert.Null(trial.Refusal);

            Assert.True(trial.Put(5, "ogre"));
            Assert.False(trial.Put(3, "ogre"));          // no spawn 3
            Assert.False(trial.Put(2, "a_beast_nobody_wrote"));

            // an ogre (CR 2, 450 XP) and a goblin is over a level 1 hero's high budget: a warning, and still playable
            Assert.Equal(450 + 50, trial.Xp);
            Assert.Contains(trial.Warnings, w => w.Key == MapTrial.OverBudgetKey);
            Assert.Null(trial.Refusal);

            trial.Level = 20;
            Assert.Empty(trial.Warnings);
            Assert.Equal(FightBudget.BudgetFor(20, EncounterDifficulty.Moderate), trial.Budget(EncounterDifficulty.Moderate));
        }

        [Fact]
        public void AMapWithNoSpawnOrWithProblemsCantBePlayed()
        {
            MapDraft none = Screens.MapBuilderView.Fresh(6, 6, "empty_field").Draft;
            Assert.Equal(MapTrial.NoSpawnsKey, new MapTrial(none, Srd).Refusal);

            MapDraft broken = Field();
            broken.Paint(broken.Start, Tile.Void);
            Assert.Equal(MapTrial.ProblemsKey, new MapTrial(broken, Srd).Refusal);
        }

        [Fact]
        public void TheThrowawayCampaignLoadsAndItsStoryWalksStraightIntoTheFight()
        {
            var trial = new MapTrial(Field(), Srd) { Class = "cleric", Level = 5 };
            trial.Put(5, "bandit");

            Assert.Null(trial.Write(_folder));

            Package pack = Package.Read(_folder);
            Assert.Empty(pack.Problems.Where(p => !p.IsACaution));
            Assert.True(pack.Maps.ContainsKey(MapTrial.MapId));

            // the spawns are 1 and 2 now, in the order they were, each with its monster
            MapLayout map = pack.Maps[MapTrial.MapId];
            Assert.Equal(new Cell(8, 2), map.SpawnAt(1));
            Assert.Equal(new Cell(8, 6), map.SpawnAt(2));
            Assert.Equal(Ground.Grass, pack.LookOn(MapTrial.MapId).At(new Cell(3, 3)));

            Hero hero = QuickHero.Make(Srd, trial.Class, trial.Level, "Tester");
            Assert.Equal(5, hero.Level);

            var run = new CampaignRun(Srd, pack, hero, new StandardResolver(new SeededRng(4)), new SeededRng(4));
            Assert.Empty(run.Book.Problems.Select(p => p.What));
            run.Start();
            while (run.Now == Scene.Line) run.Next();

            Assert.Equal(Scene.Fight, run.Now);
            Assert.Equal(MapTrial.MapId, run.Fight.MapId);
            Assert.Equal(new[] { "goblin", "bandit" }, run.Fight.Group.Select(g => g.Monster).ToArray());
        }
    }
}
