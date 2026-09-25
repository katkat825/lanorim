using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Sheet;
using Core.Characters;

namespace Content.Tests
{
    // the SRD 5.2.1 check of 2026-09-25, weapons, armor and the starting kit (p.89-92), and the
    // speed rules that ride on what is worn
    public class SrdItemCheckTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Made(string cls, int level = 1)
        {
            CharacterClass made = Srd.Class(cls);

            var hero = new Hero("Tess", made, Srd.Kind("human"), Srd.Background("soldier"),
                                Creation.Creation.Standard(made), level);

            hero.Build(new Dictionary<Ability, int> { [Ability.Strength] = 2, [Ability.Constitution] = 1 },
                       made.SkillChoices.Take(made.SkillPicks).ToList(), null, Srd.Items);

            return hero;
        }

        [Fact]
        public void TheKitPutsTheBigWeaponInHandUnlessThereIsAShieldToCarry()
        {
            Assert.Contains(Made("barbarian").Attacks, a => a.Id == "greataxe");
            Assert.DoesNotContain(Made("barbarian").Attacks, a => a.Id == "handaxe");
            Assert.Contains(Made("fighter").Attacks, a => a.Id == "greatsword");

            // the Paladin carries a shield, so the longsword
            Assert.Contains(Made("paladin").Attacks, a => a.Id == "longsword");
        }

        [Fact]
        public void AVersatileWeaponUsesItsBiggerDieWithBothHandsFree()
        {
            Attack staff = Made("mage").Attacks.First(a => a.Id == "quarterstaff");

            Assert.Equal(Srd.Items.Find("quarterstaff").Attack.Versatile, staff.Damage);

            // the Paladin's longsword shares its arm with a shield: the one-handed die
            Attack sword = Made("paladin").Attacks.First(a => a.Id == "longsword");

            Assert.Equal(Srd.Items.Find("longsword").Attack.Damage, sword.Damage);
        }

        [Fact]
        public void AHeavyWeaponIsUnwieldyBelowThirteen()
        {
            Attack greatsword = Srd.Items.Find("greatsword").Attack;

            var weak = new Actor("weak", 1, new AbilityScores());
            Assert.True(weak.AttackLeansWith(greatsword).disadvantage);

            var strong = new Actor("strong", 1, new AbilityScores());
            strong.Scores.Raise(Ability.Strength, 3);
            Assert.False(strong.AttackLeansWith(greatsword).disadvantage);
        }

        [Fact]
        public void ArmorTooHeavyForYourStrengthCostsTenFeet()
        {
            var weak = new Actor("weak", 1, new AbilityScores()) { Speed = 30 };

            weak.Armor = new ArmorProfile(ArmorWeight.Heavy, 18, strengthRequirement: 15);
            Assert.Equal(20, weak.Speed);

            weak.Armor = ArmorProfile.Unarmored;
            Assert.Equal(30, weak.Speed);

            // a statblock's armor names no Strength, so a monster never pays it
            Assert.Equal(30, Srd.Bestiary.Find("skeleton").Spawn().Speed);
        }

        [Fact]
        public void FastMovementIsTenFeetOutOfHeavyArmorOnly()
        {
            Hero barbarian = Made("barbarian", 5);

            Assert.Equal(40, barbarian.Actor.Speed);

            barbarian.Actor.Armor = new ArmorProfile(ArmorWeight.Heavy, 16);
            Assert.Equal(30, barbarian.Actor.Speed);

            barbarian.Actor.Armor = new ArmorProfile(ArmorWeight.Medium, 14);
            Assert.Equal(40, barbarian.Actor.Speed);
        }

        [Fact]
        public void ADownedHeroGetsNothingBackFromALongRest()
        {
            Hero barbarian = Made("barbarian", 3);
            Feature rage = barbarian.Features.First(f => f.Id == "rage");

            Assert.True(barbarian.Invoke(rage));
            barbarian.EndStance(rage);
            int left = barbarian.UsesLeft(rage);

            barbarian.Actor.Suffer(barbarian.Actor.Health.Current, DamageType.Slashing);
            Assert.True(barbarian.Actor.IsDown);

            barbarian.LongRest();

            // SRD 5.2.1: a rest needs at least 1 hit point to start
            Assert.Equal(left, barbarian.UsesLeft(rage));
            Assert.True(barbarian.Actor.IsDown);
        }

        [Fact]
        public void TheLightBonusAttackLeavesOffAPositiveModifierButKeepsANegativeOne()
        {
            Hero rogue = Made("rogue");
            Attack dagger = Srd.Items.Find("dagger").Attack;

            Attack bonus = rogue.LightBonusAttack(dagger);

            Assert.False(bonus.AddsAbilityToDamage);
            Assert.Equal(dagger.DamageBonus + System.Math.Min(0, rogue.Actor.AbilityModifier(dagger.AbilityFor(rogue.Actor))),
                         bonus.DamageBonus);
        }
    }
}
