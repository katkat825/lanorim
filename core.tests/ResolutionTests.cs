using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Rules;

namespace Core.Tests
{
    public class AdvantageTests
    {
        [Fact]
        public void AdvantageAndDisadvantageCancel()
        {
            Assert.Equal(Advantage.Flat, Advantages.Of(true, true));
            Assert.Equal(Advantage.Advantage, Advantages.Of(true, false));
            Assert.Equal(Advantage.Disadvantage, Advantages.Of(false, true));
            Assert.Equal(Advantage.Flat, Advantages.Of(false, false));
        }

        [Fact]
        public void TwoSourcesOnOneSideAreStillOneAdvantage()
        {
            Advantage a = Advantage.Advantage.And(Advantage.Advantage);

            Assert.Equal(Advantage.Advantage, a);
            Assert.Equal(2, a.Dice());
        }

        [Fact]
        public void OneOnEachSideIsAFlatRoll() =>
            Assert.Equal(Advantage.Flat, Advantage.Advantage.And(Advantage.Disadvantage));

        [Fact]
        public void FlatIsTheIdentity()
        {
            Assert.Equal(Advantage.Disadvantage, Advantage.Flat.And(Advantage.Disadvantage));
            Assert.Equal(Advantage.Advantage, Advantage.Advantage.And(Advantage.Flat));
        }
    }

    public class D20RollTests
    {
        [Fact]
        public void AFlatRollThrowsOneDie()
        {
            D20Roll roll = D20Roll.Make(new ScriptedRng(13), 4);

            Assert.Single(roll.Faces);
            Assert.Equal(13, roll.Natural);
            Assert.Equal(17, roll.Total);
        }

        [Fact]
        public void AdvantageKeepsTheHigherAndShowsBoth()
        {
            D20Roll roll = D20Roll.Make(new ScriptedRng(4, 17), 0, Advantage.Advantage);

            Assert.Equal(new[] { 4, 17 }, roll.Faces);
            Assert.Equal(17, roll.Natural);
        }

        [Fact]
        public void DisadvantageKeepsTheLower()
        {
            D20Roll roll = D20Roll.Make(new ScriptedRng(4, 17), 0, Advantage.Disadvantage);

            Assert.Equal(4, roll.Natural);
        }

        [Fact]
        public void ANaturalTwentyIsTheFaceThatCountsNotTheTotal()
        {
            D20Roll roll = D20Roll.Make(new ScriptedRng(20), -7);

            Assert.True(roll.IsNaturalTwenty);
            Assert.Equal(13, roll.Total);
        }

        [Fact]
        public void ANaturalTwentyOnTheDiscardedDieIsNotANaturalTwenty()
        {
            D20Roll roll = D20Roll.Make(new ScriptedRng(20, 3), 0, Advantage.Disadvantage);

            Assert.False(roll.IsNaturalTwenty);
            Assert.Equal(3, roll.Natural);
        }
    }

    public class AttemptTests
    {
        static Attempt Attack(int natural, int modifier, int ac) =>
            new Attempt(RollKind.Attack, D20Roll.Fixed(natural, modifier), ac);

        static Attempt Check(int natural, int modifier, int dc) =>
            new Attempt(RollKind.Check, D20Roll.Fixed(natural, modifier), dc);

        [Fact]
        public void AnAttackHitsOnTwentyWhateverTheArmor()
        {
            Attempt attempt = Attack(20, 0, 99);

            Assert.True(attempt.Succeeded);
            Assert.True(attempt.IsCritical);
        }

        [Fact]
        public void AnAttackMissesOnOneWhateverTheBonus()
        {
            Attempt attempt = Attack(1, 50, 5);

            Assert.False(attempt.Succeeded);
            Assert.True(attempt.IsCriticalMiss);
        }

        [Fact]
        public void ACheckIsDecidedPurelyByTheNumbers()
        {
            // the keeper delta: a natural 1 on an easy check that your modifiers still pass, passes
            Attempt passed = Check(1, 15, 10);

            Assert.True(passed.Succeeded);
            Assert.True(passed.DrawsConsequence);

            // and a natural 20 that still doesn't reach the DC, fails
            Attempt failed = Check(20, 0, 30);

            Assert.False(failed.Succeeded);
            Assert.True(failed.DrawsConsequence);
        }

        [Fact]
        public void ACheckNeverCrits()
        {
            Assert.False(Check(20, 0, 10).IsCritical);
            Assert.False(Check(1, 0, 10).IsCriticalMiss);
        }

        [Fact]
        public void AnOrdinaryCheckDrawsNoConsequence() =>
            Assert.False(Check(11, 3, 10).DrawsConsequence);

        [Fact]
        public void ACriticalHitDrawsFromTheSamePool() =>
            Assert.True(Attack(20, 0, 10).DrawsConsequence);

        [Fact]
        public void MarginSaysByHowMuch()
        {
            Assert.Equal(4, Check(11, 3, 10).Margin);
            Assert.Equal(-2, Check(5, 3, 10).Margin);
        }
    }

    public class ChecksTests
    {
        static Actor Hero()
        {
            var hero = new Actor("hero", 5, new AbilityScores(10, 16, 14, 8, 12, 10),
                                 Allegiance.Hero);

            hero.Train(Skill.Stealth);
            hero.TrainSave(Ability.Dexterity);

            return hero;
        }

        [Fact]
        public void ASkillCheckAddsAbilityAndProficiency()
        {
            Actor hero = Hero();

            // dex +3, proficient at level 5 is +3
            Assert.Equal(6, hero.CheckModifier(Skill.Stealth));

            // untrained: the ability alone
            Assert.Equal(3, hero.CheckModifier(Skill.Acrobatics));
        }

        [Fact]
        public void ExpertiseDoublesProficiencyOnTheSkillItIsGivenTo()
        {
            Actor hero = Hero();

            hero.Train(Skill.Stealth, Training.Expert);

            Assert.Equal(9, hero.CheckModifier(Skill.Stealth));
        }

        [Fact]
        public void TrainingNeverDemotes()
        {
            Actor hero = Hero();

            hero.Train(Skill.Stealth, Training.Expert);
            hero.Train(Skill.Stealth);

            Assert.Equal(Training.Expert, hero.TrainingIn(Skill.Stealth));
        }

        [Fact]
        public void ARawAbilityCheckGetsNoProficiency()
        {
            Actor hero = Hero();

            Assert.Equal(3, hero.CheckModifier(Ability.Dexterity));
        }

        [Fact]
        public void ASaveAddsProficiencyOnlyWhereTheClassHasIt()
        {
            Actor hero = Hero();

            Assert.Equal(6, hero.SaveModifier(Ability.Dexterity));
            Assert.Equal(2, hero.SaveModifier(Ability.Constitution));
        }

        [Fact]
        public void BeingStunnedAutoFailsStrengthAndDexteritySaves()
        {
            Actor hero = Hero();
            var resolver = new StandardResolver(new ScriptedRng(20));

            hero.Apply(Condition.Stunned);

            Assert.False(Checks.Save(resolver, hero, Ability.Dexterity, 5).Succeeded);
            Assert.False(Checks.Save(resolver, hero, Ability.Strength, 5).Succeeded);

            // a wisdom save is still rolled
            Assert.True(Checks.Save(resolver, hero, Ability.Wisdom, 5).Succeeded);
        }

        [Fact]
        public void BeingPoisonedSoursChecksButNotSaves()
        {
            Actor hero = Hero();

            hero.Apply(Condition.Poisoned);

            Assert.Equal(Advantage.Disadvantage, hero.CheckAdvantage);

            var resolver = new StandardResolver(new ScriptedRng(18, 2));

            // the check takes the lower of the two
            Assert.Equal(2, Checks.Check(resolver, hero, Skill.Stealth, 10).Natural);
        }

        [Fact]
        public void APassiveScoreIsTenPlusTheModifierAndRollsNothing() =>
            Assert.Equal(16, Checks.Passive(Hero(), Skill.Stealth));

        [Fact]
        public void TheDeathSaveIsABareD20AgainstTen()
        {
            Actor hero = Hero();
            hero.SetHealth(new Health(20));

            hero.Suffer(50, DamageType.Slashing);

            Assert.True(hero.IsDown);
            Assert.True(hero.Has(Condition.Unconscious));

            Attempt saved = Checks.DeathSave(new StandardResolver(new ScriptedRng(10)), hero);

            Assert.True(saved.Succeeded);
            Assert.Equal(1, hero.Health.Current);
            Assert.False(hero.Has(Condition.Unconscious));
        }

        [Fact]
        public void ADeathSaveTakesNoModifiersFromAnyone()
        {
            Actor hero = Hero();
            hero.SetHealth(new Health(20));
            hero.AddSaveBonus(Ability.Constitution, 10);
            hero.Suffer(50, DamageType.Slashing);

            // a 9 is a 9 - nothing on the sheet may push it to 10
            Attempt failed = Checks.DeathSave(new StandardResolver(new ScriptedRng(9)), hero);

            Assert.False(failed.Succeeded);
            Assert.Equal(9, failed.Total);
            Assert.Equal(0, hero.Health.Current);
        }
    }
}
