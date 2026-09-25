using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Content.Sheet;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Magic;
using Core.Resolution;
using Core.Space;

namespace Content.Tests
{
    // the sheet and the spell card, as the UI binds them. every assertion here is "the sheet says
    // what the rules say", which is the only property a sheet has to have
    public class SheetViewTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Fighter(int level = 1)
        {
            var hero = new Hero("Brenna", Srd.Class("fighter"), Srd.Kind("human"),
                                Srd.Background("soldier"),
                                Creation.Creation.Standard(Srd.Class("fighter")), level)
            {
                Alignment = Alignment.LawfulGood,
            };

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Strength] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Athletics, Skill.Perception }, null, Srd.Items);

            return hero;
        }

        static Hero Mage(SpellResourceMode mode = SpellResourceMode.Slots, int level = 3)
        {
            var hero = new Hero("Ilse", Srd.Class("mage"), Srd.Kind("human"), Srd.Background("sage"),
                                Creation.Creation.Standard(Srd.Class("mage")), level, null, mode);

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Intelligence] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Arcana, Skill.History }, null, Srd.Items,
                       new[] { Srd.Spells.Find("fire_bolt"), Srd.Spells.Find("magic_missile"),
                               Srd.Spells.Find("shield"), Srd.Spells.Find("fireball"),
                               Srd.Spells.Find("burning_hands") });

            return hero;
        }

        [Fact]
        public void TheIdentityBlockIsKeysAndTheNameTheyTyped()
        {
            SheetView sheet = SheetView.Of(Fighter());

            Assert.Equal("Brenna", sheet.Name);
            Assert.Equal("class.fighter.name", sheet.ClassKey);
            Assert.Equal("species.human.name", sheet.SpeciesKey);
            Assert.Equal("ui.alignment.lawful_good.name", sheet.AlignmentKey);
            Assert.Equal(KeyConventions.BackgroundName("soldier"), sheet.BackgroundKey);
            Assert.Null(sheet.LineageKey);
        }

        [Fact]
        public void TheBasicsAreTheActorsOwnNumbers()
        {
            Hero hero = Fighter(3);
            SheetView sheet = SheetView.Of(hero);

            Assert.Equal(hero.Actor.ArmorClass, sheet.ArmorClass);
            Assert.Equal(hero.Actor.AbilityModifier(Ability.Dexterity), sheet.Initiative);
            Assert.Equal(hero.Actor.Health.Maximum, sheet.MaxHitPoints);
            Assert.Equal(3, sheet.MaxHitDice);
            Assert.Equal(Die.D10, sheet.HitDie);
            Assert.Equal(2, sheet.ProficiencyBonus);
        }

        [Fact]
        public void EveryAbilityAndEverySkillHasARow()
        {
            SheetView sheet = SheetView.Of(Fighter());

            Assert.Equal(6, sheet.Abilities.Count);
            Assert.Equal(18, sheet.Skills.Count);

            SkillRow athletics = sheet.Skills.Single(s => s.Skill == Skill.Athletics);

            Assert.Equal(Training.Proficient, athletics.Training);
            Assert.Equal("ability.str.short", athletics.AbilityShortKey);
            Assert.Equal(10 + athletics.Modifier, athletics.Passive);

            AbilityRow strength = sheet.Abilities.Single(a => a.Ability == Ability.Strength);
            Assert.True(strength.SaveProficient);
        }

        [Fact]
        public void OutsideAFightTheEconomyIsWhatATurnHolds()
        {
            SheetView five = SheetView.Of(Fighter(5));

            // Extra Attack on top of the base two
            Assert.Equal(3, five.Actions);
            Assert.Equal(1, five.BonusActions);
            Assert.Equal(1, five.Reactions);
            Assert.Equal(1, five.ExtraActionsBanked);
            Assert.False(five.InAFight);
            Assert.Equal(0, five.SwapCostActions);
        }

        [Fact]
        public void OnItsTurnTheEconomyIsWhatIsLeft()
        {
            Hero hero = Fighter(5);

            Assert.True(MapReader.TryRead(@"
+-+-+-+
|@ . .|
+-+-+-+", out MapLayout map, out _));

            var fight = new Encounter(new StandardResolver(new ScriptedRng(20, 1)),
                                      new Battlefield(map));

            var goblin = new Actor("goblin");
            goblin.SetHealth(new Health(7));

            fight.Enlist(hero.Actor, new Cell(0, 0), hero.Budget);
            fight.Enlist(goblin, new Cell(2, 0));
            fight.Begin();

            Turn turn = fight.Next();
            turn.Take(Spend.Action);
            turn.Take(Spend.Movement, 5);

            SheetView sheet = SheetView.Of(hero, fight, turn);

            Assert.True(sheet.InAFight);
            Assert.Equal(2, sheet.Actions);
            Assert.Equal(hero.Actor.Speed - 5, sheet.Movement);
            Assert.Equal(1, sheet.SwapCostActions);
        }

        [Fact]
        public void AWeaponRowCarriesItsBonusDamageAndCrit()
        {
            Hero hero = Fighter();
            SheetView sheet = SheetView.Of(hero);

            WeaponRow sword = sheet.Weapons.First(w => w.Attack.Id != "unarmed_strike");

            Assert.Equal(sword.Attack.Modifier(hero.Actor), sword.AttackBonus);
            Assert.Equal(sword.Damage.Count * 2, sword.CriticalDamage.Count);
            Assert.Equal(sword.Damage.Modifier, sword.CriticalDamage.Modifier);
            Assert.Equal(20, sword.CriticalOn);
            Assert.Equal(5, sword.ReachFeet);
        }

        [Fact]
        public void AMartialHasNoSpellsAndTheSpecialSectionListsWhatHasAButton()
        {
            SheetView sheet = SheetView.Of(Fighter(2));

            Assert.False(sheet.Casts);
            Assert.Empty(sheet.Spells);

            SpecialRow wind = sheet.Special.Single(s => s.Feature.Id == "second_wind");

            Assert.Equal(2, wind.UsesMost);
            Assert.Equal(2, wind.UsesLeft);
            Assert.True(wind.Available);
        }

        [Fact]
        public void ASlotsCasterShowsItsGridAndItsCards()
        {
            SheetView sheet = SheetView.Of(Mage());

            Assert.True(sheet.Casts);
            Assert.Equal(SpellResourceMode.Slots, sheet.Resource);
            Assert.Equal((1, 4, 4), sheet.Slots[0]);
            Assert.Equal((2, 2, 2), sheet.Slots[1]);
            Assert.Null(sheet.ConcentratingKey);

            // cantrips first
            Assert.Equal("fire_bolt", sheet.Spells[0].Spell.Id);
        }

        [Fact]
        public void APointsCasterShowsOnePool()
        {
            SheetView sheet = SheetView.Of(Mage(SpellResourceMode.Points));

            Assert.Equal(SpellResourceMode.Points, sheet.Resource);
            Assert.Empty(sheet.Slots);
            Assert.True(sheet.PointsMost > 0);
            Assert.Equal(sheet.PointsMost, sheet.PointsLeft);
        }

        [Fact]
        public void ACardSaysWhereAndHowMuchAndWhatItCosts()
        {
            Hero mage = Mage();

            SpellCard bolt = SpellCard.Of(Srd.Spells.Find("fire_bolt"), mage.Caster);

            Assert.Equal(RangeKind.Feet, bolt.RangeKind);
            Assert.Equal(120, bolt.RangeFeet);
            Assert.True(bolt.RollsToHit);
            Assert.Equal(mage.Caster.AttackModifier, bolt.AttackBonus);
            Assert.Equal("ui.spell_cost.at_will.name", bolt.CostKey);

            SpellCard hands = SpellCard.Of(Srd.Spells.Find("burning_hands"), mage.Caster);

            Assert.Equal(RangeKind.Self, hands.RangeKind);
            Assert.Equal("ui.area.cone.name", hands.AreaKey);
            Assert.Equal(15, hands.AreaFeet);
            Assert.Equal(mage.Caster.SaveDc, hands.SaveDc);
            Assert.Equal("ui.spell_cost.slot.name", hands.CostKey);
            Assert.Equal(1, hands.CostAmount);

            // a level 3 mage has 2nd-level slots, so a burning hands can go up one
            Assert.Equal(2, hands.HighestLevel);
        }

        [Fact]
        public void ACardTheCasterCannotAffordIsGreyWithAReason()
        {
            SpellCard fireball = SpellCard.Of(Srd.Spells.Find("fireball"), Mage().Caster);

            Assert.False(fireball.Castable);
            Assert.Equal("ui.not_castable.spent.name", fireball.NotCastableKey);
        }

        [Fact]
        public void AReactionCardSaysWhatItWaitsFor()
        {
            SpellCard shield = SpellCard.Of(Srd.Spells.Find("shield"), Mage().Caster);

            Assert.Equal("ui.casting_time.reaction.name.hit", shield.CastingTimeKey);
            Assert.True(shield.Castable);
        }

        [Fact]
        public void AnAdaptedSpellsCardSaysSo()
        {
            Assert.True(SpellCard.Of(Srd.Spells.Find("wish")).Adapted);
            Assert.False(SpellCard.Of(Srd.Spells.Find("fireball")).Adapted);
        }

        [Fact]
        public void APointsCardCostsThePriceOfItsLevel()
        {
            SpellCard missile = SpellCard.Of(Srd.Spells.Find("magic_missile"),
                                             Mage(SpellResourceMode.Points).Caster);

            Assert.Equal("ui.spell_cost.points.name", missile.CostKey);
            Assert.Equal(SpellPoints.CostOf(1), missile.CostAmount);
        }

        [Fact]
        public void TheAlignmentIsChosenAtCreationAndDefaultsToNeutral()
        {
            var creation = new Creation.Creation(Srd, Srd.Backgrounds.ToList());

            Assert.Equal(Alignment.Neutral, creation.Alignment);

            creation.Pick(Alignment.ChaoticGood);

            Assert.Equal(Alignment.ChaoticGood, creation.Alignment);
        }

        [Fact]
        public void EveryAlignmentReadsBackFromItsId()
        {
            foreach (Alignment alignment in Alignments.All)
            {
                Assert.True(Alignments.TryParse(alignment.Id(), out Alignment back));
                Assert.Equal(alignment, back);
                Assert.True(KeyConventions.IsWellFormed(alignment.NameKey()));
            }
        }
    }
}
