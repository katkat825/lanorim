using System.Linq;
using Core.Characters;

namespace Core.Tests
{
    public class AbilityTests
    {
        [Theory]
        [InlineData(1, -5)]
        [InlineData(3, -4)]
        [InlineData(8, -1)]
        [InlineData(9, -1)]   // the one C# integer division gets wrong without an explicit floor
        [InlineData(10, 0)]
        [InlineData(11, 0)]
        [InlineData(15, 2)]
        [InlineData(20, 5)]
        public void ModifierFollowsTheSrdTable(int score, int modifier) =>
            Assert.Equal(modifier, Abilities.Modifier(score));

        [Theory]
        [InlineData(8, 0)]
        [InlineData(9, 1)]
        [InlineData(13, 5)]
        [InlineData(14, 7)]
        [InlineData(15, 9)]
        public void PointBuyCostsWhatSrdSaysItCosts(int score, int cost) =>
            Assert.Equal(cost, Abilities.PointBuyCost(score));

        [Theory]
        [InlineData(7)]
        [InlineData(16)]
        public void PointBuyRefusesScoresOffTheLadder(int score) =>
            Assert.Equal(-1, Abilities.PointBuyCost(score));

        [Fact]
        public void TheStandardArraySpendsExactlyTheBudget()
        {
            // 15 14 13 12 10 8 is the array everyone builds; it is 27 points to the penny
            var scores = new AbilityScores(15, 14, 13, 12, 10, 8);

            Assert.Equal(Abilities.PointBuyBudget, scores.PointBuySpend);
            Assert.True(scores.IsLegalPointBuy(out _));
        }

        [Fact]
        public void OverspendingIsRefusedAndSaysBySoMuch()
        {
            var scores = new AbilityScores(15, 15, 15, 15, 15, 15);

            Assert.False(scores.IsLegalPointBuy(out string problem));
            Assert.Contains("54", problem);
        }

        [Fact]
        public void AScoreOffTheLadderIsRefusedByName()
        {
            var scores = new AbilityScores(16, 8, 8, 8, 8, 8);

            Assert.False(scores.IsLegalPointBuy(out string problem));
            Assert.Contains("str", problem);
        }

        [Fact]
        public void RaisingStopsAtTwenty()
        {
            var scores = new AbilityScores(19, 10, 10, 10, 10, 10);

            Assert.Equal(1, scores.Raise(Ability.Strength, 2));
            Assert.Equal(20, scores.Base(Ability.Strength));
        }

        [Fact]
        public void AnUntilRestShiftMovesTheModifierAndARestTakesItBack()
        {
            var scores = new AbilityScores(10, 10, 10, 10, 14, 10);

            Assert.Equal(2, scores.Modifier(Ability.Wisdom));

            scores.ShiftUntilRest(Ability.Wisdom, -1);

            Assert.Equal(13, scores.Score(Ability.Wisdom));
            Assert.Equal(1, scores.Modifier(Ability.Wisdom));
            Assert.Equal(14, scores.Base(Ability.Wisdom));

            scores.Rested();

            Assert.Equal(2, scores.Modifier(Ability.Wisdom));
        }

        [Fact]
        public void EveryAbilityHasAnIdThatParsesBack()
        {
            foreach (Ability ability in Abilities.All)
            {
                Assert.True(Abilities.TryParse(ability.Id(), out Ability read));
                Assert.Equal(ability, read);
            }
        }

        [Fact]
        public void TheSixAreDistinctAndInSheetOrder() =>
            Assert.Equal(6, Abilities.All.Select(a => a.Id()).Distinct().Count());
    }

    public class ProficiencyTests
    {
        [Theory]
        [InlineData(1, 2)]
        [InlineData(4, 2)]
        [InlineData(5, 3)]
        [InlineData(8, 3)]
        [InlineData(9, 4)]
        [InlineData(13, 5)]
        [InlineData(17, 6)]
        [InlineData(20, 6)]
        public void BonusScalesTwoToSix(int level, int bonus) =>
            Assert.Equal(bonus, Proficiency.Bonus(level));

        [Fact]
        public void ALevelOffTheEndsIsClampedRatherThanThrown()
        {
            Assert.Equal(2, Proficiency.Bonus(0));
            Assert.Equal(6, Proficiency.Bonus(99));
        }

        [Fact]
        public void ExpertiseDoublesTheBonusAndUntrainedAddsNothing()
        {
            Assert.Equal(0, Proficiency.Applied(5, Training.Untrained));
            Assert.Equal(3, Proficiency.Applied(5, Training.Proficient));
            Assert.Equal(6, Proficiency.Applied(5, Training.Expert));
        }
    }

    public class SkillTests
    {
        [Fact]
        public void ThereAreEighteenOfThem() => Assert.Equal(Skills.Count, Skills.All.Count);

        [Fact]
        public void NoneIsNotOneOfThem() => Assert.DoesNotContain(Skill.None, Skills.All);

        [Fact]
        public void EverySkillHasAGoverningAbilityAndAnIdThatParsesBack()
        {
            foreach (Skill skill in Skills.All)
            {
                Assert.Contains(skill.Governs(), Abilities.All);

                Assert.True(Skills.TryParse(skill.Id(), out Skill read));
                Assert.Equal(skill, read);
            }
        }

        [Fact]
        public void IdsAreSnakeCaseAndUnique()
        {
            Assert.Equal("animal_handling", Skill.AnimalHandling.Id());
            Assert.Equal("sleight_of_hand", Skill.SleightOfHand.Id());

            Assert.Equal(Skills.Count, Skills.All.Select(s => s.Id()).Distinct().Count());
        }

        [Fact]
        public void TheGoverningAbilitiesAreTheSrdOnes()
        {
            Assert.Equal(Ability.Dexterity, Skill.Stealth.Governs());
            Assert.Equal(Ability.Wisdom, Skill.Perception.Governs());
            Assert.Equal(Ability.Intelligence, Skill.Investigation.Governs());
            Assert.Equal(Ability.Charisma, Skill.Intimidation.Governs());
            Assert.Equal(Ability.Strength, Skill.Athletics.Governs());
        }
    }
}
