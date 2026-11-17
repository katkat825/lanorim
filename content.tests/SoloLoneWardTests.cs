using System;
using System.Linq;
using Content.Classes;
using Content.Combat;
using Content.Creation;
using Content.Schema;
using Content.Sheet;
using Content.SoloPlay;
using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Content.Tests
{
    // 3.4, Lone Ward (cc_task_cb Part 2; Kathleen 2026-11-16): the Mage, Cleric and Druid gain temporary hit points
    // equal to proficiency bonus + level, until the next rest, once a long rest, an action and no slot. Lanorim's own,
    // never under an SRD name. Behind SoloRules.CasterWard
    [Collection(SoloRulesCollection.Name)]
    public class SoloLoneWardTests
    {
        static readonly Library Srd = Library.Srd();

        static T With<T>(bool ward, Func<T> run)
        {
            bool was = SoloRules.CasterWard;

            try
            {
                SoloRules.CasterWard = ward;
                return run();
            }
            finally { SoloRules.CasterWard = was; }
        }

        static Hero Hero(string cls, int level = 1) => QuickHero.Make(Srd, cls, level, "Tess");

        [Theory]
        [InlineData("mage", true)]
        [InlineData("cleric", true)]
        [InlineData("druid", true)]
        [InlineData("paladin", false)]
        [InlineData("fighter", false)]
        public void OnlyTheThreeFullCastersHaveItAndOnlyWhileTheSwitchIsOn(string cls, bool has)
        {
            Assert.Equal(has, With(true, () => Hero(cls).FeatureCalled(SoloRules.WardId) != null));
            Assert.Null(With(false, () => Hero(cls).FeatureCalled(SoloRules.WardId)));
        }

        [Fact]
        public void ItIsLanorimsOwnAndAnActionOnceALongRest()
        {
            Feature ward = With(true, () => Hero("mage").FeatureCalled(SoloRules.WardId));

            Assert.True(ward.LanorimsOwn);
            Assert.Equal(Trait.Boost, ward.Trait);
            Assert.Equal(Core.Combat.Spend.Action, ward.UseTime);
            Assert.Equal(Recharge.Long, ward.Recharge);
            Assert.Equal(Manoeuvre.None, ward.Manoeuvres);
        }

        [Theory]
        [InlineData(1, 3)]
        [InlineData(2, 4)]
        [InlineData(4, 6)]
        [InlineData(5, 8)]
        [InlineData(9, 13)]
        [InlineData(20, 26)]
        public void ItGivesProficiencyBonusPlusLevel(int level, int expected)
        {
            int temporary = With(true, () =>
            {
                Hero mage = Hero("mage", level);
                Assert.Equal(expected, mage.Actor.ProficiencyBonus + level);

                Assert.True(mage.Invoke(mage.FeatureCalled(SoloRules.WardId), new StandardResolver(new SeededRng(1))));
                return mage.Actor.Health.Temporary;
            });

            Assert.Equal(expected, temporary);
        }

        [Fact]
        public void TheCardOffersItOnceAndItDoesNotStackAndALongRestBringsItBack()
        {
            With(true, () =>
            {
                Hero cleric = Hero("cleric", 5);
                var resolver = new StandardResolver(new SeededRng(1));

                Feature ward = Assert.Single(BeforeTheFight.Features(cleric));
                Assert.Equal(SoloRules.WardId, ward.Id);

                // a bigger pool already up stays (SRD: temporary hit points don't stack)
                cleric.Actor.Health.GrantTemporary(20);
                Assert.True(BeforeTheFight.Use(cleric, ward, resolver, out _));
                Assert.Equal(20, cleric.Actor.Health.Temporary);

                Assert.Empty(BeforeTheFight.Features(cleric));
                Assert.Equal(0, cleric.UsesLeft(ward));

                cleric.LongRest();
                Assert.Equal(1, cleric.UsesLeft(ward));
                Assert.Single(BeforeTheFight.Features(cleric));
                return 0;
            });
        }

        [Fact]
        public void AdrenalineRushIsNotOfferedBeforeTheFightAndStillDashes()
        {
            Hero orc = QuickHero.Make(Srd, "fighter", 1, "Tess", species: "orc");
            Feature rush = orc.FeatureCalled("adrenaline_rush");

            Assert.Equal(Manoeuvre.Dash, rush.Manoeuvres);
            Assert.Empty(BeforeTheFight.Features(orc));

            Assert.True(orc.Invoke(rush, new StandardResolver(new SeededRng(1))));
            Assert.Equal(orc.Actor.ProficiencyBonus, orc.Actor.Health.Temporary);
        }
    }
}
