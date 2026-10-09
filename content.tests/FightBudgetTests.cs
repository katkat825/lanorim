using System.Linq;
using Content.Campaigns;
using Content.Encounters;
using Content.Schema;
using Core.Dice;
using Core.Tables;
using Xunit;

namespace Content.Tests
{
    // 3.5 (cc_task_h): THE SOLO FIGHT BUDGET. A fight that says its level and difficulty is held to the SRD's XP
    // Budget per Character for one character, and to the solo enemy cap; it's warned, never refused
    public class FightBudgetTests
    {
        static readonly Library Srd = Library.Srd();

        static EncounterEntry Fight(int level, EncounterDifficulty difficulty, params (string Monster, int Count)[] bands) =>
            new EncounterEntry("test", EntryKind.Fight, monsters: bands.Select(b => new Band(b.Monster, DiceRoll.Flat(b.Count))).ToList(),
                               level: level, difficulty: difficulty);

        static string[] Said(EncounterEntry fight) =>
            FightBudget.Check(fight, Srd.Bestiary.Find, "f", "w").Select(p => p.What).ToArray();

        [Fact]
        public void TheSrdTablesAreRead()
        {
            Assert.Equal(50, FightBudget.BudgetFor(1, EncounterDifficulty.Low));
            Assert.Equal(100, FightBudget.BudgetFor(1, EncounterDifficulty.High));
            Assert.Equal(22000, FightBudget.BudgetFor(20, EncounterDifficulty.High));
            Assert.Equal(25, FightBudget.Xp(Srd.Bestiary.Find("giant_rat")));
            Assert.Equal(50, FightBudget.Xp(Srd.Bestiary.Find("wolf")));
            Assert.Equal(200, FightBudget.Xp(Srd.Bestiary.Find("goblin_boss")));
            Assert.Equal(new[] { 2, 2, 3, 3, 4, 4, 5 }, new[] { 1, 2, 3, 4, 5, 10, 11 }.Select(FightBudget.Cap));
        }

        [Fact]
        public void AFightWithinItIsQuiet() =>
            Assert.Empty(Said(Fight(1, EncounterDifficulty.Low, ("giant_rat", 2))));

        [Fact]
        public void OverTheBudgetOrTheCapIsAWarning()
        {
            Assert.Single(Said(Fight(1, EncounterDifficulty.Low, ("wolf", 2))), s => s.Contains("over a low fight's 50 XP"));
            Assert.Contains(Said(Fight(1, EncounterDifficulty.High, ("giant_rat", 3))), s => s.Contains("over the solo cap of 2"));
            Assert.True(FightBudget.Check(Fight(1, EncounterDifficulty.Low, ("wolf", 2)), Srd.Bestiary.Find, "f", "w")
                                   .All(p => p.IsACaution));
        }

        [Fact]
        public void AFightWithNoLevelGetsANote() =>
            Assert.Contains(Said(Fight(0, EncounterDifficulty.Moderate, ("wolf", 1))), s => s.Contains("can't be checked"));

        // the sample campaign's four fights are within their budgets, so it loads with no budget warnings
        [Fact]
        public void TheSampleCampaignIsWithinItsBudgets()
        {
            Package pack = Package.Read(SampleCampaignTests.Folder);

            Assert.DoesNotContain(pack.Cautions, c => c.What.Contains("XP") || c.What.Contains("solo cap") || c.What.Contains("can't be checked"));
        }
    }
}
