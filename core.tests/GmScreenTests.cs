using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Localization;
using Core.Tables;

namespace Core.Tests
{
    // random-encounter tables and the rolls the GM makes behind the screen
    // (decisions_checklist.md section 3). the screen reports; nothing here starts a fight.
    public class GmScreenTests
    {
        static readonly Trigger OnFifteen = new Trigger(DiceRoll.Parse("1d20"), 15);

        // one, two and three: six tickets, so a d6's worth of script walks every one of them
        static EncounterTable Weighted(Trigger trigger, Visibility visibility = Visibility.Hidden) =>
            new EncounterTable("north_road", trigger, new[]
            {
                new EncounterEntry("quiet", EntryKind.Nothing, 1),
                new EncounterEntry("crows", EntryKind.Line, 2),
                new EncounterEntry("goblins", EntryKind.Fight, 3, new[]
                {
                    new Band("goblin", DiceRoll.Parse("1d4")),
                    new Band("goblin_boss", DiceRoll.Flat(1)),
                }, "road"),
            }, visibility);

        [Fact]
        public void BelowTheThresholdNothingHappensAndNothingIsPicked()
        {
            TableRoll roll = new GmScreen(new ScriptedRng(14)).Consult(Weighted(OnFifteen));

            Assert.False(roll.Triggered);
            Assert.Equal(14, roll.TriggerRoll.Total);
            Assert.Null(roll.PickRoll);
            Assert.Null(roll.Entry);
            Assert.False(roll.Fights);
            Assert.Equal("", roll.LineKey);
            Assert.Single(roll.Rolls);
        }

        [Fact]
        public void OnTheThresholdItFires()
        {
            TableRoll roll = new GmScreen(new ScriptedRng(15, 1)).Consult(Weighted(OnFifteen));

            Assert.True(roll.Triggered);
            Assert.Equal("quiet", roll.Entry.Id);
        }

        [Fact]
        public void ATableThatAlwaysFiresRollsNoTrigger()
        {
            TableRoll roll = new GmScreen(new ScriptedRng(2)).Consult(Weighted(Trigger.Always));

            Assert.True(roll.Triggered);
            Assert.Null(roll.TriggerRoll);
            Assert.Equal("crows", roll.Entry.Id);
        }

        [Theory]
        [InlineData(1, "quiet")]
        [InlineData(2, "crows")]
        [InlineData(3, "crows")]
        [InlineData(4, "goblins")]
        [InlineData(6, "goblins")]
        public void TheTicketWalksTheWeights(int ticket, string expected)
        {
            TableRoll roll = new GmScreen(new ScriptedRng(ticket, 1))
                .Consult(Weighted(Trigger.Always));

            Assert.Equal("d6", roll.PickRoll.Dice);
            Assert.Equal(ticket, roll.PickRoll.Total);
            Assert.Equal(expected, roll.Entry.Id);
        }

        [Fact]
        public void EveryTicketOnceGivesTheWeightsExactly()
        {
            // the fight rolls its 1d4 as well, so the script is ticket, count - and every
            // ticket comes up a hundred times
            var script = new List<int>();

            for (int i = 0; i < 100; i++)
                for (int ticket = 1; ticket <= 6; ticket++)
                {
                    script.Add(ticket);

                    if (ticket >= 4) script.Add(2);
                }

            var screen = new GmScreen(new ScriptedRng(script.ToArray()));
            EncounterTable table = Weighted(Trigger.Always);

            var tally = Enumerable.Range(0, 600)
                                  .Select(_ => screen.Consult(table).Entry.Id)
                                  .GroupBy(id => id)
                                  .ToDictionary(g => g.Key, g => g.Count());

            Assert.Equal(100, tally["quiet"]);
            Assert.Equal(200, tally["crows"]);
            Assert.Equal(300, tally["goblins"]);
        }

        [Fact]
        public void AFightRollsItsCountsAndAFlatCountIsNotRolled()
        {
            TableRoll roll = new GmScreen(new ScriptedRng(5, 3)).Consult(Weighted(Trigger.Always));

            Assert.True(roll.Fights);
            Assert.Equal("road", roll.Map);
            Assert.Equal(3, roll.Group.Single(g => g.Monster == "goblin").Count);
            Assert.Equal(1, roll.Group.Single(g => g.Monster == "goblin_boss").Count);

            Assert.Equal(new[] { RollPurpose.Pick, RollPurpose.Count },
                         roll.Rolls.Select(r => r.Purpose));
        }

        [Fact]
        public void ANothingEntryOwesNoLineAndTheOthersDo()
        {
            EncounterTable table = Weighted(OnFifteen);

            Assert.Equal(new[] { "encounter.north_road.line.crows", "encounter.north_road.line.goblins" },
                         table.Keys());

            Assert.All(table.Keys(), key => Assert.True(KeyConventions.IsWellFormed(key), key));

            Assert.Equal("", table.LineKey(table.Entries[0]));
        }


        // --- the surface behind the screen --------------------------------------------------------

        sealed class Listening : ScreenObserver
        {
            public readonly List<GmRoll> Rolls = new List<GmRoll>();

            public readonly List<TableRoll> Results = new List<TableRoll>();

            public override void Rolled(GmRoll roll) => Rolls.Add(roll);

            public override void Consulted(TableRoll result) => Results.Add(result);
        }

        sealed class Broken : ScreenObserver
        {
            public override void Rolled(GmRoll roll) => throw new InvalidOperationException("no");
        }

        [Fact]
        public void TheTableHearsEveryRollAndIsToldItWasHidden()
        {
            var ears = new Listening();

            TableRoll roll = new GmScreen(new ScriptedRng(17, 5, 2), ears)
                .Consult(Weighted(OnFifteen));

            Assert.Equal(3, ears.Rolls.Count);
            Assert.All(ears.Rolls, r => Assert.True(r.IsHidden));
            Assert.Same(roll, ears.Results.Single());
        }

        [Fact]
        public void ATableRolledInTheOpenSaysSo()
        {
            var ears = new Listening();

            new GmScreen(new ScriptedRng(17, 1), ears)
                .Consult(Weighted(OnFifteen, Visibility.Shown));

            Assert.All(ears.Rolls, r => Assert.False(r.IsHidden));
        }

        [Fact]
        public void AnAsideIsHiddenUnlessAskedOtherwise()
        {
            var ears = new Listening();

            GmRoll roll = new GmScreen(new ScriptedRng(4), ears).Roll(DiceRoll.Parse("1d6"));

            Assert.True(roll.IsHidden);
            Assert.Equal(RollPurpose.Aside, roll.Purpose);
            Assert.Equal(4, roll.Total);
            Assert.Same(roll, ears.Rolls.Single());
        }

        [Fact]
        public void ABrokenListenerDoesNotLoseTheEncounter()
        {
            var ears = new Listening();
            var screen = new GmScreen(new ScriptedRng(20, 6, 1), new Broken(), ears);

            TableRoll roll = screen.Consult(Weighted(OnFifteen));

            Assert.True(roll.Fights);
            Assert.Equal(3, ears.Rolls.Count);
            Assert.Equal(3, screen.Failures.Count);
        }
    }
}
