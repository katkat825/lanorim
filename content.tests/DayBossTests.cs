using Content.Dialogue;
using Core.Characters;
using Core.Combat;

namespace Content.Tests
{
    // Tier 2.5 of the 2026-09-24 run: the fight tells the day what it was fighting, so a fallen
    // boss is a night's topic again (HARVEST_REPORT.md section 2)
    public class DayBossTests
    {
        static Actor Hero(int level)
        {
            var hero = new Actor("hero", level, new AbilityScores(), Allegiance.Hero);
            hero.SetHealth(new Health(30));
            return hero;
        }

        static Actor Foe(string id)
        {
            var foe = new Actor(id, 1, new AbilityScores());
            foe.SetHealth(new Health(10));
            return foe;
        }

        [Fact]
        public void AFoeOfTheHerosLevelOrAboveIsABoss()
        {
            Actor hero = Hero(3);
            Actor goblin = Foe("goblin");
            Actor ogre = Foe("ogre");

            var day = new Day(hero);
            day.Knows(a => a == ogre ? (3.0, false) : (0.25, false));

            day.Downed(goblin);
            Assert.False(day.KilledABoss);

            day.Downed(ogre);
            Assert.True(day.KilledABoss);
            Assert.Equal(Topic.Boss, day.About());

            day.Slept();
            Assert.False(day.KilledABoss);
        }

        [Fact]
        public void ACampaignTagMakesABossWhateverItsChallenge()
        {
            Actor hero = Hero(10);
            Actor captain = Foe("captain");

            var day = new Day(hero);
            day.Knows(_ => (1.0, true));

            day.Downed(captain);
            Assert.Equal(Topic.Boss, day.About());
        }

        [Fact]
        public void WithoutBeingToldNothingIsABoss()
        {
            var day = new Day(Hero(1));
            day.Downed(Foe("dragon"));

            Assert.NotEqual(Topic.Boss, day.About());
        }
    }
}
