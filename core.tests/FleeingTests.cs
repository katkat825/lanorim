using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;

namespace Core.Tests
{
    public class FleeingTests
    {
        // the middle of the left side is open: the outline has a gap at (0, 1)
        const string Gap = @"
+-+-+-+-+-+-+-+
|@ . . . . . .|
+ + + + + + + +
 . . . . . . .|
+ + + + + + + +
|. . . . . . .|
+-+-+-+-+-+-+-+";

        static Encounter Start(string map, out Actor hero, out Actor goblin, params int[] rolls)
        {
            var field = new Battlefield(Rooms.Read(map));
            var fight = new Encounter(new StandardResolver(new ScriptedRng(rolls)), field);

            hero = Combatants.Hero();
            goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(6, 1));
            fight.Begin();

            return fight;
        }

        [Fact]
        public void AGapInTheOutlineIsAWayOut()
        {
            var field = new Battlefield(Rooms.Read(Gap));

            Assert.Equal(new[] { new Cell(0, 1) }, field.Exits);
        }

        [Fact]
        public void AClosedRoomHasNoWayOut()
        {
            Assert.Empty(new Battlefield(Rooms.Read(Rooms.Open)).Exits);
        }

        [Fact]
        public void StandingOnTheWayOutTheHeroCanFleeAndTheFightEndsAsFled()
        {
            Encounter fight = Start(Gap, out Actor hero, out _, 10, 1);

            Turn turn = fight.Next();
            Assert.Same(hero, turn.Actor);

            Assert.False(fight.CanFlee(turn));

            fight.Walk(turn, new Cell(0, 1));

            Assert.True(fight.Flee(turn));
            Assert.Equal(Outcome.Fled, fight.Outcome);
            Assert.Null(fight.Next());
        }

        [Fact]
        public void AwayFromTheEdgeThereIsNoFleeing()
        {
            Encounter fight = Start(Gap, out _, out _, 10, 1);

            Turn turn = fight.Next();

            Assert.False(fight.Flee(turn));
            Assert.Equal(Outcome.Open, fight.Outcome);
        }

        [Fact]
        public void AMonsterDoesNotFleeByTheRules()
        {
            Encounter fight = Start(Gap, out _, out Actor goblin, 1, 20);

            Turn turn = fight.Next();
            Assert.Same(goblin, turn.Actor);

            fight.Walk(turn, new Cell(6, 0));

            Assert.False(fight.CanFlee(turn));
        }
    }
}
