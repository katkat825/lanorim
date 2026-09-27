using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Space;

namespace Core.Tests
{
    public class BattlefieldTests
    {
        static Battlefield Open() => new Battlefield(Rooms.Read(Rooms.Open));

        [Fact]
        public void ADiagonalIsOneSquare()
        {
            Assert.Equal(1, Battlefield.Distance(new Cell(0, 0), new Cell(1, 1)));
            Assert.Equal(3, Battlefield.Distance(new Cell(0, 0), new Cell(3, 2)));
        }

        [Fact]
        public void APieceCannotStandOnAnother()
        {
            Battlefield field = Open();
            Actor a = Combatants.Goblin("a");
            Actor b = Combatants.Goblin("b");

            Assert.True(field.Place(a, new Cell(1, 1)));
            Assert.False(field.Place(b, new Cell(1, 1)));
        }

        [Fact]
        public void AWallStopsSightAndTheRouteGoesRound()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Split));
            Actor a = Combatants.Goblin("a");

            field.Place(a, new Cell(2, 1));

            Assert.False(field.CanSee(new Cell(2, 1), new Cell(3, 1)));

            // the wall runs the full height of the room, so there is no way round at all
            Assert.Null(field.RouteFor(a, new Cell(3, 1)));
        }

        [Fact]
        public void ABurstCatchesEverythingInsideTheRadius()
        {
            Battlefield field = Open();

            Actor middle = Combatants.Goblin("middle");
            Actor beside = Combatants.Goblin("beside");
            Actor far = Combatants.Goblin("far");

            field.Place(middle, new Cell(3, 1));
            field.Place(beside, new Cell(4, 1));
            field.Place(far, new Cell(6, 1));

            List<Actor> caught = field.Caught(new Cell(3, 1), 1).ToList();

            Assert.Contains(middle, caught);
            Assert.Contains(beside, caught);
            Assert.DoesNotContain(far, caught);
        }

        [Fact]
        public void ABurstDoesNotReachThroughAWall()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Split));

            Actor behind = Combatants.Goblin("behind");
            field.Place(behind, new Cell(3, 1));

            Assert.DoesNotContain(behind, field.Caught(new Cell(2, 1), 2).ToList());
        }

        [Fact]
        public void ReachableIsEverySquareTheBudgetPaysFor()
        {
            Battlefield field = Open();
            Actor walker = Combatants.Goblin();

            field.Place(walker, new Cell(0, 0));

            IReadOnlyDictionary<Cell, int> reached = field.Reachable(walker, 1);

            // the three squares touching the corner, and nothing else
            Assert.Equal(3, reached.Count);
            Assert.Contains(new Cell(1, 1), reached.Keys);
        }
    }
}
