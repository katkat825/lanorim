using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;

namespace Core.Tests
{
    public class ActionEconomyTests
    {
        [Fact]
        public void ExtraAttackStacksOnTheBaseTwo()
        {
            // the base two are solo compensation; Extra Attack is a third, every round
            var budget = new ActionBudget { ExtraActionsEachRound = 1 };

            Assert.Equal(3, budget.ActionsFor(1));
            Assert.Equal(3, budget.ActionsFor(7));
        }

        [Fact]
        public void NoTurnHoldsMoreThanTheGuardrail()
        {
            var budget = new ActionBudget { ExtraActionsEachRound = 5 };

            Assert.Equal(ActionBudget.MostActionsInATurn, budget.ActionsFor(1));
        }

        [Fact]
        public void ActionSurgeIsOneMoreActionOncePerRest()
        {
            var budget = new ActionBudget { ExtraActionsEachRound = 1, ExtraActionsPerLongRest = 1 };
            budget.LongRest();

            var turn = new Turn(Combatants.Hero(), budget, 1);

            Assert.Equal(3, turn.Actions);
            Assert.True(turn.Surge());
            Assert.Equal(4, turn.Actions);

            // spent for the rest
            var next = new Turn(Combatants.Hero(), budget, 2);
            Assert.False(next.Surge());

            budget.LongRest();
            Assert.True(new Turn(Combatants.Hero(), budget, 3).Surge());
        }

        [Fact]
        public void ASurgeOnATurnAlreadyAtTheCapIsNotSpent()
        {
            var budget = new ActionBudget { ExtraActionsEachRound = 2, ExtraActionsPerLongRest = 1 };
            budget.LongRest();

            var turn = new Turn(Combatants.Hero(), budget, 1);

            Assert.Equal(4, turn.Actions);
            Assert.False(turn.Surge());
            Assert.Equal(1, budget.ExtraActionsLeft);
        }

        static Encounter HeroFirst(out Actor hero, out Actor goblin, params int[] rolls)
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var fight = new Encounter(new StandardResolver(new ScriptedRng(rolls)), field,
                                      new CombatLog());

            hero = Combatants.Hero(ac: 10);
            goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(2, 1));
            fight.ArmOpportunity(goblin, Combatants.Scimitar);

            return fight;
        }

        [Fact]
        public void DisengagingMeansWalkingAwayProvokesNothing()
        {
            Encounter fight = HeroFirst(out Actor hero, out Actor goblin, 10, 1, 18, 3);
            fight.Begin();

            Turn turn = fight.Next();

            Assert.True(fight.Disengage(turn));
            fight.Walk(turn, new Cell(5, 1));

            Assert.Equal(hero.Health.Maximum, hero.Health.Current);
            Assert.Equal(1, fight.ReactionsLeft(goblin));
            Assert.Equal(1, turn.Actions);
        }

        [Fact]
        public void DashingIsAnotherTurnsWorthOfMovement()
        {
            Encounter fight = HeroFirst(out Actor hero, out _, 10, 1);
            fight.Begin();

            Turn turn = fight.Next();

            Assert.True(fight.Dash(turn));
            Assert.Equal(hero.Speed * 2, turn.Movement);
        }

        [Fact]
        public void ABonusActionDashNeedsAFeatureThatSaysSo()
        {
            Encounter fight = HeroFirst(out Actor hero, out _, 10, 1);
            fight.Begin();

            Turn turn = fight.Next();

            Assert.False(fight.Dash(turn, Spend.Bonus));
            Assert.Equal(1, turn.BonusActions);

            // Cunning Action
            hero.QuickOnBonus = Manoeuvre.Dash | Manoeuvre.Disengage | Manoeuvre.Hide;

            Assert.True(fight.Dash(turn, Spend.Bonus));
            Assert.Equal(0, turn.BonusActions);
            Assert.Equal(2, turn.Actions);
        }

        [Fact]
        public void HidingNeedsToBeUnseenAndGivesTheInvisibleConditionUntilYouAttack()
        {
            // initiative, a Stealth check of 18, then an attack: a pair of d20s for the advantage,
            // and a damage die
            Encounter fight = HeroFirst(out Actor hero, out Actor goblin, 10, 1, 18, 2, 19, 4, 3);
            fight.Begin();

            Turn turn = fight.Next();

            // SRD 5.2.1 Hide (p.183): not while an enemy can see you
            Assert.False(fight.CanHide(hero));
            Assert.Null(fight.Hide(turn));

            goblin.Apply(Condition.Blinded);
            Assert.True(fight.CanHide(hero));

            Attempt hid = fight.Hide(turn);

            Assert.True(hid.Succeeded);
            Assert.True(hero.Has(Condition.Invisible));

            goblin.Remove(Condition.Blinded);
            Assert.False(fight.Sees(goblin, hero));

            fight.Hit(turn, goblin, Combatants.Longsword);

            // the attack gave it away
            Assert.False(hero.Has(Condition.Invisible));
            Assert.True(fight.Sees(goblin, hero));
        }
    }
}
