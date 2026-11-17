using System.Linq;
using Content.Combat;
using Content.Companions;
using Content.Dialogue;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;
using Xunit;

namespace Content.Tests
{
    // cc_task_nt Parts 1 and 2 (Kathleen, 2026-11-16): a story's bark always plays and still starts the cooldown; a bout
    // lost by a knockout reads as one in the log and draws a line from the companion's Knock-Out pool
    public partial class BarkTests
    {
        // --- a story bark always plays ------------------------------------------------------------------

        [Fact]
        public void AStoryBarkStraightAfterAVictoryLineIsSaidAndStartsTheCooldown()
        {
            var mind = new CompanionMind(Wolf(), Hero(), new Chatter(new SeededRng(1)));

            Assert.NotNull(mind.Say(Bark.Victory));
            mind.Tick(1);

            // Camp is lower than Victory and on the cooldown: an ambient one is dropped, the story's is said
            Assert.Equal("camp", PoolOf(mind.Hears(Bark.Camp)));

            mind.Tick(1);
            Assert.Null(mind.Say(Bark.Camp));
        }

        [Fact]
        public void SomeNeverThinsAStoryBarkAndOffStillSilencesIt()
        {
            var some = new Chatter(new SeededRng(7), () => CompanionChatter.Some);

            for (int i = 0; i < 200; i++) Assert.True(some.Allows(Bark.Victory, i * 0.5, fromStory: true));

            var off = new CompanionMind(Wolf(), Hero(), new Chatter(new SeededRng(1), () => CompanionChatter.Off));
            Assert.Null(off.Hears(Bark.Camp));
            Assert.Null(off.Hears(Bark.KnockOut));
        }

        // --- the knockout -------------------------------------------------------------------------------

        const string Ring = @"
+-+-+-+-+-+
|@ . . . .|
+ + + + + +
|. . . . .|
+-+-+-+-+-+";

        // the hero already down as the fight starts, so their first turn is the death save; the scripted d20 throws the
        // initiatives (20 and 1), then `save` for every roll after
        static (FightLog log, CompanionMind mind) DownedIn(Defeat defeat, int save)
        {
            Assert.True(MapReader.TryRead(Ring, out MapLayout map, out string problem), problem);

            Actor hero = Hero();
            var log = new FightLog();
            var mind = new CompanionMind(Wolf(), hero, new Chatter(new SeededRng(1)));

            var fight = new Encounter(new StandardResolver(new ScriptedRng(new[] { 20, 1 }.Concat(Enumerable.Repeat(save, 50)).ToArray())),
                                      new Battlefield(map), new Observers(log, mind)) { Defeat = defeat };

            fight.Enlist(hero, new Cell(0, 0));
            var goblin = new Actor("goblin", 1, new AbilityScores());
            goblin.SetHealth(new Health(7));
            fight.Enlist(goblin, new Cell(4, 1));

            hero.Suffer(100, DamageType.Slashing);
            Assert.True(hero.IsDown);

            fight.Begin();

            // round the turns until the hero's comes up: its start is the death save
            for (int turn = 0; turn < 4 && !log.Lines.Any(l => l.Key.Contains("death_save") || l.Key.Contains("knocked_out")); turn++)
            {
                if (fight.Current != null) fight.EndTurn();
                if (fight.Next() == null) break;
            }

            return (log, mind);
        }

        static bool Logged(FightLog log, string what) => log.Lines.Any(l => l.Key == $"ui.log.{what}.name");

        [Fact]
        public void AFailedSaveInABoutThatContinuesIsAKnockoutWithAKnockOutLine()
        {
            (FightLog log, CompanionMind mind) = DownedIn(Defeat.Continues, 3);

            Assert.True(Logged(log, "knocked_out"));
            Assert.False(Logged(log, "death_save_dead"));
            Assert.Equal("knock_out", PoolOf(mind.Said.Last()));
        }

        [Fact]
        public void ANormalFightsFailedSaveIsStillTheEndAndNoKnockOutLine()
        {
            (FightLog log, CompanionMind mind) = DownedIn(Defeat.Death, 3);

            Assert.True(Logged(log, "death_save_dead"));
            Assert.False(Logged(log, "knocked_out"));
            Assert.DoesNotContain(mind.Said, k => PoolOf(k) == "knock_out");
        }

        [Fact]
        public void AMadeSaveOrAYieldIsNoKnockout()
        {
            (FightLog log, CompanionMind mind) = DownedIn(Defeat.Continues, 15);

            Assert.True(Logged(log, "death_save_up"));
            Assert.DoesNotContain(mind.Said, k => PoolOf(k) == "knock_out");

            // a yield ends the bout with no roll and no knockout (Encounter.Yield)
            var yielded = new CompanionMind(Wolf(), Hero(), new Chatter(new SeededRng(1)));
            yielded.Ended(Outcome.Yielded);
            Assert.Empty(yielded.Said);
        }
    }
}
