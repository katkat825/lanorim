using System.Linq;
using Core.Magic;
using Xunit;

namespace Core.Tests
{
    // The two ways of paying for a leveled spell. What matters is that each one is correct on its
    // own terms, and that neither is strictly better than the other - which is what the 6+ cap in
    // points mode exists to guarantee.
    public class SpellResourceTests
    {
        // --- mode A: slots ---------------------------------------------------------------------

        [Fact]
        public void AFullCasterStartsWithTwoFirstLevelSlots()
        {
            var slots = SpellSlots.For(CasterProgression.Full, 1);

            Assert.Equal(2, slots.Maximum(1));
            Assert.Equal(0, slots.Maximum(2));
            Assert.Equal(1, slots.Highest);
        }

        [Fact]
        public void SpendingASlotTakesItFromTheLevelItWasCastAt()
        {
            var slots = SpellSlots.For(CasterProgression.Full, 5);

            Assert.True(slots.Pay(3));

            Assert.Equal(1, slots.Remaining(3));
            Assert.Equal(4, slots.Remaining(1));
        }

        // UPCASTING IS NOT A SPECIAL CASE: castLevel is the level the player chose, so a 1st-level
        // spell thrown at 3rd spends a 3rd-level slot because that is what it was cast at
        [Fact]
        public void UpcastingSpendsTheSlotItWasCastAtNotTheSpellsOwnLevel()
        {
            var slots = SpellSlots.For(CasterProgression.Full, 5);

            Assert.True(slots.Pay(3));

            Assert.Equal(4, slots.Remaining(1));
            Assert.Equal(1, slots.Remaining(3));
        }

        // it climbs when the chosen level is empty, and never climbs down
        [Fact]
        public void AnEmptyLevelClimbsToTheNextOneUp()
        {
            var slots = SpellSlots.For(CasterProgression.Full, 5);

            Assert.True(slots.Pay(3));
            Assert.True(slots.Pay(3));
            Assert.Equal(0, slots.Remaining(3));

            // no 3rd left, so it is not refused - it spends nothing lower and nothing at all,
            // because a level-5 full caster has no 4th either
            Assert.False(slots.CanPay(3));

            // and it never climbed DOWN - the 1st-level slots are untouched
            Assert.Equal(4, slots.Remaining(1));
        }

        [Fact]
        public void ASlotCasterWithNothingLeftIsRefused()
        {
            var slots = SpellSlots.For(CasterProgression.Full, 1);

            Assert.True(slots.Pay(1));
            Assert.True(slots.Pay(1));

            Assert.False(slots.CanPay(1));
            Assert.False(slots.Pay(1));
            Assert.Equal(0, slots.Highest);
        }

        [Fact]
        public void ALongRestRefillsEverySlotAndAShortRestRefillsNone()
        {
            var slots = SpellSlots.For(CasterProgression.Full, 5);

            slots.Pay(1);
            slots.Pay(3);

            slots.Restore(Rest.Short);
            Assert.Equal(3, slots.Remaining(1));

            slots.Restore(Rest.Long);
            Assert.Equal(4, slots.Remaining(1));
            Assert.Equal(2, slots.Remaining(3));
        }

        // the half caster's two defining facts, both from the table rather than from code
        [Fact]
        public void AHalfCasterHasTwoFirstLevelSlotsAtFirstLevel()
        {
            // SRD 5.2.1 (p.53): a Paladin casts from level 1, with two level 1 slots
            var slots = SpellSlots.For(CasterProgression.Half, 1);

            Assert.Equal(1, slots.Highest);
            Assert.Equal(2, slots.Maximum(1));
        }

        [Fact]
        public void AHalfCasterNeverPassesFifthLevel()
        {
            var slots = SpellSlots.For(CasterProgression.Half, 20);

            Assert.Equal(2, slots.Maximum(5));
            Assert.Equal(0, slots.Maximum(6));
            Assert.Equal(5, slots.Highest);
        }


        // --- mode B: points --------------------------------------------------------------------

        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 3)]
        [InlineData(3, 5)]
        [InlineData(4, 6)]
        [InlineData(5, 7)]
        [InlineData(6, 9)]
        [InlineData(7, 10)]
        [InlineData(8, 11)]
        [InlineData(9, 13)]
        public void EverySpellLevelHasItsPrice(int level, int cost)
        {
            Assert.Equal(cost, SpellPoints.CostOf(level));
        }

        [Fact]
        public void CastingSpendsThePriceOfTheLevelItWasCastAt()
        {
            var points = new SpellPoints(20);

            Assert.True(points.Pay(1));
            Assert.Equal(18, points.Remaining);

            Assert.True(points.Pay(3));
            Assert.Equal(13, points.Remaining);
        }

        [Fact]
        public void UpcastingPaysTheHigherLevelsPrice()
        {
            var cheap = new SpellPoints(20);
            var dear = new SpellPoints(20);

            cheap.Pay(1);
            dear.Pay(3);

            Assert.Equal(18, cheap.Remaining);
            Assert.Equal(15, dear.Remaining);
        }

        [Fact]
        public void APoolTooSmallForTheLevelIsRefused()
        {
            var points = new SpellPoints(4);

            Assert.False(points.CanPay(3));
            Assert.False(points.Pay(3));
            Assert.Equal(4, points.Remaining);
        }

        // THE CAP IS THE WHOLE REASON POINTS IS NOT STRICTLY BETTER THAN SLOTS. A pool big enough
        // to buy four 6th-level spells must still only buy one of them in a day.
        [Fact]
        public void ASixthLevelSpellIsCastableOnlyOncePerLongRest()
        {
            var points = new SpellPoints(100);

            Assert.True(points.CanPay(6));
            Assert.True(points.Pay(6));

            Assert.False(points.CanPay(6));
            Assert.False(points.Pay(6));

            // and it cost nothing to be refused
            Assert.Equal(91, points.Remaining);
        }

        [Fact]
        public void TheCapIsPerLevelNotAcrossAllHighLevels()
        {
            var points = new SpellPoints(100);

            Assert.True(points.Pay(6));
            Assert.True(points.Pay(7));
            Assert.True(points.Pay(8));

            Assert.False(points.CanPay(6));
            Assert.True(points.CanPay(9));
        }

        [Fact]
        public void LowLevelSpellsAreNotCapped()
        {
            var points = new SpellPoints(100);

            for (int i = 0; i < 5; i++) Assert.True(points.Pay(5));
        }

        [Fact]
        public void ALongRestRefillsThePoolAndClearsTheHighLevelFlags()
        {
            var points = new SpellPoints(100);

            points.Pay(6);
            points.Pay(1);

            points.Restore(Rest.Short);
            Assert.False(points.CanPay(6));

            points.Restore(Rest.Long);

            Assert.Equal(100, points.Remaining);
            Assert.True(points.CanPay(6));
        }

        [Fact]
        public void AHalfCasterGetsASmallerPoolRoundingItsLevelUp()
        {
            // half its levels, rounded up (SRD 5.2.1 p.25): a level 1 Paladin pools like a level 1
            // full caster
            Assert.Equal(SpellPoints.PoolFor(CasterProgression.Full, 1),
                         SpellPoints.PoolFor(CasterProgression.Half, 1));

            Assert.True(SpellPoints.PoolFor(CasterProgression.Half, 10) <
                        SpellPoints.PoolFor(CasterProgression.Full, 10));
        }


        // --- the two modes against each other ---------------------------------------------------

        // Neither mode may be the obvious pick. Slots is the faithful one; points buys flexibility
        // at the price of the cap, and this is the line that says the cap is actually doing it.
        [Fact]
        public void NeitherModeCanCastMoreThanOneSixthLevelSpellInADay()
        {
            var slots = SpellSlots.For(CasterProgression.Full, 11);
            var points = SpellPoints.For(CasterProgression.Full, 11);

            Assert.Equal(1, slots.Maximum(6));

            Assert.True(slots.Pay(6));
            Assert.False(slots.CanPay(6));

            Assert.True(points.Pay(6));
            Assert.False(points.CanPay(6));
        }

        // the interface is the only thing Casting sees, and it must be answerable without knowing
        // which side of it is holding the state
        // A POOL IS JUST A NUMBER, so without a ceiling a 5th-level caster holding 27 points could
        // pay the 13 a 9th-level spell costs while the slot table gives them nothing above 3rd.
        // The 6+ cap does not catch it - that limits how often, not whether.
        [Fact]
        public void PointsCannotReachASpellLevelTheSlotTableWouldNotHaveGiven()
        {
            var slots = SpellSlots.For(CasterProgression.Full, 5);
            var points = SpellPoints.For(CasterProgression.Full, 5);

            Assert.Equal(3, slots.Highest);
            Assert.Equal(3, points.HighestLevel);

            Assert.True(points.Remaining >= SpellPoints.CostOf(9));
            Assert.False(points.CanPay(9));
            Assert.Equal(3, points.Highest);
        }

        [Fact]
        public void AHalfCasterOnPointsNeverReachesPastFifth()
        {
            var points = SpellPoints.For(CasterProgression.Half, 20);

            Assert.Equal(5, points.HighestLevel);
            Assert.False(points.CanPay(6));
        }

        [Theory]
        [InlineData(SpellResourceMode.Slots)]
        [InlineData(SpellResourceMode.Points)]
        public void BothModesAnswerTheSameThreeQuestions(SpellResourceMode mode)
        {
            ISpellResource resource = mode == SpellResourceMode.Points
                ? SpellPoints.For(CasterProgression.Full, 5)
                : SpellSlots.For(CasterProgression.Full, 5);

            Assert.Equal(mode, resource.Mode);
            Assert.True(resource.CanPay(1));
            Assert.True(resource.Pay(1));

            resource.Restore(Rest.Long);

            Assert.True(resource.CanPay(3));
            Assert.Equal(3, resource.Highest);
            Assert.NotEmpty(resource.Describe());
        }
    }
}
