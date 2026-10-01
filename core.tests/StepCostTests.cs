using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;

namespace Core.Tests
{
    // ONE STEP COST FOR THE ROUTE, THE FLOOD AND THE WALK (cc_task_e-shop-species-and-ui-notes.md 2.5): "I can go one square
    // at a time and get there in less squares than if I click on the square". The route used to be found on the map's
    // ground alone, so it went straight through a spell's difficult ground the walk then charged double for
    public class StepCostTests
    {
        const string Hall = @"
+-+-+-+-+-+-+-+-+-+
|. . . . . . . . .|
+ + + + + + + + + +
|. . . . . . . . .|
+ + + + + + + + + +
|@ . . . . . . . .|
+ + + + + + + + + +
|. . . . . . . . .|
+ + + + + + + + + +
|. . . . . . . . .|
+-+-+-+-+-+-+-+-+-+";

        // a spell's difficult ground across the straight line: a band one square wide, three tall
        static bool Band(Cell cell, Actor mover) => cell.X == 3 && cell.Y >= 1 && cell.Y <= 3;

        // the same, wall to wall: no way round it
        static bool Wall(Cell cell, Actor mover) => cell.X == 3;

        static (Encounter Fight, Actor Hero, Turn Turn) Start(System.Func<Cell, Actor, bool> rough = null)
        {
            var fight = new Encounter(new StandardResolver(new ScriptedRng(20, 1)), new Battlefield(Rooms.Read(Hall)));
            Actor hero = Combatants.Hero();

            fight.Enlist(hero, new Cell(0, 2));
            fight.Enlist(Combatants.Goblin(), new Cell(8, 4));
            fight.Field.MadeRough = rough ?? Band;
            fight.Begin();

            return (fight, hero, fight.Next());
        }

        [Fact]
        public void TheClickedRouteGoesRoundASpellsDifficultGroundWhenThatIsCheaper()
        {
            (Encounter fight, Actor hero, Turn turn) = Start();
            var to = new Cell(6, 2);

            var route = fight.Field.RouteFor(hero, to);

            // round the band (six squares) rather than through it (six squares, one of them double)
            Assert.DoesNotContain(route, c => Band(c, hero));
            Assert.Equal(6, fight.Field.CostOf(route, hero));
            Assert.Equal(6, fight.Field.Reachable(hero, turn.SquaresLeft)[to]);

            // and a 30-foot hero gets there, where the straight line ran out a square short
            var walked = fight.Walk(turn, to);

            Assert.Equal(to, walked.Last());
            Assert.Equal(0, turn.SquaresLeft);
        }

        // THE BUG AS IT PLAYED: the board's reach was blind to a spell's difficult ground, so it lit a square six away
        // across Spike Growth, the click took the hero five squares and the movement was gone. now the board says
        // what the walk will charge
        [Fact]
        public void TheBoardOnlyOffersWhatTheWalkCanPayForAcrossASpellsGround()
        {
            (Encounter fight, Actor hero, Turn turn) = Start(Wall);
            var reach = fight.Field.Reachable(hero, turn.SquaresLeft);

            Assert.False(reach.ContainsKey(new Cell(6, 2)));
            Assert.Equal(6, reach[new Cell(5, 2)]);

            Assert.Equal(new Cell(5, 2), fight.Walk(turn, new Cell(5, 2)).Last());
            Assert.Equal(0, turn.SquaresLeft);
        }

        // what the click pays is what walking it square by square pays
        [Fact]
        public void TheClickCostsWhatSquareBySquareCosts()
        {
            (Encounter clicked, Actor hero, Turn turn) = Start();
            var to = new Cell(6, 2);
            var route = clicked.Field.RouteFor(hero, to);

            clicked.Walk(turn, to);
            int byClick = 6 - turn.SquaresLeft;

            (Encounter stepped, Actor walker, Turn steps) = Start();

            foreach (Cell cell in route.Skip(1)) stepped.Walk(steps, cell);

            Assert.Equal(byClick, 6 - steps.SquaresLeft);
            Assert.Equal(to, stepped.Field.Where(walker));
        }

        // Frightened is a step the route can't take, not only a square it can't end on
        [Fact]
        public void AFrightenedRouteNeverStepsCloserToTheFear()
        {
            (Encounter fight, Actor hero, Turn turn) = Start();
            Actor fear = fight.Field.Pieces.First(a => !ReferenceEquals(a, hero));

            hero.Apply(Condition.Frightened, fear);

            foreach ((Cell cell, int cost) in fight.Field.Reachable(hero, turn.SquaresLeft))
            {
                var route = fight.Field.RouteFor(hero, cell);

                Assert.NotNull(route);

                for (int i = 1; i < route.Count; i++)
                    Assert.False(fight.Field.CloserToFear(hero, route[i - 1], route[i]), $"{route[i - 1]} -> {route[i]}");

                Assert.Equal(cost, fight.Field.CostOf(route, hero));
            }
        }
    }
}
