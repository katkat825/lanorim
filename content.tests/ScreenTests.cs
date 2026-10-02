using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Inventory;
using Content.Maps;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Space;

namespace Content.Tests
{
    // Tier 2.8 of the 2026-09-24 run: the screens' view models - what each screen shows and what its
    // buttons do, tested without Godot. The scenes that draw them are Kathleen's to look at.
    public partial class ScreenTests : IDisposable
    {
        static readonly Library Srd = Library.Srd();

        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_screens_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        static Manifest Manifest(string id, params string[] tags)
        {
            string json = $@"{{ ""id"": ""{id}"", ""kind"": ""campaign"", ""format"": {ContentFormat.Current},
                ""engine"": ""{Core.EngineVersion.Current}"", ""author"": ""K"",
                ""tags"": [{string.Join(",", tags.Select(t => $"\"{t}\""))}],
                ""chapters"": [ {{ ""id"": ""one"", ""maps"": [ ""yard"" ] }} ] }}";

            Read<Manifest> read = ManifestReader.Parse(json, "pack.json");

            Assert.True(read.Ok, string.Join("; ", read.Problems));

            return read.Value;
        }

        static Hero Made(string cls, int level = 1)
        {
            Content.Classes.CharacterClass made = Srd.Class(cls);

            var hero = new Hero("Tess", made, Srd.Kind("human"), Srd.Background("soldier"),
                                Creation.Creation.Standard(made), level);

            hero.Build(null, made.SkillChoices.Take(made.SkillPicks).ToList(), null, Srd.Items);

            return hero;
        }

        [Fact]
        public void TheBookHasCampaignsThenTestsAndTutorialsBehindThePicker()
        {
            var saves = new SaveLibrary(_root);
            saves.Save(new SaveGame { Campaign = "the_goat", Slot = 1, Hero = HeroSaves.Capture(Made("fighter")) },
                       SaveKind.ChapterStart);

            Manifest[] all =
            {
                Manifest("sample", "test"), Manifest("the_goat"), Manifest("first_steps", "tutorial_beginner"),
                Manifest("long_road"),
            };

            var book = new CampaignBook(all, saves);

            Assert.Equal(new[] { "the_goat", "long_road", "sample" }, book.Pages.Select(p => p.Id));
            Assert.Equal(CampaignBook.TestLabel, book.Pages.Single(p => p.Id == "sample").LabelKey);
            Assert.Null(book.Pages.Single(p => p.Id == "the_goat").LabelKey);
            Assert.Equal("first_steps", book.Tutorials.Single().Id);

            CharacterSlot tess = book.Pages.Single(p => p.Id == "the_goat").Characters.Single();
            Assert.Equal(1, tess.Slot);
            Assert.Equal("Tess", tess.Name);
            Assert.True(book.Pages.Single(p => p.Id == "the_goat").CanStartNew);
            Assert.True(book.CanContinue);

            var picker = new TutorialPicker(all);

            Assert.Equal(3, picker.Choices.Count);
            Assert.True(picker.Choices[0].Available);
            Assert.Equal("first_steps", picker.Choices[0].CampaignId);
            Assert.Equal(TutorialPicker.NotYetKey, picker.Choices[1].WhyNotKey);
            Assert.Equal("sample", picker.Tests.Single().Id);
        }

        // cc_task_ui-issues-9-30.md 6.2: a character is deleted from the book, every save with it, after a
        // question that names it and says how many saves go
        [Fact]
        public void DeletingACharacterTakesEverySaveAndFreesTheSlot()
        {
            var saves = new SaveLibrary(_root);
            SaveGame Ren() => new SaveGame { Campaign = "the_goat", Slot = 0, Hero = HeroSaves.Capture(Made("fighter")) };

            saves.Save(Ren(), SaveKind.ChapterStart);
            saves.Save(Ren(), SaveKind.FightStart);
            saves.Save(Ren(), SaveKind.Manual, "before the bridge");
            saves.Save(new SaveGame { Campaign = "the_goat", Slot = 3, Hero = HeroSaves.Capture(Made("rogue")) },
                       SaveKind.ChapterStart);

            Manifest[] all = { Manifest("the_goat") };
            BookPage page = new CampaignBook(all, saves).Pages.Single();
            CharacterSlot ren = page.Characters.Single(c => c.Slot == 0);

            Assert.Equal(3, ren.SaveCount);
            (string key, object[] args) = CampaignBook.DeleteQuestion(ren);
            Assert.Equal(CampaignBook.DeleteManyKey, key);
            Assert.Equal(new object[] { ren.Name, 3 }, args);

            Assert.Equal(3, CampaignBook.Delete(saves, "the_goat", ren));

            BookPage after = new CampaignBook(all, saves).Pages.Single();
            Assert.Equal(new[] { 3 }, after.Characters.Select(c => c.Slot));
            Assert.Empty(saves.Of("the_goat", 0));
            Assert.Equal(0, saves.FreeSlot("the_goat"));

            // one save left says so in the singular
            Assert.Equal(CampaignBook.DeleteOneKey, CampaignBook.DeleteQuestion(after.Characters.Single()).Key);
        }

        [Fact]
        public void LevelUpShowsWhatCameAndWaitsForTheImprovement()
        {
            Hero hero = Made("fighter", 3);
            int was = hero.Actor.Health.Maximum;

            hero.LevelTo(4);

            var view = new LevelUpView(hero, 3, was);

            Assert.Equal(4, view.Level);
            Assert.True(view.HitPointsGained > 0);
            Assert.Equal(1, view.Pending);
            Assert.False(view.Done);
            Assert.Equal(LevelUpView.ImprovementsWaitingKey, view.DoneWhyNotKey);

            AbilityImprovement pick = AbilityImprovement.Two(Ability.Constitution);
            int con = hero.Actor.Scores.Base(Ability.Constitution);

            Assert.Equal(con + 2, view.ScoreWith(pick, Ability.Constitution));
            Assert.True(view.Improve(pick, out _));
            Assert.True(view.Done);
            Assert.Equal(con + 2, hero.Actor.Scores.Base(Ability.Constitution));
        }

        [Fact]
        public void ThePackBuysSellsAndDiscardsAndTheWarningStaysDismissedForTheCharacter()
        {
            Hero hero = Made("fighter");
            hero.Pack.Earn(Content.Inventory.Coins.FromGold(200));

            var merchant = new Merchant("store", Srd.Items, new[] { "potion_of_healing", "torch" });
            var view = new PackView(hero, Srd.Items, merchant);

            Assert.True(view.AtTheCounter);
            Assert.Contains(view.Shelf, r => r.Item.Id == "potion_of_healing" && r.Affordable);

            int gold = view.Copper;
            Assert.True(view.Buy("potion_of_healing").Done);
            Assert.True(view.Copper < gold);
            Assert.Contains(view.Rows, r => r.Item.Id == "potion_of_healing" && r.SellsFor > 0);

            // something the hero is wearing asks first, every time
            string worn = hero.Equipment.All.First().Id;
            Assert.True(view.NeedsSellConfirmation(worn));
            Assert.False(view.Sell(worn).Done);

            Assert.True(view.NeedsDiscardWarning);
            view.DismissDiscardWarning();
            Assert.True(view.Discard("potion_of_healing"));
            Assert.Equal(0, hero.Pack.CountOf("potion_of_healing"));

            SavedHero saved = HeroSaves.Capture(hero);
            Read<SaveGame> back = SaveReader.Parse(SaveWriter.Write(new SaveGame { Campaign = "x", Hero = saved }));
            Hero again = HeroSaves.Restore(back.Value.Hero, Srd, out _);

            Assert.False(new PackView(again, Srd.Items).NeedsDiscardWarning);
        }

        [Fact]
        public void TheDialoguePopupShowsTheSpeakerTheLineAndTheChoices()
        {
            Package pack = Package.Read(Path.Combine(AppContext.BaseDirectory, "campaigns", "sample_millbrook"));
            var rng = new SeededRng(2);
            var run = new CampaignRun(Srd, pack, Made("fighter"), new StandardResolver(rng), rng);

            run.Start();
            run.LeaveShop(); // every new hero opens on the starting shop (cc_task_f 1.3)

            var view = new DialogueView(run, "wolf");

            Assert.True(view.Showing);
            Assert.True(view.CanContinue);
            Assert.Null(view.SpeakerNameKey);
            Assert.EndsWith("arrive", view.LineKey);

            view.Continue();
            view.Continue();

            Assert.Equal("actor.oda.name", view.SpeakerNameKey);

            view.Continue();

            // 'companion' is said by the companion travelling
            Assert.Equal("wolf", view.Speaker);

            view.Continue();

            Assert.False(view.CanContinue);
            Assert.Equal(3, view.Choices.Count);
            Assert.All(view.Choices, c => Assert.True(c.Offered));
        }

        [Fact]
        public void TheCombatHudReadsTheSession()
        {
            Hero hero = Made("fighter", 3);
            var hall = new MapDraft(8, 4);
            hall.Paint(new Cell(0, 0), new Cell(7, 3), Tile.Floor);
            hall.Enclose();
            hall.PlaceStart(new Cell(0, 1));
            MapLayout map = hall.Layout();

            var resolver = new StandardResolver(new SeededRng(5));
            var log = new FightLog();
            Battle battle = Battle.Set(Srd, map, hero,
                                       new[] { new Battle.Foe(Srd.Bestiary.Find("goblin"), new Cell(6, 2)) },
                                       resolver, null, log);

            var session = new CombatSession(battle, Srd.Items, Srd.Forms);
            session.Start();

            var hud = new CombatHud(session, log);

            Assert.Equal(2, hud.Order.Count);
            Assert.Single(hud.Order, c => c.Hero && c.Name == "Tess");
            Assert.Single(hud.Order, c => c.NameKey == "monster.goblin.name");

            Assert.True(hud.HerosTurn);
            Assert.Single(hud.Order, c => c.Current && c.Hero);
            Assert.Equal(2, hud.Actions);
            Assert.Equal(1, hud.BonusActions);
            Assert.Equal(1, hud.Reactions);
            Assert.True(hud.SquaresLeft > 0);

            ActionOption first = hud.Hotkey(1);
            Assert.NotNull(first);
            Assert.Null(hud.Hotkey(0));
            Assert.All(hud.Bar.Where(o => !o.Enabled), o => Assert.False(string.IsNullOrEmpty(o.WhyNotKey)));

            Assert.NotEmpty(hud.Log.Lines);
        }

        [Fact]
        public void SettingsRoundTripAndABadFileIsTheDefaults()
        {
            var settings = new GameSettings { EnemySpeed = CombatSpeed.Fast, SkipPhysicalDice = true, BringTrayToMe = false };
            settings.Reactions.Set("shield", ReactionPolicy.Ask);

            GameSettings back = GameSettings.Read(settings.Write(), out IReadOnlyList<string> problems);

            Assert.Empty(problems);
            Assert.Equal(CombatSpeed.Fast, back.EnemySpeed);
            Assert.True(back.SkipPhysicalDice);
            Assert.False(back.BringTrayToMe);
            Assert.True(new GameSettings().BringTrayToMe);
            Assert.Equal(ReactionPolicy.Ask, back.Reactions.For("shield"));
            Assert.True(back.SecondsPerEnemyStep < new GameSettings().SecondsPerEnemyStep);

            GameSettings bad = GameSettings.Read(@"{ ""enemy_speed"": ""ludicrous"" }", out problems);

            Assert.Equal(CombatSpeed.Normal, bad.EnemySpeed);
            Assert.Contains(problems, p => p.Contains("ludicrous"));
        }

        [Fact]
        public void DeathOffersTheNewestSave()
        {
            var saves = new SaveLibrary(_root);

            Assert.False(new DeathView(saves, "sample", 0).CanReload);

            saves.Save(new SaveGame { Campaign = "sample", Node = "cellar" }, SaveKind.FightStart);

            var death = new DeathView(saves, "sample", 0);

            Assert.True(death.CanReload);
            Assert.Equal("cellar", death.Reload.Game.Node);
        }

        [Fact]
        public void CreationPicksCanBeTakenBack()
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);

            making.Pick(Srd.Class("rogue"));
            making.Pick(Srd.Kind("human"));
            making.Pick(Srd.Background("soldier"));

            Skill first = making.SkillChoices.First();
            Assert.True(making.Train(first));
            Assert.True(making.Master(first));

            int left = making.SkillPicksLeft;
            Assert.True(making.Untrain(first));
            Assert.Equal(left + 1, making.SkillPicksLeft);
            Assert.DoesNotContain(first, making.Expertise);

            var mage = new Creation.Creation(Srd, Srd.Backgrounds);
            mage.Pick(Srd.Class("mage"));
            Core.Magic.Spell bolt = mage.SpellChoices.First(s => s.IsCantrip);
            Assert.True(mage.Learn(bolt));
            Assert.True(mage.Unlearn(bolt));
            Assert.Empty(mage.Spells);
        }

        [Fact]
        public void AManifestCanNameItsGmScreenAndOnlyARealOne()
        {
            Assert.Equal("blank", Manifest("plain").GmScreen);

            string json = $@"{{ ""id"": ""cold"", ""kind"": ""campaign"", ""format"": {ContentFormat.Current},
                ""engine"": ""{Core.EngineVersion.Current}"", ""author"": ""K"", ""gm_screen"": ""snowy-mountains"",
                ""chapters"": [ {{ ""id"": ""one"", ""maps"": [ ""yard"" ] }} ] }}";

            Assert.Equal("snowy-mountains", ManifestReader.Parse(json, "pack.json").Value.GmScreen);

            Read<Manifest> bad = ManifestReader.Parse(json.Replace("snowy-mountains", "volcano"), "pack.json");

            Assert.False(bad.Ok);
            Assert.Contains(bad.Problems, p => p.What.Contains("volcano"));
        }
        // THE BAR IN THREE PARTS (Kathleen, 2026-09-28): every option is in exactly one - a button,
        // the spells' menu or the manoeuvres' - and the hotkeys don't move
        [Fact]
        public void TheBarIsButtonsAndTwoMenusAndLosesNothing()
        {
            Content.Classes.CharacterClass mages = Srd.Class("mage");
            var mage = new Hero("Tess", mages, Srd.Kind("human"), Srd.Background("sage"),
                                Creation.Creation.Standard(mages), 3);
            mage.Build(null, mages.SkillChoices.Take(mages.SkillPicks).ToList(), null, Srd.Items,
                       Srd.Spells.For("mage").Where(s => s.Level <= 2).ToList());
            var hall = new MapDraft(8, 4);
            hall.Paint(new Cell(0, 0), new Cell(7, 3), Tile.Floor);
            hall.Enclose();
            hall.PlaceStart(new Cell(0, 1));

            Battle battle = Battle.Set(Srd, hall.Layout(), mage,
                                       new[] { new Battle.Foe(Srd.Bestiary.Find("goblin"), new Cell(6, 2)) },
                                       new StandardResolver(new SeededRng(5)), null, new FightLog());
            var session = new CombatSession(battle, Srd.Items, Srd.Forms);
            session.Start();

            var hud = new CombatHud(session);

            Assert.NotEmpty(hud.Spells);
            Assert.NotEmpty(hud.Manoeuvres);
            Assert.All(hud.Spells, o => Assert.True(o.Kind is OptionKind.Spell or OptionKind.Again));
            Assert.DoesNotContain(hud.Buttons, o => o.Kind is OptionKind.Spell or OptionKind.Dash or OptionKind.EndTurn);
            Assert.Contains(hud.Buttons, o => o.Kind == OptionKind.Attack);

            Assert.Equal(hud.Bar.Count(o => o.Kind != OptionKind.EndTurn),
                         hud.Buttons.Count + hud.Spells.Count + hud.Manoeuvres.Count);
            Assert.Same(hud.Hotkey(1), hud.Bar.First(o => o.Hotkey == 1));
        }

        // THE NATURAL 1 / 20 POOL, WIRED (Kathleen, 2026-09-28): a story's check with a natural 1
        // draws a bane and a natural 20 a boon, whether it passed or not, and it is carried out
        [Theory]
        [InlineData(1, Polarity.Bane)]
        [InlineData(20, Polarity.Boon)]
        public void AStoryChecksNaturalOneOrTwentyBuysAConsequence(int natural, Polarity side)
        {
            Hero hero = Made("fighter", 3);
            var referee = new Content.Dialogue.Referee(hero, new StandardResolver(new ScriptedRng(natural)),
                                                       new Core.Tables.GmScreen(new SeededRng(3)), Srd);

            Content.Dialogue.Settled settled =
                referee.Settle(Content.Dialogue.Request.Parse("check athletics 15", out _));

            Assert.NotNull(settled.Visit);
            Assert.Equal(side, settled.Visit.Consequence.Polarity);
            Assert.Equal(natural, settled.Attempt.Natural);
        }

        [Fact]
        public void AnOrdinaryCheckOrASaveBuysNothing()
        {
            Hero hero = Made("fighter", 3);
            var referee = new Content.Dialogue.Referee(hero, new StandardResolver(new ScriptedRng(11)),
                                                       new Core.Tables.GmScreen(new SeededRng(3)), Srd);

            Assert.Null(referee.Settle(Content.Dialogue.Request.Parse("check athletics 15", out _)).Visit);

            var unlucky = new Content.Dialogue.Referee(hero, new StandardResolver(new ScriptedRng(1)),
                                                       new Core.Tables.GmScreen(new SeededRng(3)), Srd);

            Assert.Null(unlucky.Settle(Content.Dialogue.Request.Parse("save dex 15", out _)).Visit);
        }

        // and a critical hit in a fight: the hero's, through the same referee
        [Fact]
        public void TheHerosCriticalHitBuysAConsequenceAndAnUnwiredSessionBuysNone()
        {
            foreach (bool wired in new[] { true, false })
            {
                Hero hero = Made("fighter", 3);
                var hall = new MapDraft(8, 4);
                hall.Paint(new Cell(0, 0), new Cell(7, 3), Tile.Floor);
                hall.Enclose();
                hall.PlaceStart(new Cell(0, 1));

                // every die its highest: every attack a critical hit
                var resolver = new StandardResolver(new ScriptedRng(20));
                Battle battle = Battle.Set(Srd, hall.Layout(), hero,
                                           new[] { new Battle.Foe(Srd.Bestiary.Find("skeleton"), new Cell(1, 1)) },
                                           resolver, null, new FightLog());
                var referee = new Content.Dialogue.Referee(hero, resolver, new Core.Tables.GmScreen(new SeededRng(3)), Srd);

                var session = new CombatSession(battle, Srd.Items, Srd.Forms)
                {
                    Consequence = wired ? referee.Consequence : null,
                };
                session.Start();

                ActionOption swing = session.Options().First(o => o.Kind == OptionKind.Attack && o.Enabled &&
                                                                 o.Attack.Reach >= 1 && !o.Attack.IsRanged);
                Assert.True(session.Select(swing));

                ActionResult hit = session.Confirm(session.LegalTargets().First());

                Assert.True(hit.Blow.Attempt.IsCritical);
                if (wired) Assert.Equal(Polarity.Boon, hit.Visit.Consequence.Polarity);
                else Assert.Null(hit.Visit);
            }
        }
        // DOWN OUTSIDE A FIGHT IS A LONG REST (Kathleen, 2026-09-28): a bane that takes the hero to
        // 0 hit points on a story's check wakes it and rests it, and the day is done
        [Fact]
        public void ABaneThatPutsTheHeroDownOutsideAFightIsALongRest()
        {
            bool downed = false;

            for (int seed = 1; seed < 200 && !downed; seed++)
            {
                Hero hero = Made("fighter", 3);
                hero.Actor.Suffer(hero.Actor.Health.Current - 1, DamageType.Bludgeoning);

                var referee = new Content.Dialogue.Referee(hero, new StandardResolver(new ScriptedRng(1)),
                                                           new Core.Tables.GmScreen(new SeededRng(seed)), Srd);

                Content.Dialogue.Settled settled =
                    referee.Settle(Content.Dialogue.Request.Parse("check athletics 15", out _));

                if (settled.Visit.Consequence.Kind != ConsequenceKind.Health)
                {
                    Assert.False(settled.Rested);
                    continue;
                }

                downed = true;

                Assert.True(settled.Rested);
                Assert.False(hero.Actor.IsDown);
                Assert.False(hero.Actor.IsDead);
                Assert.Equal(hero.Actor.Health.Maximum, hero.Actor.Health.Current);
            }

            Assert.True(downed, "no seed drew a bane that hurts");
        }
    }
}
