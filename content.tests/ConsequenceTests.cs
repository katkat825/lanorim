using System.Linq;
using Content.Inventory;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Rules;

namespace Content.Tests
{
    // the keeper delta: the check passes or fails on the numbers alone, and a natural 1 or 20
    // changes something regardless (docs/updated_decisions.md).
    public class ConsequenceTests
    {
        static readonly ConsequencePool Pool = Library.Srd().Consequences;

        static Actor Hero()
        {
            // int 14 and trained: Investigation is +5 at level 5, which is what the DCs below
            // are written against
            var hero = new Actor("hero", 5, new AbilityScores(12, 14, 12, 14, 14, 10),
                                 Allegiance.Hero);

            hero.SetHealth(new Health(40));
            hero.Train(Skill.Investigation);

            return hero;
        }

        [Fact]
        public void ThePoolLoadsAndHasBothSides()
        {
            Assert.True(Pool.Has(Polarity.Bane));
            Assert.True(Pool.Has(Polarity.Boon));
            Assert.True(Pool.All.Count >= 10);
        }

        [Fact]
        public void TheTwoExamplesFromTheDesignDocAreInIt()
        {
            // updated_decisions.md writes both of these out by hand
            Consequence toe = Pool.All.First(c => c.Id == "stubbed_toe");

            Assert.Equal(Polarity.Bane, toe.Polarity);
            Assert.Equal(ConsequenceKind.Health, toe.Kind);
            Assert.Equal(DiceRoll.Parse("1d4"), toe.Amount);

            Consequence pouch = Pool.All.First(c => c.Id == "a_pouch_on_the_floor");

            Assert.Equal(Polarity.Boon, pouch.Polarity);
            Assert.Equal(ConsequenceKind.Gold, pouch.Kind);
            Assert.True(pouch.ScalesWithLevel);
        }

        [Fact]
        public void ANaturalOneOnACheckYourModifiersStillPassDrawsABane()
        {
            Actor hero = Hero();

            // DC 5, +5 from the sheet: a natural 1 totals 6, and 6 beats 5
            var resolver = new StandardResolver(new ScriptedRng(1));

            Attempt attempt = Checks.Check(resolver, hero, Skill.Investigation, 5);

            Assert.True(attempt.Succeeded);
            Assert.True(attempt.DrawsConsequence);

            Consequence drawn = Pool.DrawFor(new SeededRng(7), attempt);

            Assert.NotNull(drawn);
            Assert.Equal(Polarity.Bane, drawn.Polarity);
        }

        [Fact]
        public void ANaturalTwentyThatStillFailsDrawsABoon()
        {
            Actor hero = Hero();

            var resolver = new StandardResolver(new ScriptedRng(20));

            Attempt attempt = Checks.Check(resolver, hero, Skill.Investigation, 40);

            Assert.False(attempt.Succeeded);
            Assert.True(attempt.DrawsConsequence);

            Assert.Equal(Polarity.Boon, Pool.DrawFor(new SeededRng(7), attempt).Polarity);
        }

        [Fact]
        public void AnOrdinaryRollDrawsNothing()
        {
            Actor hero = Hero();

            var resolver = new StandardResolver(new ScriptedRng(11));

            Attempt attempt = Checks.Check(resolver, hero, Skill.Investigation, 10);

            Assert.Null(Pool.DrawFor(new SeededRng(7), attempt));
        }

        [Fact]
        public void ASavingThrowDoesNotDrawFromThePool()
        {
            // only checks and critical hits do (decisions_checklist.md section 1)
            Actor hero = Hero();

            var resolver = new StandardResolver(new ScriptedRng(1));

            Attempt save = Checks.Save(resolver, hero, Ability.Wisdom, 10);

            Assert.False(save.DrawsConsequence);
        }

        [Fact]
        public void YouCanTakeDamageOutsideCombat()
        {
            // "this also means there is the ability to take damage outside of combat" -
            // updated_decisions.md, in as many words
            Actor hero = Hero();

            Consequence toe = Pool.All.First(c => c.Id == "stubbed_toe");

            var visit = Consequences.Befall(toe, new StandardResolver(new ScriptedRng(3)), hero);

            Assert.Equal(3, visit.Amount);
            Assert.Equal(37, hero.Health.Current);
        }

        [Fact]
        public void AShiftLastsUntilYouRest()
        {
            Actor hero = Hero();

            Consequence tired = Pool.All.First(c => c.Id == "that_took_it_out_of_you");

            Assert.Equal(2, hero.AbilityModifier(Ability.Wisdom));

            Consequences.Befall(tired, new StandardResolver(new ScriptedRng(1)), hero);

            Assert.Equal(1, hero.AbilityModifier(Ability.Wisdom));
            Assert.Equal(14, hero.Scores.Base(Ability.Wisdom));

            hero.ShortRest();

            Assert.Equal(2, hero.AbilityModifier(Ability.Wisdom));
        }

        [Fact]
        public void APouchIsFivePlusYourLevel()
        {
            Actor hero = Hero();
            var pack = new Pack();

            Consequence pouch = Pool.All.First(c => c.Id == "a_pouch_on_the_floor");

            var visit = Consequences.Befall(pouch, new StandardResolver(new ScriptedRng(1)),
                                            hero, pack);

            Assert.Equal(10, visit.Amount); // 5, plus level 5
            Assert.Equal(10, pack.Gold);
        }

        [Fact]
        public void EveryConsequenceHasALineTheNarratorCanRead()
        {
            var english = Locale.Localizer(
                System.IO.File.Exists("game.csv") ? System.IO.File.ReadAllText("game.csv") : "");

            foreach (Consequence one in Pool.All)
            {
                Assert.True(Core.Localization.KeyConventions.IsWellFormed(one.LineKey));
                Assert.NotEqual(one.LineKey, english.Get(one.LineKey));
            }
        }

        [Fact]
        public void TheDrawIsWeightedAndRepeatable()
        {
            var counts = new System.Collections.Generic.Dictionary<string, int>();

            var rng = new SeededRng(99);

            for (int i = 0; i < 2000; i++)
            {
                Consequence drawn = Pool.Draw(rng, Polarity.Bane);

                counts[drawn.Id] = counts.TryGetValue(drawn.Id, out int had) ? had + 1 : 1;
            }

            // every bane comes up, and the weight-3 ones come up oftener than the weight-1 one
            Assert.Equal(Pool.Of(Polarity.Bane).Count(), counts.Count);
            Assert.True(counts["stubbed_toe"] > counts["something_in_the_air"]);
        }

        [Fact]
        public void ACampaignCanAddItsOwnWithoutTouchingTheEngines()
        {
            var mine = new Consequence("the_gallery_watches", Polarity.Bane,
                                       ConsequenceKind.Condition,
                                       condition: Condition.Frightened, weight: 50);

            ConsequencePool wider = Pool.With(new[] { mine });

            Assert.Equal(Pool.All.Count + 1, wider.All.Count);
            Assert.Contains(wider.All, c => c.Id == "the_gallery_watches");

            // and the engine's own pool is untouched
            Assert.DoesNotContain(Pool.All, c => c.Id == "the_gallery_watches");
        }
    }
}
