using Content.Classes;
using Content.Schema;
using Content.SoloPlay;
using Core.Magic;
using Xunit;

namespace Content.Tests
{
    // 3.1 (cc_task_h): the Mage, Cleric and Druid have one more 1st-level slot at every level, or a 1st-level
    // spell's price more in points, so the two modes stay even. The Paladin doesn't change. Behind its switch
    [Collection(SoloRulesCollection.Name)]
    public class SoloCasterSlotTests
    {
        static readonly Library Srd = Library.Srd();

        static int FirstLevelSlots(string cls, int level) =>
            ((SpellSlots)Srd.Class(cls).ResourceAt(level, SpellResourceMode.Slots)).Maximum(1);

        static int Points(string cls, int level) =>
            ((SpellPoints)Srd.Class(cls).ResourceAt(level, SpellResourceMode.Points)).Maximum;

        [Theory]
        [InlineData("mage")]
        [InlineData("cleric")]
        [InlineData("druid")]
        public void AFullCasterHasThreeFirstLevelSlotsAtLevelOneAndFiveAtFive(string cls)
        {
            bool was = SoloRules.CasterSlot;

            try
            {
                SoloRules.CasterSlot = true;
                Assert.Equal(3, FirstLevelSlots(cls, 1));
                Assert.Equal(5, FirstLevelSlots(cls, 5));
                Assert.Equal(SpellPoints.PoolFor(CasterProgression.Full, 1) + SpellPoints.CostOf(1), Points(cls, 1));

                SoloRules.CasterSlot = false;
                Assert.Equal(2, FirstLevelSlots(cls, 1));
                Assert.Equal(SpellPoints.PoolFor(CasterProgression.Full, 1), Points(cls, 1));
            }
            finally { SoloRules.CasterSlot = was; }
        }

        [Fact]
        public void APaladinDoesNotChange()
        {
            bool was = SoloRules.CasterSlot;

            try
            {
                SoloRules.CasterSlot = true;
                Assert.Equal(2, FirstLevelSlots("paladin", 1));
                Assert.Equal(0, Srd.Class("paladin").SoloBonusSlots);
                Assert.Equal(1, Srd.Class("mage").SoloBonusSlots);
                Assert.Equal(0, Srd.Class("fighter").SoloBonusSlots);
            }
            finally { SoloRules.CasterSlot = was; }
        }
    }
}
