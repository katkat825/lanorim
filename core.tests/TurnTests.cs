using Core.Characters;
using Core.Combat;

namespace Core.Tests
{
    public class TurnTests
    {
        static Turn Fresh(int round = 1) =>
            new Turn(Combatants.Hero(), new ActionBudget(), round);

        [Fact]
        public void TheBudgetIsTwoActionsOneBonusOneReaction()
        {
            Turn turn = Fresh();

            Assert.Equal(2, turn.Actions);
            Assert.Equal(1, turn.BonusActions);
            Assert.Equal(1, new ActionBudget().ReactionsFor(1));
        }

        [Fact]
        public void SpendingTakesItOffAndRefusesWhenItIsGone()
        {
            Turn turn = Fresh();

            Assert.True(turn.Take(Spend.Action));
            Assert.True(turn.Take(Spend.Action));
            Assert.False(turn.Take(Spend.Action));

            Assert.True(turn.Take(Spend.Bonus));
            Assert.False(turn.Take(Spend.Bonus));
        }

        [Fact]
        public void AFirstRoundGrantIsOnlyThereOnTheFirstRound()
        {
            var budget = new ActionBudget { ExtraActionsFirstRound = 1 };

            Assert.Equal(3, budget.ActionsFor(1));
            Assert.Equal(2, budget.ActionsFor(2));
        }

        [Fact]
        public void APerLongRestGrantIsAPoolThatEmpties()
        {
            var budget = new ActionBudget { ExtraActionsPerLongRest = 2 };

            budget.LongRest();

            Assert.True(budget.DrawExtraAction());
            Assert.True(budget.DrawExtraAction());
            Assert.False(budget.DrawExtraAction());

            budget.LongRest();

            Assert.True(budget.DrawExtraAction());
        }

        [Fact]
        public void MovementIsTheActorsSpeedInFeet()
        {
            Turn turn = Fresh();

            Assert.Equal(30, turn.Movement);
            Assert.Equal(6, turn.SquaresLeft);
        }

        [Fact]
        public void StandingUpCostsHalfYourSpeedAndYouKeepTheRest()
        {
            Actor hero = Combatants.Hero();
            hero.Apply(Condition.Prone);

            var turn = new Turn(hero, new ActionBudget(), 1);

            Assert.True(turn.StandUp());
            Assert.Equal(15, turn.Movement);
            Assert.False(hero.Has(Condition.Prone));
        }

        [Fact]
        public void BeingStunnedStopsActionsButNotMovement()
        {
            // SRD 5.2.1 Stunned (p.189) has no speed clause: Incapacitated, Str and Dex saves
            // failed, attacks against it at advantage - and it can still move
            Actor hero = Combatants.Hero();
            hero.Apply(Condition.Stunned);

            var turn = new Turn(hero, new ActionBudget(), 1);

            Assert.False(turn.Can(Spend.Action));
            Assert.True(turn.Can(Spend.Movement, 5));
        }

        [Fact]
        public void BeingGrappledStopsMovementButNotActions()
        {
            Actor hero = Combatants.Hero();
            hero.Apply(Condition.Grappled);

            var turn = new Turn(hero, new ActionBudget(), 1);

            Assert.True(turn.Can(Spend.Action));
            Assert.False(turn.Can(Spend.Movement, 5));
        }
    }
}
