using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Dialogue;
using Content.Inventory;
using Content.Items;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Content.Tests
{
    // THE CRUCIBLE'S ENGINE PIECES (cc_task_i Part 3), on a fixture pack (content.tests/Fixtures/crucible_fixture):
    // defeat continues, Yield, the armory, XP levelling and more than five characters. No Crucible pack - that is
    // Kathleen's
    public class CrucibleTests : IDisposable
    {
        static readonly Library Srd = Library.Srd();

        internal static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crucible_fixture");

        readonly string _temp = Path.Combine(Path.GetTempPath(), "lanorim_crucible_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, true);
        }

        static Package Pack() => SampleCampaignTests.Sound(Folder);

        static CampaignRun Run(out Hero hero, int seed = 3)
        {
            hero = SampleCampaignTests.Make(Srd, "fighter");
            return new CampaignRun(Srd, Pack(), hero, new StandardResolver(new SeededRng(seed)), new SeededRng(seed));
        }

        // reads on to the next thing the table has to do
        static Scene On(CampaignRun run)
        {
            while (run.Now == Scene.Line) run.Next();
            return run.Now;
        }

        // the story started and read up to the armory
        static Scene Begin(CampaignRun run)
        {
            run.Start();
            return On(run);
        }

        static string Said(CampaignRun run) => run.Talk.Heard.Last().Key;

        [Fact]
        public void TheFixtureSaysHowItPlays()
        {
            CampaignRules rules = Pack().Manifest.Rules;

            Assert.Equal(Defeat.Continues, rules.Defeat);
            Assert.Equal(Leveling.Xp, rules.Leveling);
            Assert.Equal(8, rules.Characters);
            Assert.False(rules.StartingShop);

            // and the game's defaults, for a campaign that says none of it
            CampaignRules sample = Package.Read(SampleCampaignTests.Folder).Manifest.Rules;
            Assert.Equal(Defeat.Death, sample.Defeat);
            Assert.Equal(Leveling.Milestone, sample.Leveling);
            Assert.Equal(SaveLibrary.CharactersPerCampaign, sample.Characters);
            Assert.True(sample.StartingShop);
        }

        [Theory]
        [InlineData(@"""defeat"": ""maybe""", "defeat")]
        [InlineData(@"""leveling"": ""levels""", "leveling")]
        [InlineData(@"""characters"": 0", "characters")]
        [InlineData(@"""characters"": 99", "characters")]
        [InlineData(@"""starting_shop"": ""no""", "starting_shop")]
        [InlineData(@"""consequences"": ""merge""", "consequences")]
        public void AWrongRuleIsRefusedAndSaysWhich(string rule, string field)
        {
            string json = $@"{{ ""id"": ""x"", ""kind"": ""campaign"", ""format"": 1, ""engine"": ""0.1"", {rule} }}";

            Read<Manifest> read = ManifestReader.Parse(json, ManifestReader.PackFileName);

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.Where == field);
        }

        [Fact]
        public void ANewHeroSkipsTheStartingShopWhenTheCampaignSaysSo()
        {
            CampaignRun run = Run(out Hero hero);

            Assert.True(hero.ShopsFirst);

            // the armory, not the starting shop
            Assert.Equal(Scene.Shop, Begin(run));
            Assert.Equal("racks", run.Shop.Id);
            Assert.False(run.InStartingShop);
            Assert.False(hero.ShopsFirst);
        }

        // --- defeat continues ----------------------------------------------------------------------------

        [Fact]
        public void LoseABoutAndTheStoryContinuesWithTheHeroBroughtRound()
        {
            CampaignRun run = Run(out Hero hero);
            Begin(run);
            run.LeaveShop();

            Assert.Equal(Scene.Fight, On(run));
            Assert.Equal(Defeat.Continues, run.DefeatHere);

            // the single death save failed: a knockout, not death
            hero.Actor.Health.Take(500);
            hero.Actor.Perish();

            Scene next = run.EndFight(Outcome.HeroesLost);

            Assert.Equal(Scene.Line, next);
            Assert.False(hero.Actor.IsDead);
            Assert.Equal(1, hero.Actor.Health.Current);
            Assert.EndsWith("lost", Said(run));

            // a lost bout gives no XP: only the story's <<xp 250>> after it
            On(run);
            Assert.Equal(250, hero.Experience);
        }

        [Fact]
        public void AFightThatSaysDeathIsDeathEvenInAContinuingCampaign()
        {
            Package pack = Pack();
            EncounterEntryFor(pack, "to_the_death", out var deadly);
            EncounterEntryFor(pack, "first_bout", out var bout);

            Assert.Equal(Defeat.Death, pack.Manifest.Rules.For(deadly));
            Assert.Equal(Defeat.Continues, pack.Manifest.Rules.For(bout));

            // and a campaign that says nothing keeps the death screen
            Assert.Equal(Defeat.Death, CampaignRules.Default.For(bout));
        }

        static void EncounterEntryFor(Package pack, string id, out Core.Tables.EncounterEntry entry) =>
            entry = pack.Encounters.SelectMany(t => t.Entries).Single(e => e.Id == id);

        // --- yield --------------------------------------------------------------------------------------

        [Fact]
        public void ABoutWithNoWayOutCanBeYieldedAndTheStoryHearsIt()
        {
            CampaignRun run = Run(out Hero hero);
            Begin(run);
            run.LeaveShop();
            On(run);

            Battle battle = run.BattleFor(new StandardResolver(new SeededRng(5)));
            Assert.True(battle.Fight.Yieldable);

            var session = new CombatSession(battle, Srd.Items, Srd.Forms);
            session.Start();

            ActionOption yield = session.Options().Single(o => o.Kind == OptionKind.Yield);
            Assert.Contains(new CombatHud(session, null).Manoeuvres, o => o.Kind == OptionKind.Yield);

            session.Take(yield);

            Assert.Equal(Outcome.Yielded, battle.Fight.Judge());

            int hp = hero.Actor.Health.Current;
            run.EndFight(Outcome.Yielded);

            Assert.EndsWith("yielded", Said(run));
            Assert.Equal(hp, hero.Actor.Health.Current);
            On(run);
            Assert.Equal(250, hero.Experience);   // only the story's <<xp 250>>: a yielded bout gives none
        }

        [Fact]
        public void NoYieldWhereDefeatIsDeathOrThereIsAWayOut()
        {
            CampaignRun sample = new CampaignRun(Srd, Package.Read(SampleCampaignTests.Folder), SampleCampaignTests.Make(Srd, "fighter"),
                                                 new StandardResolver(new SeededRng(1)), new SeededRng(1));
            sample.Start();
            sample.LeaveShop();

            for (int step = 0; step < 50 && sample.Now != Scene.Fight; step++)
                if (sample.Now == Scene.Choice) sample.Choose(0);
                else if (sample.Now == Scene.Shop) sample.LeaveShop();
                else sample.Next();

            Battle battle = sample.BattleFor(new StandardResolver(new SeededRng(5)));
            var session = new CombatSession(battle, Srd.Items, Srd.Forms);
            session.Start();

            Assert.False(battle.Fight.Yieldable);
            Assert.DoesNotContain(session.Options(), o => o.Kind == OptionKind.Yield);
        }

        // --- the armory ---------------------------------------------------------------------------------

        [Fact]
        public void EquipFromTheArmoryForNothingAndNothingLandsInThePack()
        {
            CampaignRun run = Run(out Hero hero);
            Begin(run);

            Assert.True(run.Shop.Armory);

            var view = new PackView(hero, run.Items, run.Shop.Open(run.Items), run.Shop.Armory);
            int copper = hero.Pack.Copper;

            // what the hero owns: what's in the pack and what's worn. nothing else may end up in the pack
            var owned = hero.Pack.Stacks.Select(s => s.Item.Id).Concat(hero.Equipment.All.Select(i => i.Id)).ToHashSet();

            Assert.False(view.AtTheCounter);
            Assert.Empty(view.Shelf);
            Assert.Contains(view.Racks, i => i.Id == "chain_mail");

            Assert.True(view.Borrow("longsword"));
            Assert.True(view.Borrow("chain_mail"));

            Assert.Equal("longsword", hero.Equipment.In(Slot.MainHand)?.Id);
            Assert.Equal("chain_mail", hero.Equipment.In(Slot.Body)?.Id);
            Assert.True(view.IsLoaned(hero.Equipment.In(Slot.Body)));

            // changed as often as you like: the bow takes both hands, and the borrowed sword goes back to the racks
            Assert.True(view.Borrow("shortbow"));
            Assert.Equal("shortbow", hero.Equipment.In(Slot.TwoHand)?.Id);
            Assert.Null(hero.Equipment.In(Slot.MainHand));
            Assert.False(hero.OnLoan("longsword"));

            // taken off, a borrowed thing goes back too
            Assert.True(view.TakeOff(Slot.Body));
            Assert.False(hero.OnLoan("chain_mail"));

            // no gold moved, and the pack holds only what the hero owned
            Assert.Equal(copper, hero.Pack.Copper);
            Assert.All(hero.Pack.Stacks, s => Assert.Contains(s.Item.Id, owned));
            Assert.Equal(owned.Contains("longsword") ? hero.Pack.CountOf("longsword") : 0, hero.Pack.CountOf("longsword"));

            // nothing to buy or sell here
            Assert.False(view.Buy("longsword").Done);
            Assert.All(view.Rows, r => Assert.Equal(-1, r.SellsFor));

            Assert.Equal(Scene.Fight, On(LeaveAndGo(run)));
        }

        static CampaignRun LeaveAndGo(CampaignRun run)
        {
            run.LeaveShop();
            return run;
        }

        [Fact]
        public void AnArmoryRefusesAConsumable()
        {
            string copy = CopyFixture();
            File.WriteAllText(Path.Combine(copy, "merchants", "armory.json"),
                              @"{ ""merchants"": [ { ""id"": ""racks"", ""armory"": true, ""stock"": [ ""longsword"", ""potion_of_healing"" ] } ] }");

            Package pack = Package.Read(copy);

            Assert.False(pack.Sound);
            Assert.Contains(pack.Faults, p => p.What.Contains("potion_of_healing") && p.What.Contains("consumables"));
        }

        [Fact]
        public void ALoanStaysALoanAcrossASave()
        {
            CampaignRun run = Run(out Hero hero);
            Begin(run);
            new PackView(hero, run.Items, run.Shop.Open(run.Items), true).Borrow("chain_mail");

            Read<SaveGame> back = SaveReader.Parse(SaveWriter.Write(run.Capture()));
            Assert.True(back.Ok, string.Join("; ", back.Problems));

            Hero again = HeroSaves.Restore(back.Value.Hero, Srd, out _);
            int owned = again.Pack.CountOf("chain_mail");

            Assert.True(again.OnLoan("chain_mail"));
            Assert.True(again.TakeOff(Slot.Body));
            Assert.Equal(owned, again.Pack.CountOf("chain_mail"));
        }

        // a copy to change, in a folder named for the campaign (a pack's folder is its id)
        string CopyFixture()
        {
            string copy = Path.Combine(_temp, "crucible_fixture");

            foreach (string file in Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories))
            {
                string to = Path.Combine(copy, Path.GetRelativePath(Folder, file));
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(file, to);
            }

            return copy;
        }

        // --- XP -------------------------------------------------------------------------------------------

        [Fact]
        public void TheSrdsAdvancementTable()
        {
            Assert.Equal(1, Advancement.LevelFor(0));
            Assert.Equal(1, Advancement.LevelFor(299));
            Assert.Equal(2, Advancement.LevelFor(300));
            Assert.Equal(4, Advancement.LevelFor(2_700));
            Assert.Equal(20, Advancement.LevelFor(355_000));
            Assert.Equal(20, Advancement.LevelFor(9_999_999));
            Assert.Equal(6_500, Advancement.XpFor(5));
        }

        [Fact]
        public void AWonBoutsXpAndTheStorysCrossThreeHundredAndTheHeroIsLevelTwo()
        {
            CampaignRun run = Run(out Hero hero);
            Begin(run);
            run.LeaveShop();
            On(run);

            Assert.Equal(100, run.FightXp);   // two goblins, 50 each at challenge 1/4

            run.EndFight(Outcome.HeroesWon);
            On(run);

            Assert.Equal(350, hero.Experience);
            Assert.Equal(2, hero.Level);
            Assert.Equal(900, hero.NextLevelAt);

            // <<level 5>> in an XP campaign: ignored, and the author is told
            Assert.Contains(run.Talk.Complained, c => c.Contains("<<level 5>> is ignored"));

            Assert.Equal(350, SheetView.Of(hero, leveling: Leveling.Xp).Experience);
            Assert.Null(SheetView.Of(hero).Experience);

            // and the XP is in the save
            Read<SaveGame> back = SaveReader.Parse(SaveWriter.Write(run.Capture()));
            Assert.Equal(350, HeroSaves.Restore(back.Value.Hero, Srd, out _).Experience);
        }

        [Fact]
        public void XpInAMilestoneCampaignIsIgnoredWithAWarning()
        {
            Hero hero = SampleCampaignTests.Make(Srd, "fighter");
            var referee = new Referee(hero, new StandardResolver(new SeededRng(1)), new Core.Tables.GmScreen(new SeededRng(1)), Srd,
                                      Package.Read(SampleCampaignTests.Folder));

            Settled settled = referee.Settle(Request.Parse("xp 400", out _));

            Assert.Contains("ignored", settled.Problem);
            Assert.Equal(0, hero.Experience);
            Assert.Equal(1, hero.Level);

            Assert.Null(Request.Parse("xp 0", out string problem));
            Assert.Contains("<<xp 50>>", problem);
        }

        // --- more characters, and the campaign's barks ----------------------------------------------------

        [Fact]
        public void ACampaignCanHoldMoreThanFiveCharacters()
        {
            var saves = new SaveLibrary(_temp);
            Hero hero = SampleCampaignTests.Make(Srd, "fighter");

            for (int slot = 0; slot < 6; slot++)
            {
                var run = new CampaignRun(Srd, Pack(), hero, new StandardResolver(new SeededRng(1)), new SeededRng(1), saves, slot);
                run.Save();
            }

            Assert.Equal(6, saves.FreeSlot("crucible_fixture", 8));
            Assert.Equal(-1, saves.FreeSlot("crucible_fixture"));

            var book = new CampaignBook(new[] { Pack().Manifest }, saves);
            Assert.True(book.Pages.Single().CanStartNew);
            Assert.Equal(8, book.Pages.Single().Slots);
        }

        [Fact]
        public void TheCampaignsOwnBarksAreAddedToTheCompanions()
        {
            CampaignRun run = Run(out _);

            Assert.Equal(Srd.Barks.Of("bonded_wolf").Count(Bark.Victory) + 1, run.Barks.Count(Bark.Victory));
            Assert.Contains("dialogue.bonded_wolf.bark.victory.crucible_fixture.001", run.Pack.Keys());
        }

        [Fact]
        public void TheFixturesKeysAllHaveEnglish()
        {
            Package pack = Pack();
            DialogueBook book = DialogueBook.Read(Path.Combine(Folder, Package.DialogueFolder), pack.Id);

            Assert.True(book.Problems.Count == 0, string.Join("\n", book.Problems));

            string csv = File.ReadAllText(Path.Combine(Folder, Package.LocaleFolder, pack.Id + ".csv"));

            Assert.Empty(Locale.Audit(csv, pack.Keys().Concat(book.Keys())));
        }
    }
}
