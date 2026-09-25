using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;

namespace Core.Tests
{
    // decisions_checklist.md section 1, "Monsters don't get the solo extra action" (2026-09-25): a
    // monster plays its SRD statblock's turn - one action, a bonus action only if the statblock has
    // one, one reaction. its Attack action is one attack, or exactly what its Multiattack lists.
    public class MonsterTurnTests
    {
        static readonly Attack Bite =
            new Attack("bite", DiceRoll.Parse("1d8"), DamageType.Piercing);

        static readonly Attack Claw =
            new Attack("claw", DiceRoll.Parse("1d4"), DamageType.Slashing);

        // a monster beside a hero that will not fall over mid-test
        static (Encounter fight, Actor monster, Actor hero) Standoff(ActionBudget budget)
        {
            var fight = new Encounter(new StandardResolver(new SeededRng(7)),
                                      new Battlefield(Rooms.Read(Rooms.Open)));

            Actor monster = Combatants.Goblin(hp: 500);
            Actor hero = Combatants.Hero(hp: 500);

            fight.Enlist(monster, new Cell(1, 1), budget);
            fight.Enlist(hero, new Cell(2, 1));

            return (fight, monster, hero);
        }

        static Turn TurnOf(Encounter fight, Actor actor) =>
            new Turn(actor, fight.BudgetFor(actor), 2);

        [Fact]
        public void AStatblockTurnIsOneActionAndNoBonusUnlessItHasOne()
        {
            ActionBudget plain = ActionBudget.Statblock();

            Assert.Equal(1, plain.ActionsFor(1));
            Assert.Equal(1, plain.ActionsFor(2));
            Assert.Equal(0, plain.BonusActionsFor(2));
            Assert.Equal(1, plain.ReactionsFor(2));

            Assert.Equal(1, ActionBudget.Statblock(bonusAction: true).BonusActionsFor(2));
        }

        [Fact]
        public void AMonsterWithoutMultiattackAttacksOnce()
        {
            (Encounter fight, Actor monster, Actor hero) = Standoff(ActionBudget.Statblock());
            Turn turn = TurnOf(fight, monster);

            Assert.NotNull(fight.Hit(turn, hero, Combatants.Scimitar));
            Assert.False(turn.CanAttack);
            Assert.Null(fight.Hit(turn, hero, Combatants.Scimitar));
        }

        [Fact]
        public void AMultiattackOfThreeIsThreeAttacksForOneAction()
        {
            (Encounter fight, Actor monster, Actor hero) =
                Standoff(ActionBudget.Statblock(Multiattack.Any(3)));
            Turn turn = TurnOf(fight, monster);

            for (int i = 0; i < 3; i++)
                Assert.NotNull(fight.Hit(turn, hero, Combatants.Scimitar));

            Assert.Equal(0, turn.Actions);
            Assert.False(turn.CanAttack);
            Assert.Null(fight.Hit(turn, hero, Combatants.Scimitar));
        }

        [Fact]
        public void AMultiattackMakesExactlyTheAttacksItLists()
        {
            // "The bear makes one Bite attack and one Claw attack"
            var bear = new Multiattack(new[] { new[] { "bite" }, new[] { "claw" } });

            (Encounter fight, Actor monster, Actor hero) = Standoff(ActionBudget.Statblock(bear));
            Turn turn = TurnOf(fight, monster);

            Assert.NotNull(fight.Hit(turn, hero, Bite));
            Assert.False(turn.Allows(Bite));
            Assert.Null(fight.Hit(turn, hero, Bite));

            Assert.True(turn.Allows(Claw));
            Assert.NotNull(fight.Hit(turn, hero, Claw));
            Assert.False(turn.CanAttack);
        }

        [Fact]
        public void AnEitherOrSlotKeepsItsChoiceForTheAttackOnlyItTakes()
        {
            // the werewolf: two attacks, and one of them may be a bite. a claw first goes in the
            // claw-only slot, so the bite still has somewhere to go
            var werewolf = new Multiattack(new[] { new[] { "claw" }, new[] { "claw", "bite" } });

            (Encounter fight, Actor monster, Actor hero) = Standoff(ActionBudget.Statblock(werewolf));
            Turn turn = TurnOf(fight, monster);

            Assert.NotNull(fight.Hit(turn, hero, Claw));
            Assert.NotNull(fight.Hit(turn, hero, Bite));
            Assert.False(turn.CanAttack);
        }

        [Fact]
        public void AnAttackTheMultiattackDoesNotListIsAnAttackActionOfOne()
        {
            // the ghoul's Multiattack is two Bites; its Claw is an Attack action on its own
            var ghoul = new Multiattack(new[] { new[] { "bite" }, new[] { "bite" } });

            (Encounter fight, Actor monster, Actor hero) = Standoff(ActionBudget.Statblock(ghoul));
            Turn turn = TurnOf(fight, monster);

            Assert.NotNull(fight.Hit(turn, hero, Claw));
            Assert.False(turn.CanAttack);
            Assert.Null(fight.Hit(turn, hero, Bite));
        }

        [Fact]
        public void AMonsterCanDashOrAttackButNotBoth()
        {
            (Encounter fight, Actor monster, Actor hero) =
                Standoff(ActionBudget.Statblock(Multiattack.Any(2)));

            Turn dashed = TurnOf(fight, monster);
            Assert.True(fight.Dash(dashed));
            Assert.False(dashed.CanAttack);
            Assert.Null(fight.Hit(dashed, hero, Combatants.Scimitar));

            Turn attacked = TurnOf(fight, monster);
            Assert.NotNull(fight.Hit(attacked, hero, Combatants.Scimitar));
            Assert.False(fight.Dash(attacked));

            // and the rest of its Multiattack is still there - moving between attacks is allowed
            Assert.NotNull(fight.Hit(attacked, hero, Combatants.Scimitar));
        }

        [Fact]
        public void AMultiattackUnderWayLeavesNoActionToCastWith()
        {
            (Encounter fight, Actor monster, Actor hero) =
                Standoff(ActionBudget.Statblock(Multiattack.Any(2)));
            Turn turn = TurnOf(fight, monster);

            Assert.NotNull(fight.Hit(turn, hero, Combatants.Scimitar));

            Assert.False(turn.Can(Spend.Action));
            Assert.Equal(1, turn.AttacksLeft);
        }

        [Fact]
        public void AMonsterWithoutABonusActionCannotDisengageOnOne()
        {
            (Encounter fight, Actor monster, _) = Standoff(ActionBudget.Statblock());
            monster.QuickOnBonus = Manoeuvre.Disengage;

            Assert.False(fight.Disengage(TurnOf(fight, monster), Spend.Bonus));

            (Encounter nimble, Actor goblin, _) = Standoff(ActionBudget.Statblock(bonusAction: true));
            goblin.QuickOnBonus = Manoeuvre.Disengage | Manoeuvre.Hide;

            Turn turn = TurnOf(nimble, goblin);
            Assert.True(nimble.Disengage(turn, Spend.Bonus));
            Assert.True(turn.Can(Spend.Action));
        }

        [Fact]
        public void HeroesStillGetTwoActionsAndThreeWithExtraAttack()
        {
            (Encounter fight, Actor monster, Actor hero) = Standoff(ActionBudget.Statblock());

            var two = new Turn(hero, new ActionBudget(), 2);
            Assert.NotNull(fight.Hit(two, monster, Combatants.Longsword));
            Assert.NotNull(fight.Hit(two, monster, Combatants.Longsword));
            Assert.Null(fight.Hit(two, monster, Combatants.Longsword));

            var three = new Turn(hero, new ActionBudget { ExtraActionsEachRound = 1 }, 2);
            Assert.Equal(3, three.Actions);
            Assert.Null(new ActionBudget().Multiattack);
        }
    }
}
