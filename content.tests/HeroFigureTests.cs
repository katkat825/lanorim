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
    // WHICH OF ITS CLASS'S TWO FIGURES THE HERO'S MINI IS (cc_task_h Part 2): picked at creation, saved, and the first
    // in a save from before it. A look only - the game (HeroMinis) turns the number into a body and a head
    public class HeroFigureTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Fighter(int figure)
        {
            CharacterClass fighter = Srd.Class("fighter");

            var hero = new Hero("Pell", fighter, Srd.Kind("human"), Srd.Background("soldier"),
                                Creation.Creation.Standard(fighter), 1) { Figure = figure };

            hero.Build(new Dictionary<Ability, int>(), new Skill[0], null, Srd.Items, null);
            return hero;
        }

        static (Hero Back, string Json) RoundTrip(Hero hero)
        {
            string json = SaveWriter.Write(new SaveGame { Campaign = "ash_yard", Hero = HeroSaves.Capture(hero) });
            Read<SaveGame> read = SaveReader.Parse(json);

            Assert.True(read.Ok, string.Join("\n", read.Problems));

            Hero back = HeroSaves.Restore(read.Value.Hero, Srd, out IReadOnlyList<ContentProblem> problems);
            Assert.Empty(problems);

            return (back, json);
        }

        [Fact]
        public void TheSecondFigureIsSavedAndComesBack()
        {
            (Hero back, string json) = RoundTrip(Fighter(1));

            Assert.Equal(1, back.Figure);
            Assert.Contains("\"figure\"", json);
        }

        // the first figure isn't written, so a save from before figures reads as the first and writes the same bytes
        [Fact]
        public void TheFirstFigureIsTheDefaultAndAnOldSaveHasIt()
        {
            (Hero back, string json) = RoundTrip(Fighter(0));

            Assert.Equal(0, back.Figure);
            Assert.DoesNotContain("\"figure\"", json);
        }

        [Fact]
        public void CreationHandsItsFigureToTheHero()
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);

            Assert.Equal(0, making.Figure);

            making.Pick(Srd.Class("fighter"));
            making.Pick(Srd.Kind("human"));
            making.Pick(Srd.Background("soldier"));
            making.SuggestTraits();

            foreach (Skill skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList())
                making.Train(skill);

            making.Call("Pell");
            making.Figure = 1;

            Hero hero = making.Finish();

            Assert.True(hero != null, string.Join("; ", making.Problems));
            Assert.Equal(1, hero.Figure);
        }
    }
}
