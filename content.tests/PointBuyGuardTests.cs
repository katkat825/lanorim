using System;
using System.Linq;
using Content.Schema;
using Core.Characters;
using Xunit;

namespace Content.Tests
{
    // cc_task_ui-issues-10-01.md 1: Kathleen reached -2 points left, once -9, because + only stopped at 15
    public class PointBuyGuardTests
    {
        static readonly Library Srd = Library.Srd();

        static Creation.Creation Fresh(string cls = "fighter")
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);
            making.Pick(Srd.Classes.First(c => c.Id == cls));
            return making;
        }

        // lower everything to 8, then spend down to exactly `left`
        static Creation.Creation With(int left)
        {
            Creation.Creation making = Fresh();

            foreach (Ability a in Abilities.All)
                while (making.Lower(a)) { }

            Assert.Equal(Abilities.PointBuyBudget, making.Scores.PointBuyLeft);

            foreach (Ability a in Abilities.All)
                while (making.Scores.PointBuyLeft > left && making.Scores.Base(a) < 13 && making.Raise(a)) { }

            Assert.Equal(left, making.Scores.PointBuyLeft);
            return making;
        }

        [Fact]
        public void OnePointLeftCantTakeAThirteenToFourteen()
        {
            Creation.Creation making = With(1);
            Ability thirteen = Abilities.All.First(a => making.Scores.Base(a) == 13);

            Assert.False(making.CanRaise(thirteen));
            Assert.False(making.Raise(thirteen));
            Assert.Equal(13, making.Scores.Base(thirteen));
            Assert.Equal(1, making.Scores.PointBuyLeft);
        }

        [Fact]
        public void OnePointLeftStillBuysAOnePointStep()
        {
            Creation.Creation making = With(1);
            Ability low = Abilities.All.First(a => making.Scores.Base(a) < 13);

            Assert.True(making.Raise(low));
            Assert.Equal(0, making.Scores.PointBuyLeft);
            Assert.All(Abilities.All, a => Assert.False(making.CanRaise(a)));
        }

        [Fact]
        public void TwoPointsLeftTakeAThirteenToFourteen()
        {
            Creation.Creation making = With(2);
            Ability thirteen = Abilities.All.First(a => making.Scores.Base(a) == 13);

            Assert.True(making.Raise(thirteen));
            Assert.Equal(0, making.Scores.PointBuyLeft);
        }

        [Fact]
        public void FifteenIsStillTheCeilingAndEightTheFloor()
        {
            Creation.Creation making = Fresh();
            Ability top = Abilities.All.First(a => making.Scores.Base(a) == 15);
            Ability bottom = Abilities.All.First(a => making.Scores.Base(a) == 8);

            making.Lower(Abilities.All.First(a => making.Scores.Base(a) == 14));
            making.Lower(Abilities.All.First(a => making.Scores.Base(a) == 13));

            Assert.False(making.Raise(top));
            Assert.False(making.Lower(bottom));
        }

        // every way the array changes for a class, with no step in between: the standard array is the budget
        [Fact]
        public void EveryClassStartsOnTheBudgetAndABackgroundCostsNothing()
        {
            foreach (var cls in Srd.Classes)
            {
                var making = new Creation.Creation(Srd, Srd.Backgrounds);
                making.Pick(cls);
                Assert.Equal(0, making.Scores.PointBuyLeft);

                foreach (var bg in Srd.Backgrounds)
                {
                    making.Pick(bg);
                    Assert.Equal(0, making.Scores.PointBuyLeft);
                }
            }
        }

        [Fact]
        public void AThousandRandomStepsNeverGoBelowZero()
        {
            var random = new Random(20261001);
            Creation.Creation making = Fresh("mage");
            int refused = 0;

            for (int i = 0; i < 1000; i++)
            {
                Ability a = Abilities.All[random.Next(Abilities.Count)];
                bool up = random.Next(3) > 0;
                bool could = up ? making.CanRaise(a) : making.CanLower(a);
                bool did = up ? making.Raise(a) : making.Lower(a);

                Assert.Equal(could, did);
                if (!did) refused++;

                Assert.InRange(making.Scores.PointBuyLeft, 0, Abilities.PointBuyBudget);
                Assert.True(making.Scores.IsLegalPointBuy(out string problem), problem);
            }

            // the walk leans up, so it spends out and is refused often: the guard was really asked
            Assert.True(refused > 100, $"only {refused} refusals");
        }
    }
}
