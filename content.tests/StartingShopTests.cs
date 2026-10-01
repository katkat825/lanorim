using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Inventory;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Dice;
using Core.Resolution;
using Content.Items;

namespace Content.Tests
{
    // GEAR OR GOLD, AND A STARTING SHOP (cc_task_e-shop-species-and-ui-notes.md 1.4): a hero who takes the gold instead
    // of the gear spends it in the campaign's starting shop - the merchant every shop is, opened before the first scene
    public class StartingShopTests
    {
        static readonly Library Srd = Library.Srd();

        static Package Pack() =>
            Package.Read(Path.Combine(AppContext.BaseDirectory, "campaigns", "sample_millbrook"));

        static Hero Fighter(KitChoice cls, KitChoice background)
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);

            making.Pick(Srd.Class("fighter"));
            making.Pick(Srd.Kind("human"));
            making.Pick(Srd.Background("soldier"));
            making.SuggestTraits();
            making.PickClassKit(cls);
            making.PickBackgroundKit(background);

            foreach (Core.Characters.Skill skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList())
                making.Train(skill);

            making.Call("Tamsin");

            Assert.True(making.Ready, string.Join("; ", making.Problems));
            return making.Finish();
        }

        static CampaignRun Run(Hero hero) =>
            new CampaignRun(Srd, Pack(), hero, new StandardResolver(new ScriptedRng(10)), new SeededRng(1));

        [Fact]
        public void TheGoldInsteadIsTheSrdsAndComesWithNoGear()
        {
            // SRD 5.2.1 p.47: a Fighter's "(C) 155 GP"; p.83: a background's "(B) 50 GP"
            Hero hero = Fighter(KitChoice.Gold, KitChoice.Gold);

            Assert.Equal(155 + 50, hero.Pack.Gold);
            Assert.Empty(hero.Pack.Everything);
            Assert.Empty(hero.Equipment.Worn);
            Assert.True(hero.ShopsFirst);

            // the gear and its own little purse, as before
            Hero kitted = Fighter(KitChoice.Gear, KitChoice.Gear);
            Assert.Equal(4 + 14, kitted.Pack.Gold);
            Assert.False(kitted.ShopsFirst);
        }

        [Fact]
        public void TakeTheGoldBuyALongswordAndChainMailAndTheCampaignStartsWithThemOn()
        {
            Hero hero = Fighter(KitChoice.Gold, KitChoice.Gold);
            CampaignRun run = Run(hero);

            Assert.Equal(Scene.Shop, run.Start());
            Assert.True(run.InStartingShop);
            Assert.Equal(MerchantReader.StartingShopId, run.Shop.Id);

            var shop = new PackView(hero, run.Items, run.Shop.Open(run.Items));

            Assert.True(shop.Buy("longsword").Done);
            Assert.True(shop.Buy("chain_mail").Done);

            // SRD prices: 15 GP and 75 GP
            Assert.Equal(205 - 15 - 75, hero.Pack.Gold);

            Scene after = run.LeaveShop();

            Assert.NotEqual(Scene.Shop, after);
            Assert.False(run.InStartingShop);
            Assert.False(hero.ShopsFirst);
            Assert.Equal("chain_mail", hero.Equipment.In(Slot.Body)?.Id);
            Assert.Equal("longsword", hero.Equipment.In(Slot.MainHand)?.Id);
        }

        // the choice made here: the gear means no shop at all (not a shop with nothing to spend)
        [Fact]
        public void TakeTheGearAndThereIsNoShop()
        {
            CampaignRun run = Run(Fighter(KitChoice.Gear, KitChoice.Gear));

            Assert.NotEqual(Scene.Shop, run.Start());
            Assert.False(run.InStartingShop);
        }

        [Fact]
        public void TheShopCannotOverspendAndBuysBackAtTheMerchantsRate()
        {
            Hero hero = Fighter(KitChoice.Gold, KitChoice.Gear);
            CampaignRun run = Run(hero);
            run.Start();

            var shop = new PackView(hero, run.Items, run.Shop.Open(run.Items));
            int gold = hero.Pack.Gold;

            Deal plate = shop.Buy("plate_armor");

            Assert.False(plate.Done);
            Assert.Equal(Rebuff.NoGold, plate.Rebuff);
            Assert.Equal(gold, hero.Pack.Gold);

            // no ammunition: it is free (1.2)
            Assert.DoesNotContain(run.Shop.Stock, id => id.Contains("arrow") || id.Contains("bolt") || id.Contains("quiver"));

            Assert.True(shop.Buy("longsword").Done);
            Deal back = shop.Sell("longsword", confirmed: true);

            Assert.True(back.Done);
            Assert.True(back.Gold < 15);
        }

        // a save made in the shop goes back to the shop, with what was bought
        [Fact]
        public void ASaveMadeInTheShopReopensIt()
        {
            Hero hero = Fighter(KitChoice.Gold, KitChoice.Gold);
            CampaignRun run = Run(hero);
            run.Start();
            new PackView(hero, run.Items, run.Shop.Open(run.Items)).Buy("longsword");

            var save = new SaveGame { Campaign = run.Pack.Id, Hero = HeroSaves.Capture(hero) };
            Hero back = HeroSaves.Restore(SaveReader.Parse(SaveWriter.Write(save)).Value.Hero, Srd, out _);

            Assert.True(back.ShopsFirst);
            Assert.Equal(hero.Pack.Gold, back.Pack.Gold);
            Assert.Equal(1, back.Pack.Everything.Where(s => s.Item.Id == "longsword").Sum(s => s.Count));
            Assert.Equal(Scene.Shop, Run(back).Start());
        }

        // a campaign's own merchant with the id replaces the SRD's stock
        [Fact]
        public void ThePacksOwnStartingShopWins()
        {
            Assert.Equal(MerchantReader.StartingShopId, MerchantReader.StartingShop.Id);
            Assert.Contains("potion_of_healing", MerchantReader.StartingShop.Stock);
            Assert.All(MerchantReader.StartingShop.Stock, id => Assert.True(Srd.Items.Has(id), id));

            Assert.Same(MerchantReader.StartingShop, Pack().StartingShop);
        }
    }
}
