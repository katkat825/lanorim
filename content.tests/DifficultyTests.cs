using System.Linq;
using Content.Combat;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Content.SoloPlay;
using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Space;
using Xunit;

namespace Content.Tests
{
    // 3.3 (cc_task_h): STORY, STANDARD AND HARD. A player setting, saved with the others; only the enemies change -
    // their hit points and their damage, rounded up and never under 1 - and Standard is the game exactly as it was
    [Collection(SoloRulesCollection.Name)]
    public class DifficultyTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Fighter()
        {
            Content.Classes.CharacterClass fighter = Srd.Class("fighter");
            var hero = new Hero("Tess", fighter, Srd.Kind("human"), Srd.Background("soldier"),
                                Creation.Creation.Standard(fighter), 1);
            hero.Build(null, fighter.SkillChoices.Take(2).ToList(), null, Srd.Items, null);
            return hero;
        }

        static Battle Fight(Hero hero, PlayDifficulty difficulty)
        {
            PlayDifficulty was = SoloRules.Difficulty;

            try
            {
                SoloRules.Difficulty = difficulty;

                var map = new Content.Maps.MapDraft(6, 4);
                map.Paint(new Cell(0, 0), new Cell(5, 3), Tile.Floor);
                map.PlaceStart(new Cell(1, 1));

                return Battle.Set(Srd, map.Layout(), hero,
                                  new[] { new Battle.Foe(Srd.Bestiary.Find("bandit"), new Cell(3, 1)) },
                                  new StandardResolver(new SeededRng(1)));
            }
            finally { SoloRules.Difficulty = was; }
        }

        static Actor Foe(Battle battle, Hero hero) =>
            battle.Fight.Order.Select(t => t.Actor).First(a => !ReferenceEquals(a, hero.Actor));

        [Theory]
        [InlineData(PlayDifficulty.Story, 75, 75)]
        [InlineData(PlayDifficulty.Standard, 100, 100)]
        [InlineData(PlayDifficulty.Hard, 125, 100)]
        public void OnlyTheEnemiesChange(PlayDifficulty difficulty, int hitPoints, int damage)
        {
            Hero hero = Fighter();
            int heroMaximum = hero.Actor.Health.Maximum;
            int statblock = Srd.Bestiary.Find("bandit").HitPoints;

            Battle battle = Fight(hero, difficulty);
            Actor bandit = Foe(battle, hero);

            Assert.Equal(Core.Rules.Percent.Of(statblock, hitPoints), bandit.Health.Maximum);
            Assert.Equal(damage, bandit.DamagePercent);

            Assert.Equal(heroMaximum, hero.Actor.Health.Maximum);
            Assert.Equal(100, hero.Actor.DamagePercent);
        }

        [Fact]
        public void TheEnemysShareIsRoundedUpAndNeverUnderOne()
        {
            var hero = Fighter();
            var bandit = Srd.Bestiary.Find("bandit").Spawn();
            bandit.DamagePercent = 75;
            int had = hero.Actor.Health.Current;

            hero.Actor.Suffer(1, DamageType.Slashing, bandit);
            Assert.Equal(had - 1, hero.Actor.Health.Current);

            hero.Actor.Suffer(6, DamageType.Slashing, bandit);
            Assert.Equal(had - 1 - 5, hero.Actor.Health.Current);

            // and the hero's own blows land in full whatever the setting
            bandit.Suffer(4, DamageType.Slashing, hero.Actor);
            Assert.Equal(bandit.Health.Maximum - 4, bandit.Health.Current);
        }

        [Fact]
        public void ItsSavedWithTheOtherSettingsAndStandardIsTheDefault()
        {
            Assert.Equal(PlayDifficulty.Standard, new GameSettings().Difficulty);
            Assert.Equal(PlayDifficulty.Standard, GameSettings.Read("{}", out _).Difficulty);

            var story = new GameSettings { Difficulty = PlayDifficulty.Story };
            Assert.Equal(PlayDifficulty.Story, GameSettings.Read(story.Write(), out var problems).Difficulty);
            Assert.Empty(problems);

            GameSettings.Read("{\"difficulty\": \"nightmare\"}", out var bad);
            Assert.Single(bad);
        }
    }
}
