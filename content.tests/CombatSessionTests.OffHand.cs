using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Items;
using Content.Saves;
using Content.Sheet;
using Core.Characters;
using Core.Space;

namespace Content.Tests
{
    // AN OFF HAND (cc_task_open-questions-answers.md 3.1; Kathleen: "there should already have been an off-hand. yes add
    // one"). SRD 5.2.1 Light (p.89): after an Attack action with a Light weapon, the other Light weapon attacks as a
    // Bonus Action, without the ability modifier on its damage. Now the other one can be held: a one-handed Light
    // weapon in the off hand
    public partial class CombatSessionTests
    {
        static Item ItemCalled(string id) => Srd.Items.Find(id);

        // a fighter with a shortsword in the main hand, and a dagger to put in the other
        static Hero DualWielder(string main = "shortsword")
        {
            Hero hero = Made("fighter");

            foreach (Slot slot in Slots.All) hero.TakeOff(slot);

            hero.Pack.Take(ItemCalled(main));
            hero.Pack.Take(ItemCalled("dagger"));
            Assert.True(hero.Wear(ItemCalled(main)));
            Assert.True(hero.HoldInOffHand(ItemCalled("dagger")));

            return hero;
        }

        [Fact]
        public void ALightWeaponGoesInTheOffHandAndSwingsFromIt()
        {
            Hero hero = DualWielder();

            Assert.Equal("dagger", hero.Equipment.In(Slot.OffHand)?.Id);
            Assert.Contains(hero.Attacks, a => a.Id == "dagger" && a.Hand == Hand.Off);
            Assert.Contains(hero.Attacks, a => a.Id == "shortsword" && a.Hand == Hand.Main);
        }

        [Fact]
        public void OnlyAOneHandedLightWeaponFitsTheOffHand()
        {
            Assert.True(ItemCalled("dagger").FitsOffHand);
            Assert.False(ItemCalled("longsword").FitsOffHand);
            Assert.False(ItemCalled("greatsword").FitsOffHand);

            Hero hero = Made("fighter");
            hero.Pack.Take(ItemCalled("longsword"));
            Assert.False(hero.HoldInOffHand(ItemCalled("longsword")));
            Assert.Null(hero.Equipment.In(Slot.OffHand));
        }

        // the off hand holds a weapon or a shield, not both: a shield IS the off hand (cc_task_f 1.6)
        [Fact]
        public void AShieldAndAnOffHandWeaponShareTheHand()
        {
            Hero hero = DualWielder();

            hero.Pack.Take(ItemCalled("shield"));
            Assert.True(hero.Wear(ItemCalled("shield")));

            Assert.Equal("shield", hero.Equipment.In(Slot.OffHand)?.Id);
            Assert.True(hero.Actor.HasShield);
            Assert.True(hero.Pack.Has("dagger"));

            Assert.True(hero.HoldInOffHand(ItemCalled("dagger")));
            Assert.Equal("dagger", hero.Equipment.In(Slot.OffHand)?.Id);
            Assert.False(hero.Actor.HasShield);
            Assert.True(hero.Pack.Has("shield"));
        }

        // "change the pack ui for 'Shield: Shield' to be 'Off hand: Shield' like it is for a dagger" (Kathleen, 2026-10-05)
        [Fact]
        public void AShieldIsWornInTheOffHandAndThePackSaysSo()
        {
            Hero hero = DualWielder();

            hero.Pack.Take(ItemCalled("shield"));
            hero.Wear(ItemCalled("shield"));

            var view = new Content.Screens.PackView(hero, Srd.Items);

            Assert.Contains(view.Worn, w => w.Slot == Slot.OffHand && w.Item.Id == "shield");
            Assert.Equal("ui.pack.slot_off_hand", Content.Screens.PackView.SlotKey(Slot.OffHand));
            Assert.DoesNotContain(Slots.All, s => Core.Words.EnumWords.Id(s) == "shield");
        }

        // a save from before wore its shield in a "shield" slot; it comes back in the off hand
        [Fact]
        public void AnOldSavesShieldSlotIsTheOffHand()
        {
            Hero hero = DualWielder();

            hero.Pack.Take(ItemCalled("shield"));
            hero.Wear(ItemCalled("shield"));

            string json = Content.Saves.SaveWriter.Write(new Content.Saves.SaveGame { Hero = Content.Saves.HeroSaves.Capture(hero) });
            Assert.Contains("\"offhand\": \"shield\"", json);

            string old = json.Replace("\"offhand\": \"shield\"", "\"shield\": \"shield\"");
            Hero back = Content.Saves.HeroSaves.Restore(Content.Saves.SaveReader.Parse(old).Value.Hero, Srd, out var problems);

            Assert.Empty(problems);
            Assert.Equal("shield", back.Equipment.In(Slot.OffHand)?.Id);
            Assert.True(back.Actor.HasShield);
        }

        // after an Attack action with the shortsword, the dagger in the other hand is offered as the bonus attack
        [Theory]
        [InlineData("shortsword")]
        [InlineData("dagger")]
        public void TheLightBonusAttackSwingsTheOtherHand(string main)
        {
            // an ogre: a critical shortsword doesn't end the fight before the bonus attack
            CombatSession session = Session(DualWielder(main), new[] { new Battle.Foe(Srd.Bestiary.Find("ogre"), new Cell(1, 0)) });

            Assert.Contains(session.Options(), o => o.Id == "attack:dagger" + CombatSession.OffHand);

            ActionOption swing = session.Options().First(o => o.Id == "attack:" + main);
            session.Select(swing);
            Actor goblin = session.Fight.Actors.First(a => a.Side != Allegiance.Hero);
            Assert.True(session.Confirm(goblin).Done);

            ActionOption bonus = session.Options().FirstOrDefault(o => o.Id.StartsWith("bonus_attack:"));
            Assert.NotNull(bonus);
            Assert.Equal("bonus_attack:dagger" + CombatSession.OffHand, bonus.Id);
            Assert.Equal(Hand.Off, bonus.Attack.Hand);
            Assert.False(bonus.Attack.AddsAbilityToDamage);
        }

        [Fact]
        public void TheOffHandIsSavedAndComesBack()
        {
            Hero hero = DualWielder();

            Hero back = HeroSaves.Restore(HeroSaves.Capture(hero), Srd, out IReadOnlyList<Content.Schema.ContentProblem> problems);

            Assert.Empty(problems);
            Assert.Equal("dagger", back.Equipment.In(Slot.OffHand)?.Id);
            Assert.Equal("shortsword", back.Equipment.In(Slot.MainHand)?.Id);
        }

        [Fact]
        public void TheSheetSaysWhichHand()
        {
            var sheet = SheetView.Of(DualWielder());

            Assert.Contains(sheet.Weapons, w => w.Hand == Hand.Off);
        }
    }
}
