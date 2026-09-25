using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Xunit;

namespace Content.Tests
{
    // THE PLAYER CHOOSES THE ABILITY SCORE IMPROVEMENT (decisions_checklist.md section 1,
    // 2026-09-24): +2 to one score or +1 to two, never past 20, and nothing spent on the player's
    // behalf. The class's priority survives as a suggestion the screen pre-fills and the sim spends.
    public class ImprovementTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Fighter(int level, int strength = 15, int constitution = 13)
        {
            CharacterClass fighter = Srd.Class("fighter");
            AbilityScores scores = Creation.Creation.Standard(fighter);
            scores.SetBase(Ability.Strength, strength);
            scores.SetBase(Ability.Constitution, constitution);

            var hero = new Hero("Brenna", fighter, Srd.Kind("human"), Srd.Background("soldier"),
                                scores, level);

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Strength] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Athletics, Skill.Perception });

            return hero;
        }

        [Fact]
        public void ReachingAnImprovementLevelLeavesItPendingAndSpendsNothing()
        {
            Hero three = Fighter(3);
            Hero four = Fighter(4);

            Assert.Equal(0, three.PendingImprovements);
            Assert.Equal(1, four.PendingImprovements);
            Assert.Equal(three.Actor.Scores.Base(Ability.Strength),
                         four.Actor.Scores.Base(Ability.Strength));

            Assert.Equal(1, SheetView.Of(four).PendingImprovements);
        }

        [Fact]
        public void TwoToOne()
        {
            Hero hero = Fighter(4);
            int strength = hero.Actor.Scores.Base(Ability.Strength);

            Assert.True(hero.Improve(AbilityImprovement.Two(Ability.Strength)));

            Assert.Equal(strength + 2, hero.Actor.Scores.Base(Ability.Strength));
            Assert.Equal(0, hero.PendingImprovements);
        }

        [Fact]
        public void OneEachToTwo()
        {
            Hero hero = Fighter(4);
            int dex = hero.Actor.Scores.Base(Ability.Dexterity);
            int wis = hero.Actor.Scores.Base(Ability.Wisdom);

            Assert.True(hero.Improve(AbilityImprovement.OneEach(Ability.Dexterity, Ability.Wisdom)));

            Assert.Equal(dex + 1, hero.Actor.Scores.Base(Ability.Dexterity));
            Assert.Equal(wis + 1, hero.Actor.Scores.Base(Ability.Wisdom));
        }

        [Fact]
        public void TwoThatWouldPassTwentyIsRefusedWithAReason()
        {
            // 17 + 2 from the background = 19
            Hero hero = Fighter(4, strength: 17);
            Assert.Equal(19, hero.Actor.Scores.Base(Ability.Strength));

            Assert.False(hero.Improve(AbilityImprovement.Two(Ability.Strength), out string why));

            Assert.Equal(ImprovementRefusals.Key(ImprovementRefusals.OverTwenty), why);
            Assert.Equal(19, hero.Actor.Scores.Base(Ability.Strength));
            Assert.Equal(1, hero.PendingImprovements);
        }

        [Fact]
        public void OneOnAScoreAtTwentyIsRefused()
        {
            Hero hero = Fighter(8, strength: 16);
            Assert.True(hero.Improve(AbilityImprovement.Two(Ability.Strength)));
            Assert.Equal(20, hero.Actor.Scores.Base(Ability.Strength));

            Assert.False(hero.Improve(AbilityImprovement.OneEach(Ability.Strength, Ability.Dexterity),
                                      out string why));
            Assert.Equal(ImprovementRefusals.Key(ImprovementRefusals.OverTwenty), why);
        }

        [Fact]
        public void AThirdPointIsRefused()
        {
            // one improvement is two points, and there is only the one to spend
            Hero hero = Fighter(4);

            Assert.True(hero.Improve(AbilityImprovement.OneEach(Ability.Dexterity, Ability.Wisdom)));
            Assert.False(hero.Improve(AbilityImprovement.OneEach(Ability.Charisma, Ability.Intelligence),
                                      out string why));

            Assert.Equal(ImprovementRefusals.Key(ImprovementRefusals.NonePending), why);

            // and +1 twice to the same score is not a "+1 to two"
            Hero other = Fighter(4);
            Assert.False(other.Improve(AbilityImprovement.OneEach(Ability.Wisdom, Ability.Wisdom),
                                       out string twice));
            Assert.Equal(ImprovementRefusals.Key(ImprovementRefusals.SameTwice), twice);
        }

        [Fact]
        public void LevellingThroughTwoImprovementLevelsLeavesTwoPending()
        {
            Hero hero = Fighter(3);
            int strength = hero.Actor.Scores.Base(Ability.Strength);

            // a Fighter's improvements come at 4 and 6 (and 8, 12, 14, 16, 19 - SRD 5.2.1)
            hero.LevelTo(7);

            Assert.Equal(2, hero.PendingImprovements);
            Assert.Equal(strength, hero.Actor.Scores.Base(Ability.Strength));
        }

        [Fact]
        public void HitPointsRiseWhenConstitutionGoesUpAndTheDamageStays()
        {
            // Con 13 + 1 = 14; +2 makes 16, a modifier one higher for each of 4 levels
            Hero hero = Fighter(4);
            hero.Actor.Suffer(5, DamageType.Slashing);

            int max = hero.Actor.Health.Maximum;
            int current = hero.Actor.Health.Current;

            Assert.True(hero.Improve(AbilityImprovement.Two(Ability.Constitution)));

            Assert.Equal(max + 4, hero.Actor.Health.Maximum);
            Assert.Equal(current + 4, hero.Actor.Health.Current);
        }

        [Fact]
        public void TheSaveKeepsTheChoicesAndThePendingCount()
        {
            Hero hero = Fighter(7);
            hero.Improve(AbilityImprovement.OneEach(Ability.Dexterity, Ability.Constitution));

            var save = new SaveGame { Hero = HeroSaves.Capture(hero) };
            string text = SaveWriter.Write(save);

            Assert.Contains("\"improvements\"", text);
            Assert.Matches(@"\[\s*""dex"",\s*""con""\s*\]", text);

            Read<SaveGame> read = SaveReader.Parse(text);
            Hero back = HeroSaves.Restore(read.Value.Hero, Srd, out IReadOnlyList<ContentProblem> problems);

            Assert.Empty(problems);
            Assert.Equal(hero.Improvements, back.Improvements);
            Assert.Equal(1, back.PendingImprovements);
            Assert.Equal(hero.Actor.Scores.Base(Ability.Dexterity), back.Actor.Scores.Base(Ability.Dexterity));
            Assert.Equal(hero.Actor.Health.Maximum, back.Actor.Health.Maximum);
        }

        [Fact]
        public void AnOldSaveLoadsWithItsImprovementsPendingAndACaution()
        {
            Hero hero = Fighter(4);

            var save = new SaveGame { Hero = HeroSaves.Capture(hero) };
            string text = SaveWriter.Write(save);

            // a save from before this run has neither field
            text = System.Text.RegularExpressions.Regex.Replace(
                text, "\"improvements\":\\s*\\[[^\\]]*\\],?\\s*", "");
            text = System.Text.RegularExpressions.Regex.Replace(
                text, "\"improvements_pending\":\\s*\\d+,?\\s*", "");

            Read<SaveGame> read = SaveReader.Parse(text);
            Assert.True(read.Ok, string.Join("\n", read.Problems));

            Hero back = HeroSaves.Restore(read.Value.Hero, Srd, out IReadOnlyList<ContentProblem> problems);

            Assert.Equal(1, back.PendingImprovements);
            Assert.Contains(problems, p => p.IsACaution && p.What.Contains("waiting to be spent"));
            Assert.Equal(hero.Actor.Scores.Base(Ability.Strength), back.Actor.Scores.Base(Ability.Strength));
        }

        [Fact]
        public void TheSimAndEveryHeroNobodyChoosesForSpendTheSuggestion()
        {
            Hero hero = Fighter(7);
            int strength = hero.Actor.Scores.Base(Ability.Strength);

            Assert.Equal(AbilityImprovement.Two(Ability.Strength), hero.Suggested());
            Assert.Equal(2, hero.ImproveAsSuggested());

            // +2 to Strength (17 to 19), and then Strength has no room for +2: the next priority
            Assert.Equal(strength + 2, hero.Actor.Scores.Base(Ability.Strength));
            Assert.Equal(0, hero.PendingImprovements);
        }

        [Fact]
        public void CreationAboveLevelFourStopsOnTheImprovementsStep()
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);
            making.StartAt(5);
            making.Pick(Srd.Class("fighter"));
            making.Pick(Srd.Kind("human"));
            making.Pick(Srd.Background("soldier"));

            Assert.Equal(Creation.Step.Improvements, making.Next);

            AbilityImprovement suggestion = making.SuggestedImprovement();
            Assert.True(making.Improve(suggestion));

            Assert.NotEqual(Creation.Step.Improvements, making.Next);

            foreach (Skill skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList())
                making.Train(skill);

            making.Call("Brenna");
            Assert.True(making.Ready);

            Hero hero = making.Finish();
            Assert.Equal(new[] { suggestion }, hero.Improvements);
            Assert.Equal(0, hero.PendingImprovements);
        }
    }
}
