using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Inventory;
using Content.Items;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Dice;
using Core.Resolution;

namespace Content.Tests
{
    // MONEY IS COPPER, AND SELLING PAYS THE SRD'S HALF (Kathleen, 2026-10-05: "why is all the starting gear selling for
    // 0gp? make it the srd selling price"; cc_task_f 1.5). the purse and every price were whole gold, and a sale paid
    // 40% rounded down, so a 1 GP sickle - and every SRD item priced in silver, rounded up to 1 GP - sold for 0
    public class MoneyTests
    {
        static readonly Library Srd = Library.Srd();

        static Merchant Shop() => new Merchant("anyone", Srd.Items);

        static int Pays(string item) => Shop().PaysFor(Srd.Items.Find(item));

        // SRD 5.2.1 Selling Equipment: "Equipment fetches half its cost when sold"
        [Fact]
        public void ADaggerSellsForOneGoldAndLeatherArmorForFive()
        {
            Assert.Equal(Coins.FromGold(1), Pays("dagger"));        // 2 GP
            Assert.Equal(Coins.FromGold(5), Pays("leather_armor")); // 10 GP
            Assert.Equal(50, Pays("sickle"));                       // 1 GP: 5 SP, not 0
        }

        // "trade goods and valuables - like gems and art objects - retain their full value"
        [Fact]
        public void AGemstoneSellsAtItsFullValue() =>
            Assert.Equal(Srd.Items.Find("gemstone").Cost, Pays("gemstone"));

        // the SRD's silver and copper prices are exact now, and half of them rounds down to the copper
        [Fact]
        public void SilverAndCopperPricesAreTheSrds()
        {
            Assert.Equal(50, Srd.Items.Find("javelin").Cost);  // 5 SP
            Assert.Equal(20, Srd.Items.Find("quarterstaff").Cost); // 2 SP
            Assert.Equal(5, Srd.Items.Find("dart").Cost);      // 5 CP
            Assert.Equal(1, Srd.Items.Find("torch").Cost);     // 1 CP

            Assert.Equal(25, Pays("javelin"));
            Assert.Equal(2, Pays("dart"));  // half of 5 CP, rounded down
            Assert.Equal(0, Pays("torch")); // half of 1 CP
        }

        // a campaign's merchant may still pay its own cut
        [Fact]
        public void AMerchantsOwnCutStillWins() =>
            Assert.Equal(Coins.FromGold(15) * 80 / 100,
                         new Merchant("generous", Srd.Items, sellPercentOverride: 80).PaysFor(Srd.Items.Find("longsword")));

        // an item from the starting kit is the same item as one bought, and sells for the same
        [Fact]
        public void AnItemFromTheStartingKitSellsLikeTheSameItemBought()
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);

            making.Pick(Srd.Class("fighter"));
            making.Pick(Srd.Kind("human"));
            making.Pick(Srd.Background("soldier"));
            making.SuggestTraits();
            foreach (Core.Characters.Skill skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList())
                making.Train(skill);
            making.Call("Tamsin");
            Hero hero = making.Finish();

            Package pack = Package.Read(Path.Combine(AppContext.BaseDirectory, "campaigns", "sample_millbrook"));
            Merchant shop = pack.StartingShop.Open(Srd.Items);
            var view = new PackView(hero, Srd.Items, shop);

            // the Soldier's spear and the Fighter's javelins, from the kit
            PackRow kitSpear = view.Rows.First(r => r.Item.Id == "spear");
            PackRow kitJavelin = view.Rows.First(r => r.Item.Id == "javelin");

            Assert.True(view.Buy("spear").Done);

            Assert.Equal(50, kitSpear.SellsFor); // 1 GP: 5 SP
            Assert.Equal(25, kitJavelin.SellsFor);
            Assert.All(view.Rows.Where(r => r.Item.Id == "spear"), r => Assert.Equal(kitSpear.SellsFor, r.SellsFor));

            int before = hero.Pack.Copper;
            Assert.Equal(50, view.Sell("spear").Copper);
            Assert.Equal(before + 50, hero.Pack.Copper);
        }

        // what the screen writes: the largest coin first, a coin only when there is some, and nothing as 0 gp
        [Fact]
        public void AnAmountIsSaidAsGoldSilverAndCopper()
        {
            Assert.Equal(new[] { (PackView.GoldKey, 2) }, PackView.Money(200));
            Assert.Equal(new[] { (PackView.SilverKey, 5) }, PackView.Money(50));
            Assert.Equal(new[] { (PackView.GoldKey, 1), (PackView.SilverKey, 5) }, PackView.Money(150));
            Assert.Equal(new[] { (PackView.GoldKey, 12), (PackView.CopperKey, 3) }, PackView.Money(1203));
            Assert.Equal(new[] { (PackView.GoldKey, 0) }, PackView.Money(0));
        }

        // an item's cost is written in gold, with fractions down to the copper, and no finer
        [Fact]
        public void APriceIsReadInGoldToTheCopper()
        {
            Assert.True(ItemReader.TryRead("{ \"items\": [ { \"id\": \"nail\", \"kind\": \"tool\", \"cost\": 0.05 } ] }",
                                           out var items, out var problems), string.Join("; ", problems));
            Assert.Equal(5, items.Single().Cost);

            Assert.False(ItemReader.TryRead("{ \"items\": [ { \"id\": \"nail\", \"kind\": \"tool\", \"cost\": 0.005 } ] }",
                                            out _, out problems));
            Assert.Contains(problems, p => p.Contains("to the copper"));
        }

        // a save from before kept whole gold as "gold"; it comes back as copper
        [Fact]
        public void AnOldSavesGoldConverts()
        {
            var save = new SaveGame { Campaign = "ash_yard", Hero = new SavedHero { Name = "Pell", Copper = 1650 } };
            string written = SaveWriter.Write(save);

            Assert.Contains("\"copper\"", written);
            Assert.DoesNotContain("\"gold\"", written);
            Assert.Equal(1650, SaveReader.Parse(written).Value.Hero.Copper);

            string old = written.Replace("\"copper\"", "\"gold\"").Replace("1650", "16");
            Assert.Equal(1600, SaveReader.Parse(old).Value.Hero.Copper);
        }
    }
}
