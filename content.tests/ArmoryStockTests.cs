using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Inventory;
using Content.Items;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Xunit;

namespace Content.Tests
{
    // THE QUARTERMASTER (cc_task_ca Part 4; Kathleen 2026-11-16: "every non-magical item that the hero can use at their
    // level"): an armory shows only what is non-magical and what Item.UsableBy allows, and "stock": "all_mundane" is
    // every non-magical wearable or wieldable item, so the Crucible needn't list them
    public class ArmoryStockTests
    {
        static readonly Library Srd = Library.Srd();

        static MerchantDef Quartermaster() => SampleCampaignTests.Sound(CrucibleTests.Folder).Merchant("quartermaster");

        static List<string> Racks(Hero hero, MerchantDef armory, ItemShelf shelf = null) =>
            new PackView(hero, shelf ?? Srd.Items, armory.Open(shelf ?? Srd.Items), true).Racks.Select(i => i.Id).ToList();

        [Fact]
        public void AllMundaneIsEveryWearableNonMagicalItemAndNothingUsedUp()
        {
            MerchantDef quartermaster = Quartermaster();
            Assert.True(quartermaster.StocksAllMundane);

            IReadOnlyList<string> stock = quartermaster.Open(Srd.Items).Stock;

            Assert.Contains("longsword", stock);
            Assert.Contains("plate_armor", stock);
            Assert.Contains("shield", stock);
            Assert.Contains("arcane_focus", stock);
            Assert.DoesNotContain("ring_of_protection", stock);          // a boon: magical
            Assert.DoesNotContain("cloak_of_the_quiet_step", stock);
            Assert.DoesNotContain("potion_of_healing", stock);           // used up
            Assert.All(stock, id => Assert.True(MerchantDef.Mundane(Srd.Items.Find(id)), id));
        }

        [Fact]
        public void AFighterAndAMageSeeDifferentRacks()
        {
            List<string> fighter = Racks(Creation.QuickHero.Make(Srd, "fighter", 1, "Tess"), Quartermaster());
            List<string> mage = Racks(Creation.QuickHero.Make(Srd, "mage", 1, "Tess"), Quartermaster());

            Assert.Contains("arcane_focus", mage);
            Assert.DoesNotContain("arcane_focus", fighter);
            Assert.Contains("holy_symbol", Racks(Creation.QuickHero.Make(Srd, "cleric", 1, "Tess"), Quartermaster()));
            Assert.NotEqual(fighter, mage);
        }

        [Fact]
        public void ALevelGatedItemIsOnTheRacksOnlyFromItsLevelAndNothingMagicalEver()
        {
            Assert.True(ItemReader.TryRead(@"{""items"":[{""id"":""veterans_blade"",""kind"":""weapon"",""cost"":30,""slot"":""main_hand"",
                ""minimum_level"":3,""weapon"":{""damage"":""1d8"",""damage_type"":""slashing"",""category"":""martial""}}]}",
                                           out IReadOnlyList<Item> own, out IReadOnlyList<string> problems), string.Join("; ", problems));
            ItemShelf shelf = Srd.Items.With(own);

            Assert.DoesNotContain("veterans_blade", Racks(Creation.QuickHero.Make(Srd, "fighter", 1, "Tess"), Quartermaster(), shelf));
            Assert.Contains("veterans_blade", Racks(Creation.QuickHero.Make(Srd, "fighter", 3, "Tess"), Quartermaster(), shelf));

            // a listed armory with a magic ring in it still never lends it, even at its level
            var listed = new MerchantDef("racks", new[] { "longsword", "ring_of_protection" }, armory: true);
            Assert.Equal(new[] { "longsword" }, Racks(Creation.QuickHero.Make(Srd, "fighter", 5, "Tess"), listed));
        }

        [Fact]
        public void OnlyAnArmoryCanStockEverything()
        {
            MerchantReader.TryRead(@"{""merchants"":[{""id"":""store"",""stock"":""all_mundane""},
                                                     {""id"":""odd"",""armory"":true,""stock"":""everything""}]}",
                                   out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.StartsWith("store:") && p.Contains("only an armory"));
            Assert.Contains(problems, p => p.StartsWith("odd:") && p.Contains("all_mundane"));
        }
    }
}
