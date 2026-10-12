using System.Collections.Generic;
using Content.Inventory;
using Content.Items;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Characters;

namespace Content.Tests
{
    // SELLING WHAT YOU'RE WEARING (cc_task_i 5.1; inventory_decisions.md: "sell equipped items, with a confirmation
    // every time"): a worn item has its own Sell, which asks every time; a spare in the pack sells without taking the
    // worn one off; a full pack doesn't lose it; and a loan from an armory isn't the hero's to sell
    public class SellWornTests
    {
        static readonly Library Srd = Library.Srd();

        static Merchant Store() => new Merchant("store", Srd.Items, new[] { "longsword", "torch" });

        static PackView AtTheCounter(out Hero hero)
        {
            hero = SampleCampaignTests.Make(Srd, "fighter");
            return new PackView(hero, Srd.Items, Store());
        }

        [Fact]
        public void AWornThingHasASellThatAsksEveryTime()
        {
            PackView view = AtTheCounter(out Hero hero);
            Slot slot = hero.Equipment.In(Slot.Body) != null ? Slot.Body : Slot.MainHand;
            Item worn = hero.Equipment.In(slot);
            int copper = hero.Pack.Copper;

            Assert.True(view.SellsForWorn(slot) > 0);

            Deal asked = view.SellWorn(slot);
            Assert.False(asked.Done);
            Assert.Equal(Rebuff.Equipped, asked.Rebuff);
            Assert.Same(worn, hero.Equipment.In(slot));

            Deal sold = view.SellWorn(slot, confirmed: true);
            Assert.True(sold.Done);
            Assert.Null(hero.Equipment.In(slot));
            Assert.Equal(copper + sold.Copper, hero.Pack.Copper);
            Assert.Equal(0, hero.Pack.CountOf(worn.Id));
        }

        [Fact]
        public void AFullPackDoesntLoseAWornThingSold()
        {
            Actor hero = new Actor("hero", 1, new AbilityScores(), Allegiance.Hero);
            var pack = new Pack(1);
            var worn = new Equipment();
            Item sword = Srd.Items.Find("longsword");

            pack.Take(Srd.Items.Find("torch"));
            worn.Wear(sword, hero, "fighter");

            Deal sold = Store().Sell(pack, sword, 1, worn, hero, confirmed: true);

            Assert.True(sold.Done);
            Assert.True(sold.Copper > 0);
            Assert.Equal(sold.Copper, pack.Copper);
            Assert.Null(worn.In(Slot.MainHand));
        }

        [Fact]
        public void ASpareInThePackSellsWithoutTakingTheWornOneOff()
        {
            PackView view = AtTheCounter(out Hero hero);
            Item sword = Srd.Items.Find("longsword");

            hero.Wear(sword);
            hero.Pack.Take(sword);

            Assert.False(view.NeedsSellConfirmation("longsword"));
            Assert.True(view.Sell("longsword").Done);

            Assert.Equal("longsword", hero.Equipment.In(Slot.MainHand)?.Id);
            Assert.Equal(0, hero.Pack.CountOf("longsword"));

            // the last one is the worn one: that asks
            Assert.True(view.NeedsSellConfirmation("longsword"));
            Assert.Equal(Rebuff.Equipped, view.Sell("longsword").Rebuff);
        }

        [Fact]
        public void ALoanIsntTheHerosToSell()
        {
            PackView view = AtTheCounter(out Hero hero);

            hero.Borrow(Srd.Items.Find("longsword"));

            Assert.Equal(-1, view.SellsForWorn(Slot.MainHand));
            Assert.False(view.SellWorn(Slot.MainHand, confirmed: true).Done);
            Assert.Equal("longsword", hero.Equipment.In(Slot.MainHand)?.Id);
        }
    }
}
