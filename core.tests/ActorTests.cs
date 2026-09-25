using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Rules;

namespace Core.Tests
{
    public class ArmorClassTests
    {
        static Actor With(int dexterity) =>
            new Actor("t", 1, new AbilityScores(10, dexterity, 10, 10, 10, 10));

        [Fact]
        public void UnarmoredIsTenPlusDexterity()
        {
            Assert.Equal(13, With(16).ArmorClass);
            Assert.Equal(10, With(10).ArmorClass);
        }

        [Fact]
        public void LightArmorTakesAllOfDexterity()
        {
            Actor a = With(18);
            a.Armor = new ArmorProfile(ArmorWeight.Light, 11); // studded leather

            Assert.Equal(15, a.ArmorClass);
        }

        [Fact]
        public void MediumArmorCapsDexterityAtTwo()
        {
            Actor a = With(18);
            a.Armor = new ArmorProfile(ArmorWeight.Medium, 14); // half plate

            Assert.Equal(16, a.ArmorClass);
        }

        [Fact]
        public void HeavyArmorIgnoresDexterityEntirelyEvenWhenItIsNegative()
        {
            Actor a = With(6);
            a.Armor = new ArmorProfile(ArmorWeight.Heavy, 18); // plate

            Assert.Equal(18, a.ArmorClass);
        }

        [Fact]
        public void AShieldAddsTwoOnTopOfAnything()
        {
            Actor a = With(14);
            a.Armor = new ArmorProfile(ArmorWeight.Heavy, 16);
            a.HasShield = true;

            Assert.Equal(18, a.ArmorClass);
        }

        [Fact]
        public void UnarmoredDefenceReplacesTheArmorAndNotTheShield()
        {
            var a = new Actor("barbarian", 1, new AbilityScores(16, 14, 16, 8, 10, 10));
            a.UnarmoredDefense = Ability.Constitution;
            a.HasShield = true;

            // 10 + dex 2 + con 3 + shield 2
            Assert.Equal(17, a.ArmorClass);
        }

        [Fact]
        public void PuttingArmorOnSilencesUnarmoredDefence()
        {
            var a = new Actor("barbarian", 1, new AbilityScores(16, 14, 16, 8, 10, 10));
            a.UnarmoredDefense = Ability.Constitution;
            a.Armor = new ArmorProfile(ArmorWeight.Heavy, 16);

            Assert.Equal(16, a.ArmorClass);
        }
    }

    public class HealthTests
    {
        [Fact]
        public void TemporaryHitPointsAreSpentFirstAndAreNotHealing()
        {
            var health = new Health(20);

            health.GrantTemporary(5);
            health.Take(3);

            Assert.Equal(20, health.Current);
            Assert.Equal(2, health.Temporary);

            health.Take(4);

            Assert.Equal(18, health.Current);
            Assert.Equal(0, health.Temporary);
        }

        [Fact]
        public void TemporaryHitPointsDoNotStackTheBiggerPoolWins()
        {
            var health = new Health(20);

            health.GrantTemporary(5);
            health.GrantTemporary(3);

            Assert.Equal(5, health.Temporary);

            health.GrantTemporary(8);

            Assert.Equal(8, health.Temporary);
        }

        [Fact]
        public void DamageFloorsAtZeroAndReportsWhatItActuallyTook()
        {
            var health = new Health(10);

            Assert.Equal(10, health.Take(30));
            Assert.Equal(0, health.Current);
            Assert.True(health.IsDown);
        }

        [Fact]
        public void HealingStopsAtTheMaximum()
        {
            var health = new Health(10);

            health.Take(4);

            Assert.Equal(4, health.Heal(99));
            Assert.Equal(10, health.Current);
        }

        [Fact]
        public void ALongRestFillsHitPointsAndReturnsEveryHitDie()
        {
            var health = new Health(30, Die.D10, 5);

            health.Take(25);
            health.SpendHitDie(new StandardResolver(new ScriptedRng(6)), 2);

            Assert.Equal(4, health.HitDice);

            health.LongRest();

            Assert.Equal(30, health.Current);
            Assert.Equal(5, health.HitDice);
        }

        [Fact]
        public void SpendingAHitDieHealsTheRollPlusConstitution()
        {
            var health = new Health(30, Die.D10, 5);

            health.Take(20);

            Assert.Equal(9, health.SpendHitDie(new StandardResolver(new ScriptedRng(7)), 2));
            Assert.Equal(4, health.HitDice);
        }

        [Fact]
        public void SpendingAHitDieAtFullHealthWastesNothing()
        {
            var health = new Health(30, Die.D10, 5);

            Assert.Equal(0, health.SpendHitDie(new StandardResolver(new ScriptedRng(7)), 2));
            Assert.Equal(5, health.HitDice);
        }

        [Fact]
        public void AHitDieNeverHealsLessThanOneEvenWithABadConstitution()
        {
            var health = new Health(30, Die.D6, 5);

            health.Take(20);

            Assert.Equal(1, health.SpendHitDie(new StandardResolver(new ScriptedRng(1)), -3));
        }
    }

    public class DamageTests
    {
        static Actor Target()
        {
            var actor = new Actor("t");
            actor.SetHealth(new Health(40));
            return actor;
        }

        [Fact]
        public void ResistanceHalvesAndRoundsDown()
        {
            Actor t = Target();
            t.SetDefense(DamageType.Fire, Defense.Resistant);

            Assert.Equal(3, t.Suffer(7, DamageType.Fire));
            Assert.Equal(7, t.Suffer(7, DamageType.Cold));
        }

        [Fact]
        public void VulnerabilityDoublesAndImmunityTakesNothing()
        {
            Actor t = Target();
            t.SetDefense(DamageType.Cold, Defense.Vulnerable);
            t.SetDefense(DamageType.Poison, Defense.Immune);

            Assert.Equal(10, t.Suffer(5, DamageType.Cold));
            Assert.Equal(0, t.Suffer(50, DamageType.Poison));
        }

        [Fact]
        public void FallingToZeroLaysTheActorOut()
        {
            Actor t = Target();

            t.Suffer(40, DamageType.Bludgeoning);

            Assert.True(t.IsDown);
            Assert.True(t.Has(Condition.Unconscious));
            Assert.False(t.CanAct);
        }

        [Fact]
        public void HealingAboveZeroGetsItBackUp()
        {
            Actor t = Target();

            t.Suffer(40, DamageType.Bludgeoning);
            t.Mend(5);

            Assert.False(t.Has(Condition.Unconscious));
            Assert.True(t.CanAct);
        }
    }

    public class StrikeTests
    {
        static Actor Attacker() =>
            new Actor("hero", 5, new AbilityScores(18, 14, 14, 10, 10, 10), Allegiance.Hero);

        static Actor Dummy(int ac = 10, int hp = 100)
        {
            var actor = new Actor("dummy");
            actor.SetHealth(new Health(hp));
            actor.Armor = new ArmorProfile(ArmorWeight.Heavy, ac);
            return actor;
        }

        static readonly Attack Longsword =
            new Attack("longsword", DiceRoll.Parse("1d8"), DamageType.Slashing);

        [Fact]
        public void AnAttackAddsAbilityAndProficiency()
        {
            // str +4, proficiency +3 at level 5
            Assert.Equal(7, Longsword.Modifier(Attacker()));
        }

        [Fact]
        public void FinesseTakesTheBetterOfStrengthAndDexterity()
        {
            var rapier = new Attack("rapier", DiceRoll.Parse("1d8"), DamageType.Piercing,
                                    finesse: true);

            var nimble = new Actor("rogue", 1, new AbilityScores(8, 18, 12, 10, 10, 10));

            Assert.Equal(Ability.Dexterity, rapier.AbilityFor(nimble));
            Assert.Equal(6, rapier.Modifier(nimble)); // dex +4, proficiency +2
        }

        [Fact]
        public void AHitTakesWeaponDicePlusTheAbilityModifier()
        {
            var resolver = new StandardResolver(new ScriptedRng(15, 5));

            Blow blow = Strike.Make(resolver, Attacker(), Dummy(), Longsword);

            Assert.True(blow.Hit);
            Assert.Equal(9, blow.Suffered); // 5 on the d8, +4 strength
        }

        [Fact]
        public void ACriticalDoublesTheDiceOnly()
        {
            var resolver = new StandardResolver(new ScriptedRng(20, 5, 5));

            Blow blow = Strike.Make(resolver, Attacker(), Dummy(), Longsword);

            Assert.True(blow.Critical);
            Assert.Equal(14, blow.Suffered); // 5 + 5 on two d8, +4 strength once
        }

        [Fact]
        public void AMissTakesNothingAndRollsNoDamage()
        {
            var resolver = new StandardResolver(new ScriptedRng(2, 8));
            Actor dummy = Dummy(20);

            Blow blow = Strike.Make(resolver, Attacker(), dummy, Longsword);

            Assert.False(blow.Hit);
            Assert.Equal(0, blow.Suffered);
            Assert.Equal(100, dummy.Health.Current);
        }

        [Fact]
        public void AProneTargetHandsTheAttackerAdvantage()
        {
            Actor dummy = Dummy(18);
            dummy.Apply(Condition.Prone);

            // the low die would miss AC 18; advantage keeps the high one
            var resolver = new StandardResolver(new ScriptedRng(3, 19, 4));

            Assert.True(Strike.Make(resolver, Attacker(), dummy, Longsword).Hit);
        }

        [Fact]
        public void AFrightenedAttackerSwingsAtDisadvantage()
        {
            Actor attacker = Attacker();
            attacker.Apply(Condition.Frightened);

            var resolver = new StandardResolver(new ScriptedRng(19, 3, 4));

            Assert.False(Strike.Make(resolver, attacker, Dummy(18), Longsword).Hit);
        }

        [Fact]
        public void RidersAddTheirOwnDamageAndDoubleOnACrit()
        {
            var sneak = new Rider("sneak_attack", DiceRoll.Parse("2d6"));

            var resolver = new StandardResolver(new ScriptedRng(20, 4, 4, 3, 3, 3, 3));

            Blow blow = Strike.Make(resolver, Attacker(), Dummy(), Longsword,
                                    riders: new[] { sneak });

            // weapon 4+4 +4 str = 12, then four d6 at 3 = 12
            Assert.Equal(24, blow.Suffered);
            Assert.Single(blow.Riders);
        }

        [Fact]
        public void ACriticalOnlyRiderStaysHomeOnAnOrdinaryHit()
        {
            var rider = new Rider("shatter", DiceRoll.Parse("1d6"), onlyOnCritical: true);

            var resolver = new StandardResolver(new ScriptedRng(15, 5));

            Blow blow = Strike.Make(resolver, Attacker(), Dummy(), Longsword,
                                    riders: new[] { rider });

            Assert.True(blow.Hit);
            Assert.Empty(blow.Riders);
            Assert.Equal(9, blow.Suffered);
        }

        [Fact]
        public void ARidersDamageTakesTheTargetsResistanceForItsOwnType()
        {
            var flame = new Rider("flame_tongue", DiceRoll.Parse("2d6"), DamageType.Fire);

            Actor dummy = Dummy();
            dummy.SetDefense(DamageType.Fire, Defense.Immune);

            var resolver = new StandardResolver(new ScriptedRng(15, 5, 6, 6));

            Blow blow = Strike.Make(resolver, Attacker(), dummy, Longsword,
                                    riders: new[] { flame });

            Assert.Equal(9, blow.Suffered);   // the fire did nothing
            Assert.Equal(21, blow.Rolled);    // but it was rolled, and the sheet says so
        }
    }

    // a borrowed body: core's half of Wild Shape, with no druid anywhere in it
    public class ShapeTests
    {
        static readonly Shape Bear =
            new Shape("black_bear", "wild_shape", 15, 12, 14, 11, 30, new[] { Skill.Perception });

        static Actor Wearer()
        {
            var actor = new Actor("t", 4, new AbilityScores(8, 14, 13, 10, 16, 10));

            actor.Armor = new ArmorProfile(ArmorWeight.Medium, 14);
            actor.HasShield = true;

            return actor;
        }

        [Fact]
        public void TheShapesArmorClassReplacesTheArmorButNotTheBoons()
        {
            Actor actor = Wearer();

            actor.Assume(Bear);
            Assert.Equal(11, actor.ArmorClass);

            actor.Boons.Add(new Boon("shield_of_faith", armorClass: 2));
            Assert.Equal(13, actor.ArmorClass);
        }

        [Fact]
        public void RevertingRestoresTheBodyAndTakesOnlyTheShapesBoons()
        {
            Actor actor = Wearer();
            int armorClass = actor.ArmorClass;
            int perception = actor.CheckModifier(Skill.Perception);

            actor.Boons.Add(new Boon("bless", checks: true, flat: 1, duration: Duration.Rest));
            actor.Assume(Bear);

            Assert.Equal(15, actor.Scores[Ability.Strength]);
            Assert.Equal(16, actor.Scores[Ability.Wisdom]);
            Assert.Equal(perception + 1 + actor.ProficiencyBonus,
                         actor.CheckModifier(Skill.Perception));

            Assert.Same(Bear, actor.Revert());

            Assert.Equal(8, actor.Scores[Ability.Strength]);
            Assert.Equal(14, actor.Scores[Ability.Dexterity]);
            Assert.Equal(13, actor.Scores[Ability.Constitution]);
            Assert.Equal(30, actor.Speed);
            Assert.Equal(armorClass, actor.ArmorClass);
            Assert.True(actor.Boons.Has("bless"));
            Assert.Null(actor.Revert());
        }

        [Fact]
        public void GoingDownTakesTheShapeOff()
        {
            Actor actor = Wearer();
            actor.SetHealth(new Health(10));

            actor.Assume(Bear);
            actor.Suffer(10, DamageType.Slashing);

            Assert.False(actor.IsShifted);
            Assert.Equal(8, actor.Scores[Ability.Strength]);
        }
    }
}
