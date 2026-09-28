using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Space;

namespace Core.Tests
{
    // cc_task_godfiles-dupes-efficiency.md #16: Reachable is one flood now, where it was an A* route
    // to every square on the map. the old way is kept here as the reference the flood must match
    public class ReachableTests
    {
        // walls, a shut door, rock, difficult ground and a corner to cut
        const string Rough = @"
+-+-+-+-+-+-+-+-+-+
|@ . ~ ~|. . . . .|
+ + + + + +-+ + + +
|. ~ # . . .|. ~ .|
+ + + + + + + + + +
|. ~ ~ . . .x. ~ .|
+-+ + + +-+ + + + +
|. . . . . . . ~ ~|
+ + + + + + + + + +
|. ~ . . .|. . . .|
+-+-+-+-+-+-+-+-+-+";

        static IReadOnlyDictionary<Cell, int> TheOldWay(Battlefield field, Actor actor, int budget)
        {
            var reached = new Dictionary<Cell, int>();
            Cell from = field.Where(actor).Value;

            foreach (Cell cell in field.Map.Cells)
            {
                if (cell == from || field.Occupies(cell, actor) || !field.Map.IsPassable(cell)) continue;
                if (field.CloserToFear(actor, from, cell)) continue;

                IReadOnlyList<Cell> route = field.RouteFor(actor, cell);

                if (route == null) continue;

                int cost = field.CostOf(route, actor);

                if (cost <= budget) reached[cell] = cost;
            }

            return reached;
        }

        static (Battlefield Field, Actor Walker) Room(string text, Cell start)
        {
            var field = new Battlefield(Rooms.Read(text));
            Actor walker = Combatants.Hero();

            field.Place(walker, start);
            field.Place(Combatants.Goblin("in_the_way"), new Cell(3, 3));
            field.Place(Combatants.Goblin("by_the_door"), new Cell(6, 1));

            return (field, walker);
        }

        public static IEnumerable<object[]> Starts()
        {
            foreach (string room in new[] { Rooms.Open, Rooms.Split, Rough })
                foreach (int budget in new[] { 1, 2, 3, 6, 12, 40 })
                    yield return new object[] { room, budget };
        }

        [Theory]
        [MemberData(nameof(Starts))]
        public void TheFloodReachesWhatEveryRouteReachedAtTheSameCost(string room, int budget)
        {
            foreach (Cell start in Rooms.Read(room).Cells.Where(c => Rooms.Read(room).IsPassable(c)).Take(12))
            {
                (Battlefield field, Actor walker) = Room(room, start);

                if (field.Where(walker) != start) continue;

                Assert.Equal(TheOldWay(field, walker, budget).ToList(),
                             field.Reachable(walker, budget).ToList());

                walker.Boons.Add(Boon.Of(new BoonSpec { Duration = Duration.Rest, FlySpeed = 60 }, "fly"));
                Assert.Equal(TheOldWay(field, walker, budget).ToList(),
                             field.Reachable(walker, budget).ToList());
            }
        }

        // crawling: the old way costed A*'s cheapest route and then added its steps, so where two
        // routes tied on ground it could pick the longer one and charge a crawl too much. the flood
        // charges each step its crawl and finds the true least. the squares are the same; the cost
        // is never more, and on plain floor it is the same
        [Theory]
        [MemberData(nameof(Starts))]
        public void ACrawlCostsNoMoreThanItDidAndTheSameOnPlainFloor(string room, int budget)
        {
            (Battlefield field, Actor walker) = Room(room, new Cell(0, 0));
            walker.Apply(Condition.Prone);

            IReadOnlyDictionary<Cell, int> old = TheOldWay(field, walker, budget);
            IReadOnlyDictionary<Cell, int> now = field.Reachable(walker, budget);

            Assert.All(old, p => Assert.True(now.ContainsKey(p.Key) && now[p.Key] <= p.Value));

            if (room != Rough) Assert.Equal(old.Count, now.Count);
            if (room != Rough) Assert.All(old, p => Assert.Equal(p.Value, now[p.Key]));
        }

        [Fact]
        public void AFrightenedCreatureStillCannotStepTowardsItsFear()
        {
            var open = new Battlefield(Rooms.Read(Rooms.Open));
            Actor scared = Combatants.Hero();
            Actor source = Combatants.Goblin("source");

            open.Place(scared, new Cell(0, 1));
            open.Place(source, new Cell(4, 1));
            scared.Apply(Condition.Frightened, source);

            Assert.Equal(TheOldWay(open, scared, 6), open.Reachable(scared, 6));
            Assert.DoesNotContain(new Cell(1, 1), open.Reachable(scared, 6).Keys);
        }
    }
}
