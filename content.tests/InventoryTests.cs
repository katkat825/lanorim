using System.Linq;
using Content.Inventory;
using Content.Items;
using Content.Schema;
using Core.Characters;
using Core.Localization;

namespace Content.Tests
{
    public class PackTests
    {
        static readonly ItemShelf Shelf = Library.Srd().Items;

        static Item Potion => Shelf.Find("potion_of_healing");

        static Item Sword => Shelf.Find("longsword");

        [Fact]
        public void ForticySlots() => Assert.Equal(40, new Pack().Capacity);

        [Fact]
        public void AStackTakesOneSlotHoweverManyAreInIt()
        {
            var pack = new Pack();

            pack.Take(Potion, 5000);

            Assert.Equal(1, pack.Used);
            Assert.Equal(5000, pack.CountOf("potion_of_healing"));
        }

        [Fact]
        public void PastTheStackLimitItTakesASecondSlot()
        {
            var pack = new Pack();

            pack.Take(Potion, Item.StackLimit + 1);

            Assert.Equal(2, pack.Used);
            Assert.Equal(Item.StackLimit + 1, pack.CountOf("potion_of_healing"));
        }

        [Fact]
        public void SomethingThatDoesNotStackTakesASlotEach()
        {
            var pack = new Pack();

            pack.Take(Sword, 3);

            Assert.Equal(3, pack.Used);
        }

        [Fact]
        public void AQuestItemCostsNoSlotAtAll()
        {
            var pack = new Pack();

            var key = new Item("rusted_key", ItemKind.Quest);

            pack.Take(key);

            Assert.Equal(0, pack.Used);
            Assert.True(pack.Has("rusted_key"));
        }

        [Fact]
        public void AFullPackTakesWhatFitsAndSaysWhatDidNot()
        {
            var pack = new Pack(2);

            Assert.Equal(2, pack.Take(Sword, 5));
            Assert.True(pack.IsFull);
            Assert.Equal(2, pack.CountOf("longsword"));
        }

        [Fact]
        public void ItKnowsHowManySlotsAPickupWouldNeedBeforeTakingIt()
        {
            var pack = new Pack(3);

            pack.Take(Potion, 10);

            // there is room in the open stack, so no new slot is needed
            Assert.Equal(0, pack.SlotsNeededFor(Potion, 50));
            Assert.True(pack.Fits(Potion, 50));

            Assert.Equal(2, pack.SlotsNeededFor(Sword, 2));
        }

        [Fact]
        public void ADroppedStackFreesItsSlot()
        {
            var pack = new Pack();

            pack.Take(Potion, 10);
            Assert.Equal(1, pack.Used);

            pack.Drop("potion_of_healing", 10);
            Assert.Equal(0, pack.Used);
        }

        [Fact]
        public void AThousandReadsAsOneKAndTheRealNumberIsStillThere()
        {
            Assert.Equal("999", Stack.Short(999));
            Assert.Equal("1k", Stack.Short(1000));
            Assert.Equal("1k", Stack.Short(1999));
            Assert.Equal("99k", Stack.Short(99_000));
        }

        [Fact]
        public void GoldIsSpentAndEarnedAndNeverGoesNegative()
        {
            var pack = new Pack();

            pack.Earn(100);

            Assert.True(pack.Spend(40));
            Assert.Equal(60, pack.Gold);

            Assert.False(pack.Spend(100));
            Assert.Equal(60, pack.Gold);
        }
    }

    public class MerchantTests
    {
        static readonly Library Srd = Library.Srd();

        static Merchant Shop(params string[] stock) =>
            new Merchant("greyhollow_smith", Srd.Items, stock);

        static Actor Buyer(int level = 1)
        {
            var actor = new Actor("buyer", level, new AbilityScores(), Allegiance.Hero);
            actor.SetHealth(new Health(20));
            return actor;
        }

        [Fact]
        public void BuyingTakesTheGoldAndGivesTheItem()
        {
            Merchant shop = Shop("longsword");
            var pack = new Pack();

            pack.Earn(100);

            Deal deal = shop.Buy(pack, Srd.Items.Find("longsword"), Buyer(), "fighter");

            Assert.True(deal.Done);
            Assert.Equal(85, pack.Gold);
            Assert.True(pack.Has("longsword"));
        }

        [Fact]
        public void BuyingWithoutTheGoldIsRefusedAndCostsNothing()
        {
            Merchant shop = Shop("plate_armor");
            var pack = new Pack();

            pack.Earn(10);

            Deal deal = shop.Buy(pack, Srd.Items.Find("plate_armor"), Buyer(), "fighter");

            Assert.False(deal.Done);
            Assert.Equal(Rebuff.NoGold, deal.Rebuff);
            Assert.Equal(10, pack.Gold);
            Assert.Equal(0, pack.Used);
        }

        [Fact]
        public void AFullPackStopsTheSaleAndTheGoldStaysPut()
        {
            // the buy prevention inventory_decisions.md asks for a test on, by name
            Merchant shop = Shop("longsword");

            var pack = new Pack(1);
            pack.Take(Srd.Items.Find("shield"));
            pack.Earn(1000);

            Deal deal = shop.Buy(pack, Srd.Items.Find("longsword"), Buyer(), "fighter");

            Assert.False(deal.Done);
            Assert.Equal(Rebuff.PackFull, deal.Rebuff);
            Assert.Equal(1000, pack.Gold);
            Assert.False(pack.Has("longsword"));
        }

        [Fact]
        public void TheMerchantHasALineForAFullPackAndItIsInTheLocale()
        {
            // the other half of what inventory_decisions.md asks for: the line exists and is said
            string key = Merchant.LineFor(Rebuff.PackFull);

            Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));

            ILocalizer english = Locale.Localizer(
                System.IO.File.Exists("game.csv") ? System.IO.File.ReadAllText("game.csv") : "");

            Assert.NotEqual(key, english.Get(key));
        }

        [Fact]
        public void EveryRefusalHasItsOwnLine()
        {
            string[] keys = Merchant.Keys().ToArray();

            Assert.Equal(keys.Length, keys.Distinct().Count());

            foreach (string key in keys)
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));
        }

        [Fact]
        public void AShopNeverShowsWhatTheHeroCouldNotUse()
        {
            // inventory_decisions.md: unusable items are not surfaced in shops or as loot
            Merchant shop = Shop("holy_symbol", "longsword", "ring_of_protection");

            string[] shown = shop.Showing("fighter", 1).Select(i => i.Id).ToArray();

            Assert.Contains("longsword", shown);
            Assert.DoesNotContain("holy_symbol", shown);    // cleric and paladin only
            Assert.DoesNotContain("ring_of_protection", shown); // level 5 and up

            Assert.Contains("ring_of_protection", shop.Showing("fighter", 5).Select(i => i.Id));
            Assert.Contains("holy_symbol", shop.Showing("cleric", 1).Select(i => i.Id));
        }

        [Fact]
        public void BuyingSomethingYouCouldNotUseIsRefusedEvenIfYouAskDirectly()
        {
            Merchant shop = Shop("holy_symbol");

            var pack = new Pack();
            pack.Earn(100);

            Deal deal = shop.Buy(pack, Srd.Items.Find("holy_symbol"), Buyer(), "fighter");

            Assert.False(deal.Done);
            Assert.Equal(Rebuff.NotForYou, deal.Rebuff);
        }

        [Fact]
        public void SellingPaysTheGamesCut()
        {
            Merchant shop = Shop();

            var pack = new Pack();
            pack.Take(Srd.Items.Find("longsword"));

            Deal deal = shop.Sell(pack, Srd.Items.Find("longsword"));

            Assert.True(deal.Done);
            Assert.Equal(15 * Item.DefaultSellPercent / 100, pack.Gold);
            Assert.False(pack.Has("longsword"));
        }

        [Fact]
        public void AnItemMayRaiseItsOwnSellPriceToTheFullAmount()
        {
            Merchant shop = Shop();

            Assert.Equal(50, shop.PaysFor(Srd.Items.Find("gemstone")));
        }

        [Fact]
        public void SellingSomethingYouAreWearingWarnsEveryTime()
        {
            Merchant shop = Shop();

            Actor hero = Buyer();
            var pack = new Pack();
            var worn = new Equipment();

            Item sword = Srd.Items.Find("longsword");

            worn.Wear(sword, hero, "fighter");

            Deal warned = shop.Sell(pack, sword, 1, worn, hero);

            Assert.False(warned.Done);
            Assert.Equal(Rebuff.Equipped, warned.Rebuff);

            Deal done = shop.Sell(pack, sword, 1, worn, hero, confirmed: true);

            Assert.True(done.Done);
            Assert.Null(worn.In(Slot.MainHand));

            // and it warns again next time, because the warning is not dismissable
            worn.Wear(sword, hero, "fighter");

            Assert.Equal(Rebuff.Equipped, shop.Sell(pack, sword, 1, worn, hero).Rebuff);
        }

        [Fact]
        public void SellingWhatYouHaveNotGotIsRefused()
        {
            Merchant shop = Shop();

            Deal deal = shop.Sell(new Pack(), Srd.Items.Find("longsword"));

            Assert.False(deal.Done);
            Assert.Equal(Rebuff.NothingToSell, deal.Rebuff);
        }

        [Fact]
        public void BuyAndEquipPutsItOnAndTheOldOneBackInThePack()
        {
            Merchant shop = Shop("plate_armor");

            Actor hero = Buyer();
            var pack = new Pack();
            var worn = new Equipment();

            worn.Wear(Srd.Items.Find("leather_armor"), hero, "fighter");
            pack.Earn(2000);

            Deal deal = shop.Buy(pack, Srd.Items.Find("plate_armor"), hero, "fighter", 1, worn);

            Assert.True(deal.Done);
            Assert.Equal("plate_armor", worn.In(Slot.Body).Id);
            Assert.True(pack.Has("leather_armor"));
        }
    }

    public class OverflowTests
    {
        static readonly ItemShelf Shelf = Library.Srd().Items;

        [Fact]
        public void AnOverflowSaysHowManySlotsShortItIs()
        {
            var pack = new Pack(1);
            pack.Take(Shelf.Find("shield"));

            var overflow = new Overflow(pack, Shelf.Find("longsword"));

            Assert.False(overflow.Resolved);
            Assert.Equal(1, overflow.SlotsShort);
        }

        [Fact]
        public void DiscardingSomethingMakesTheRoomAndTheNewThingGoesIn()
        {
            var pack = new Pack(1);
            pack.Take(Shelf.Find("shield"));

            var overflow = new Overflow(pack, Shelf.Find("longsword"));

            Assert.True(overflow.Discard("shield"));
            Assert.True(overflow.Resolved);
            Assert.True(overflow.Finish());

            Assert.True(pack.Has("longsword"));
            Assert.False(pack.Has("shield"));
        }

        [Fact]
        public void RefusingTheNewThingLeavesThePackExactlyAsItWas()
        {
            var pack = new Pack(1);
            pack.Take(Shelf.Find("shield"));

            var overflow = new Overflow(pack, Shelf.Find("longsword"));

            overflow.Refuse();

            Assert.True(overflow.Finish());
            Assert.True(pack.Has("shield"));
            Assert.False(pack.Has("longsword"));
        }

        [Fact]
        public void AnUnresolvedOverflowWillNotFinish()
        {
            var pack = new Pack(1);
            pack.Take(Shelf.Find("shield"));

            var overflow = new Overflow(pack, Shelf.Find("longsword"));

            Assert.False(overflow.Finish());
            Assert.False(pack.Has("longsword"));
        }
    }
}
