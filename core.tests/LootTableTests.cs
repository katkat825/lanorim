using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Localization;
using Core.Tables;

namespace Core.Tests
{
    // loot tables on the GM screen. the screen picks and counts; what the hero can carry, and who
    // they are, is content's (content.tests/LootTests.cs).
    public class LootTableTests
    {
        // one, two and three: six tickets, the same walk the encounter tests use
        static LootTable Pockets(Visibility visibility = Visibility.Hidden) =>
            new LootTable("goblin_pockets", new[]
            {
                new LootEntry("lint", LootKind.Nothing, 1),
                new LootEntry("coins", LootKind.Find, 2,
                              gold: new Purse(DiceRoll.Parse("3d6"), 10), speaks: true),
                new LootEntry("torches", LootKind.Find, 3,
                              new[] { new Lot("torch", DiceRoll.Parse("1d4")) }),
            }, visibility);

        [Theory]
        [InlineData(1, "lint")]
        [InlineData(2, "coins")]
        [InlineData(3, "coins")]
        [InlineData(4, "torches")]
        [InlineData(6, "torches")]
        public void TheTicketWalksTheWeights(int ticket, string entry)
        {
            LootRoll roll = new GmScreen(new ScriptedRng(ticket, 1)).Open(Pockets());

            Assert.Equal(entry, roll.Entry.Id);
            Assert.Equal("d6", roll.PickRoll.Dice);
        }

        [Fact]
        public void GoldIsTheDiceTimesTheMultiplier()
        {
            // ticket 2 is the coins, then three dice of 4, 5 and 6
            LootRoll roll = new GmScreen(new ScriptedRng(2, 4, 5, 6)).Open(Pockets());

            Assert.Equal(150, roll.Gold);
            Assert.Empty(roll.Found);

            GmRoll gold = roll.Rolls.Single(r => r.Purpose == RollPurpose.Gold);

            Assert.Equal("3d6", gold.Dice);
            Assert.Equal(15, gold.Total);
        }

        [Fact]
        public void AFlatPurseRollsNothing()
        {
            var table = new LootTable("purse", new[]
            {
                new LootEntry("five", LootKind.Find, gold: new Purse(DiceRoll.Flat(5))),
            });

            LootRoll roll = new GmScreen(new ScriptedRng(1)).Open(table);

            Assert.Equal(5, roll.Gold);
            Assert.Single(roll.Rolls);
        }

        [Fact]
        public void ACountIsRolledBehindTheScreenToo()
        {
            LootRoll roll = new GmScreen(new ScriptedRng(5, 3)).Open(Pockets());

            Assert.Equal(3, roll.Found.Single(f => f.Item == "torch").Count);
            Assert.All(roll.Rolls, r => Assert.True(r.IsHidden));
            Assert.Equal(new[] { RollPurpose.Pick, RollPurpose.Count },
                         roll.Rolls.Select(r => r.Purpose));
        }

        [Fact]
        public void ATableCanBeOpenedInTheOpen()
        {
            LootRoll roll = new GmScreen(new ScriptedRng(1)).Open(Pockets(Visibility.Shown));

            Assert.All(roll.Rolls, r => Assert.False(r.IsHidden));
        }

        [Fact]
        public void NothingIsAFindWithNothingInIt()
        {
            LootRoll roll = new GmScreen(new ScriptedRng(1)).Open(Pockets());

            Assert.Equal("lint", roll.Entry.Id);
            Assert.True(roll.IsEmpty);
            Assert.Empty(roll.LineKeys);
        }

        [Fact]
        public void OnlyAnEntryThatAsksForALineHasOne()
        {
            LootRoll roll = new GmScreen(new ScriptedRng(2, 1)).Open(Pockets());

            Assert.Equal("encounter.goblin_pockets.loot.coins", roll.LineKeys.Single());
            Assert.Equal(new[] { "encounter.goblin_pockets.loot.coins" }, Pockets().Keys());
            Assert.All(Pockets().Keys(), k => Assert.True(KeyConventions.IsWellFormed(k), k));
        }


        // --- nesting ---------------------------------------------------------------------------

        static readonly LootTable Gems = new LootTable("gems", new[]
        {
            new LootEntry("agate", LootKind.Find, 1, new[] { new Lot("agate", DiceRoll.Flat(2)) },
                          speaks: true),
        });

        static readonly LootTable Hoard = new LootTable("hoard", new[]
        {
            new LootEntry("coins", LootKind.Find, 1, gold: new Purse(DiceRoll.Flat(40))),
            new LootEntry("gem_pouch", LootKind.Table, 1, table: "gems", speaks: true),
        });

        [Fact]
        public void ANestedEntryRollsTheTableItNames()
        {
            LootRoll roll = new GmScreen(new ScriptedRng(2, 1))
                .Open(Hoard, new LootTables(new[] { Hoard, Gems }));

            Assert.Equal("gem_pouch", roll.Entry.Id);
            Assert.Equal("agate", roll.Inner.Entry.Id);
            Assert.Equal(2, roll.Found.Single().Count);

            // the outer pick, then the inner one, in the order the dice were thrown
            Assert.Equal(new[] { "hoard", "gems" }, roll.Rolls.Select(r => r.Table));
            Assert.Equal(new[] { "encounter.hoard.loot.gem_pouch", "encounter.gems.loot.agate" },
                         roll.LineKeys);
        }

        [Fact]
        public void AHandBuiltLoopComesToAnEmptyChestNotACrash()
        {
            var ouroboros = new LootTable("ouroboros", new[]
            {
                new LootEntry("again", LootKind.Table, table: "ouroboros"),
                new LootEntry("coin", LootKind.Find, gold: new Purse(DiceRoll.Flat(1))),
            });

            var tables = new LootTables(new[] { ouroboros });

            Assert.Equal(new[] { "ouroboros", "ouroboros" }, tables.Cycle());

            // the loop entry offers nothing, so it is never in the draw: only the coin is
            LootRoll roll = new GmScreen(new ScriptedRng(1)).Open(ouroboros, tables);

            Assert.Equal("coin", roll.Entry.Id);
            Assert.Contains("again", roll.WeightedOut);
        }

        [Fact]
        public void ALoopThroughTwoTablesIsFound()
        {
            var a = new LootTable("a", new[] { new LootEntry("to_b", LootKind.Table, table: "b") });
            var b = new LootTable("b", new[] { new LootEntry("to_a", LootKind.Table, table: "a") });
            var c = new LootTable("c", new[] { new LootEntry("to_a", LootKind.Table, table: "a") });

            Assert.Equal(new[] { "a", "b", "a" }, new LootTables(new[] { a, b, c }).Cycle());
            Assert.Empty(new LootTables(new[] { c, Gems, Hoard }).Cycle());
        }


        // --- who it is for ---------------------------------------------------------------------

        [Fact]
        public void AnEntryTheHeroCannotUseIsWeightedOutBeforeThePick()
        {
            // torches are off: the draw is lint 1 and coins 2, so a d3 and ticket 3 is the coins
            LootRoll roll = new GmScreen(new ScriptedRng(3, 1))
                .Open(Pockets(), usable: item => item != "torch");

            Assert.Equal("d3", roll.PickRoll.Dice);
            Assert.Equal("coins", roll.Entry.Id);
            Assert.Equal(new[] { "torches" }, roll.WeightedOut);
        }

        [Fact]
        public void WhenNothingIsLeftNoDiceAreThrown()
        {
            var table = new LootTable("wand_case", new[]
            {
                new LootEntry("wand", LootKind.Find, items: new[] { new Lot("wand", DiceRoll.Flat(1)) }),
            });

            LootRoll roll = new GmScreen(new ScriptedRng(1)).Open(table, usable: _ => false);

            Assert.Null(roll.PickRoll);
            Assert.Null(roll.Entry);
            Assert.True(roll.IsEmpty);
            Assert.Empty(roll.Rolls);
        }

        [Fact]
        public void ANestedTableWithNothingForTheHeroIsWeightedOutWhole()
        {
            LootRoll roll = new GmScreen(new ScriptedRng(1))
                .Open(Hoard, new LootTables(new[] { Hoard, Gems }), item => item != "agate");

            Assert.Equal("coins", roll.Entry.Id);
            Assert.Equal(new[] { "gem_pouch" }, roll.WeightedOut);
        }


        // --- on the screen ---------------------------------------------------------------------

        sealed class Opening : ScreenObserver
        {
            public readonly List<GmRoll> Rolls = new List<GmRoll>();

            public readonly List<LootRoll> Openings = new List<LootRoll>();

            public override void Rolled(GmRoll roll) => Rolls.Add(roll);

            public override void Opened(LootRoll result) => Openings.Add(result);
        }

        [Fact]
        public void TheScreenHearsEveryRollAndOneOpeningHidden()
        {
            var watching = new Opening();
            var log = new ScreenLog();

            new GmScreen(new ScriptedRng(2, 1), watching, log)
                .Open(Hoard, new LootTables(new[] { Hoard, Gems }));

            Assert.Equal(2, watching.Rolls.Count);
            Assert.All(watching.Rolls, r => Assert.True(r.IsHidden));

            // one opening for the whole find, however deep it went
            Assert.Single(watching.Openings);
            Assert.Contains(log.Lines, l => l.StartsWith("behind the screen: pick d2"));
            Assert.Contains(log.Lines, l => l == "== hoard: gem_pouch - 2 agate");
        }
    }
}
