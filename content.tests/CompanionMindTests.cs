using System.Collections.Generic;
using System.Linq;
using Content.Companions;
using Content.Dialogue;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Content.Tests
{
    // Tier 2.6 of the 2026-09-24 run: the companion's non-visual logic, ported from the old build
    public class CompanionMindTests
    {
        static Speaking Wolf() =>
            new BarkBank("wolf", new Dictionary<Bark, int>
            {
                [Bark.Maxed] = 3, [Bark.Trouble] = 2, [Bark.Down] = 1, [Bark.Victory] = 2,
            }).Open(new SeededRng(4));

        static Attempt Attack(int natural) =>
            new Attempt(RollKind.Attack, D20Roll.Fixed(natural, 5), 15);

        [Fact]
        public void TheSrdMomentsAreTheOldBarks()
        {
            Assert.Equal(Bark.Maxed, TableCues.For(Attack(20)));
            Assert.Equal(Bark.Snag, TableCues.For(Attack(1)));
            Assert.Null(TableCues.For(Attack(12)));
            Assert.Equal(Bark.Trouble, TableCues.For(new Attempt(RollKind.Check, D20Roll.Fixed(1), 10)));
            Assert.Equal(Bark.Perfect, TableCues.For(new Attempt(RollKind.Save, D20Roll.Fixed(20), 10)));
            Assert.Equal(Bark.Nerve, TableCues.For(new Attempt(RollKind.Death, D20Roll.Fixed(8), 10)));
        }

        [Fact]
        public void ACriticalIsALineAndAPleasedWolfThatSettles()
        {
            var mind = new CompanionMind(Wolf());

            string said = mind.Sees(Attack(20));

            Assert.Equal(DialogueKeys.Bark("wolf", Bark.Maxed, 1).Split('.')[0], said.Split('.')[0]);
            Assert.Contains("wolf", said);
            Assert.Equal(Mood.Pleased, mind.Mood);

            mind.Tick(TableCues.HoldFor(Mood.Pleased) + 0.1);
            Assert.Equal(Mood.Calm, mind.Mood);
        }

        [Fact]
        public void QuietOutranksEverythingAndSilenceIsAnAnswer()
        {
            var mind = new CompanionMind(Wolf());

            mind.Say(Bark.Down);
            mind.Sees(Attack(20));

            Assert.Equal(Mood.Quiet, mind.Mood);

            // nothing in the bank for a Snag: no line, still a reaction
            Assert.Null(mind.Say(Bark.Snag));
        }

        [Fact]
        public void EveryLineIsHeardBeforeAnyIsHeardTwice()
        {
            var mind = new CompanionMind(Wolf());

            List<string> three = Enumerable.Range(0, 3).Select(_ => mind.Say(Bark.Maxed)).ToList();

            Assert.Equal(3, three.Distinct().Count());
        }

        [Fact]
        public void IdlesAreDealtNotLooped()
        {
            var idling = new Idling(5, new SeededRng(9));
            var seen = new List<int> { idling.Idle };

            while (seen.Count < 5)
                if (idling.Tick(10)) seen.Add(idling.Idle);

            Assert.Equal(5, seen.Distinct().Count());
        }

        [Fact]
        public void ItWatchesTheFightItIsShown()
        {
            var hero = new Actor("hero", 1, new AbilityScores(), Allegiance.Hero);
            var mind = new CompanionMind(Wolf(), hero);

            mind.Downed(hero);
            Assert.Equal(Mood.Quiet, mind.Mood);

            mind.Settle();
            mind.Ended(Outcome.HeroesWon);
            Assert.Equal(Mood.Pleased, mind.Mood);
        }
    }
}
