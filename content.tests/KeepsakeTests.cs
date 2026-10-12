using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Inventory;
using Content.Saves;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Content.SoloPlay;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;
using Xunit;

namespace Content.Tests
{
    // 3.2 (cc_task_h): THE KEEPSAKE. Every hero carries one; the moment they drop to 0, once per long rest, it stands
    // them back up on their Hit Die's full value plus their level, instead of the fall - no Unconscious, no Prone, no
    // death save. The next drop before a long rest is the death save as ever. It can't be sold or dropped, and a save
    // from before it gains one
    [Collection(SoloRulesCollection.Name)]
    public class KeepsakeTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Made(string cls, int level = 1)
        {
            Content.Classes.CharacterClass made = Srd.Class(cls);

            var hero = new Hero("Tess", made, Srd.Kind("human"), Srd.Background("soldier"),
                                Creation.Creation.Standard(made), level);

            hero.Build(null, made.SkillChoices.Take(made.SkillPicks).ToList(), null, Srd.Items, null);
            return hero;
        }

        // the switch on for the test, and back as it was after
        static void With(System.Action test)
        {
            bool was = SoloRules.Keepsake;

            try
            {
                SoloRules.Keepsake = true;
                test();
            }
            finally { SoloRules.Keepsake = was; }
        }

        static void Drop(Hero hero) => hero.Actor.Suffer(hero.Actor.Health.Current + 50, DamageType.Slashing);

        [Fact]
        public void EveryHeroCarriesOneAndItTakesNoSlot() => With(() =>
        {
            Hero hero = Made("mage");

            Assert.True(hero.Pack.Has(Hero.KeepsakeId));
            Assert.Contains(hero.Pack.QuestItems, s => s.Item.Id == Hero.KeepsakeId);
            Assert.DoesNotContain(hero.Pack.Stacks, s => s.Item.Id == Hero.KeepsakeId);
            Assert.True(hero.KeepsakeReady);
        });

        [Fact]
        public void ItHoldsAtZeroAndNotBefore() => With(() =>
        {
            Hero hero = Made("fighter");

            hero.Actor.Suffer(hero.Actor.Health.Current - 1, DamageType.Slashing);

            Assert.Null(hero.HoldUp());
            Assert.True(hero.KeepsakeReady);

            Drop(hero);

            Assert.Equal(Hero.KeepsakeId, hero.HoldUp());
            Assert.False(hero.Actor.IsDown);
            Assert.False(hero.Actor.Has(Condition.Unconscious));
            Assert.False(hero.Actor.Has(Condition.Prone));
        });

        [Theory]
        [InlineData("mage", 1, 7)]
        [InlineData("fighter", 1, 11)]
        [InlineData("mage", 5, 11)]
        [InlineData("barbarian", 3, 15)]
        public void ItHealsTheHitDieInFullPlusTheLevel(string cls, int level, int heals) => With(() =>
        {
            Hero hero = Made(cls, level);

            Assert.Equal(heals, hero.KeepsakeHeals);

            Drop(hero);
            hero.HoldUp();

            Assert.Equal(System.Math.Min(heals, hero.Actor.Health.Maximum), hero.Actor.Health.Current);
        });

        [Fact]
        public void ItsSpentUntilALongRestAndTheNextDropIsTheDeathSave() => With(() =>
        {
            Hero hero = Made("mage");

            Drop(hero);
            Assert.Equal(Hero.KeepsakeId, hero.HoldUp());
            Assert.True(hero.KeepsakeSpent);
            Assert.False(SheetView.Of(hero).Keepsake);

            Drop(hero);
            Assert.Null(hero.HoldUp());
            Assert.True(hero.Actor.IsDown);
            Assert.True(hero.Actor.Has(Condition.Unconscious));

            hero.Actor.Mend(1);
            hero.LongRest();

            Assert.True(hero.KeepsakeReady);
            Assert.True(SheetView.Of(hero).Keepsake);
        });

        // ONCE PER LONG REST, a short rest doesn't give it back (Kathleen, 2026-10-12). While it was kept in the per-rest
        // ledger a short rest did, because the ledger gave back whatever wasn't a feature
        [Fact]
        public void AShortRestDoesntGiveItBack() => With(() =>
        {
            Hero hero = Made("mage");

            Drop(hero);
            Assert.Equal(Hero.KeepsakeId, hero.HoldUp());
            Assert.Equal(1, HeroSaves.Capture(hero).Spent[Hero.KeepsakeId]);

            hero.ShortRest(new StandardResolver(new SeededRng(1)));

            Assert.False(hero.KeepsakeReady);
            Assert.Equal(1, HeroSaves.Capture(hero).Spent[Hero.KeepsakeId]);

            hero.LongRest();

            Assert.True(hero.KeepsakeReady);
        });

        [Fact]
        public void InAFightTheLogHearsItTheHeroKeepsTheirWeaponAndDoesntFall() => With(() =>
        {
            Hero hero = Made("fighter");
            var log = new FightLog();
            var map = new Content.Maps.MapDraft(6, 4);
            map.Paint(new Cell(0, 0), new Cell(5, 3), Tile.Floor);
            map.PlaceStart(new Cell(1, 1));

            Battle battle = Battle.Set(Srd, map.Layout(), hero,
                                       new[] { new Battle.Foe(Srd.Bestiary.Find("goblin"), new Cell(3, 1)) },
                                       new StandardResolver(new SeededRng(1)), null, log);

            string weapon = hero.Equipment.In(Content.Items.Slot.MainHand)?.Id;
            int took = hero.Actor.Suffer(hero.Actor.Health.Current + 30, DamageType.Slashing);
            battle.Fight.Hurt(null, hero.Actor, took, DamageType.Slashing);

            Assert.False(hero.Actor.IsDown);
            Assert.Equal(weapon, hero.Equipment.In(Content.Items.Slot.MainHand)?.Id);
            Assert.Contains(log.Lines, l => l.Key.Contains("held_up"));
            Assert.DoesNotContain(log.Lines, l => l.Key.Contains(".down."));
        });

        [Fact]
        public void ItCantBeSoldOrDropped() => With(() =>
        {
            Hero hero = Made("rogue");
            var merchant = new Merchant("shop", Srd.Items, new[] { "dagger" });
            var pack = new PackView(hero, Srd.Items, merchant);

            Assert.False(pack.Discard(Hero.KeepsakeId));
            Assert.False(pack.Sell(Hero.KeepsakeId).Done);
            Assert.True(hero.Pack.Has(Hero.KeepsakeId));
        });

        [Fact]
        public void ASaveFromBeforeItGainsOneAndASpentOneStaysSpent() => With(() =>
        {
            Hero old = Made("cleric");
            old.Pack.Drop(Hero.KeepsakeId);
            Assert.False(old.Pack.Has(Hero.KeepsakeId));

            Hero back = RoundTrip(old);
            Assert.True(back.Pack.Has(Hero.KeepsakeId));
            Assert.True(back.KeepsakeReady);

            Drop(back);
            back.HoldUp();
            Assert.True(RoundTrip(back).KeepsakeSpent);
        });

        [Fact]
        public void SwitchedOffItDoesNothing()
        {
            bool was = SoloRules.Keepsake;

            try
            {
                SoloRules.Keepsake = false;
                Hero hero = Made("fighter");

                Drop(hero);

                Assert.Null(hero.HoldUp());
                Assert.True(hero.Actor.IsDown);
                Assert.Null(SheetView.Of(hero).Keepsake);
            }
            finally { SoloRules.Keepsake = was; }
        }

        static Hero RoundTrip(Hero hero)
        {
            Read<SaveGame> read = SaveReader.Parse(SaveWriter.Write(new SaveGame { Campaign = "ash_yard", Hero = HeroSaves.Capture(hero) }));
            Assert.True(read.Ok, string.Join("\n", read.Problems));

            Hero back = HeroSaves.Restore(read.Value.Hero, Srd, out IReadOnlyList<ContentProblem> problems);
            Assert.Empty(problems);
            return back;
        }
    }
}
