using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Tests
{
    // a 7 x 3 room with nothing in it but floor, and one with a wall down the middle
    static class Rooms
    {
        public const string Open = @"
+-+-+-+-+-+-+-+
|@ . . . . . .|
+ + + + + + + +
|. . . . . . .|
+ + + + + + + +
|. . . . . . .|
+-+-+-+-+-+-+-+";

        public const string Split = @"
+-+-+-+-+-+-+-+
|@ . .|. . . .|
+ + + + + + + +
|. . .|. . . .|
+ + + + + + + +
|. . .|. . . .|
+-+-+-+-+-+-+-+";

        public static MapLayout Read(string text)
        {
            Assert.True(MapReader.TryRead(text, out MapLayout map, out string problem), problem);
            return map;
        }
    }

    static class Combatants
    {
        public static Actor Hero(int hp = 40, int ac = 16)
        {
            var hero = new Actor("hero", 5, new AbilityScores(18, 14, 16, 10, 12, 10),
                                 Allegiance.Hero);

            hero.SetHealth(new Health(hp, Die.D10, 5));
            hero.Armor = new ArmorProfile(ArmorWeight.Heavy, ac);

            return hero;
        }

        public static Actor Goblin(string id = "goblin", int hp = 7, int ac = 15)
        {
            var goblin = new Actor(id, 1, new AbilityScores(8, 14, 10, 10, 8, 8));

            goblin.SetHealth(new Health(hp, Die.D6, 2));
            goblin.Armor = new ArmorProfile(ArmorWeight.Light, 13);
            goblin.HasShield = true;
            goblin.Speed = 30;

            return goblin;
        }

        public static readonly Attack Scimitar =
            new Attack("scimitar", DiceRoll.Parse("1d6"), DamageType.Slashing,
                       Ability.Dexterity, finesse: true);

        public static readonly Attack Shortbow =
            new Attack("shortbow", DiceRoll.Parse("1d6"), DamageType.Piercing,
                       Ability.Dexterity, range: 16, longRange: 64);

        public static readonly Attack Longsword =
            new Attack("longsword", DiceRoll.Parse("1d8"), DamageType.Slashing);
    }

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
        public void BeingStunnedStopsActionsButTheTurnStillHappens()
        {
            Actor hero = Combatants.Hero();
            hero.Apply(Condition.Stunned);

            var turn = new Turn(hero, new ActionBudget(), 1);

            Assert.False(turn.Can(Spend.Action));
            Assert.False(turn.Can(Spend.Movement, 5));
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

    public class EncounterTests
    {
        static Encounter Start(out Actor hero, out Actor goblin, IRng rng,
                               ICombatObserver observer = null)
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var fight = new Encounter(new StandardResolver(rng), field, observer);

            hero = Combatants.Hero();
            goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(0, 1));
            fight.Enlist(goblin, new Cell(6, 1));

            return fight;
        }

        [Fact]
        public void InitiativeOrdersHighestFirstAndTheHeroWinsATie()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var fight = new Encounter(new StandardResolver(new ScriptedRng(10)), field);

            Actor hero = Combatants.Hero();
            Actor goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(0, 1));
            fight.Enlist(goblin, new Cell(6, 1));
            fight.Begin();

            // both roll a 10; the goblin has the better Dex, but the hero takes the tie
            Assert.Equal("hero", fight.Order[0].Actor.Id);
        }

        [Fact]
        public void AnAttackOutOfRangeIsRefusedAndCostsNothing()
        {
            Encounter fight = Start(out Actor hero, out Actor goblin, new ScriptedRng(15, 5));
            fight.Begin();

            Turn turn = fight.Next();

            // six squares apart, a longsword reaches one
            Assert.Null(fight.Hit(turn, goblin, Combatants.Longsword));
            Assert.Equal(2, turn.Actions);
        }

        [Fact]
        public void WalkingSpendsMovementAndStopsWhenItRunsOut()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var fight = new Encounter(new StandardResolver(new ScriptedRng(20, 1)), field);

            Actor hero = Combatants.Hero();
            hero.Speed = 15; // three squares, and the far wall is six away

            Actor goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(0, 1));
            fight.Enlist(goblin, new Cell(6, 2));
            fight.Begin();

            Turn turn = fight.Next();
            Assert.Equal("hero", turn.Actor.Id);

            IReadOnlyList<Cell> walked = fight.Walk(turn, new Cell(6, 1));

            // three squares along A*'s route, wherever its tie-break took them
            Assert.Equal(4, walked.Count);
            Assert.Equal(3, Battlefield.Distance(new Cell(0, 1), walked[walked.Count - 1]));
            Assert.Equal(0, turn.Movement);
        }

        [Fact]
        public void WalkingOntoAnOccupiedSquareIsRefusedOutright()
        {
            Encounter fight = Start(out Actor hero, out Actor goblin, new ScriptedRng(20, 1));
            fight.Begin();

            Turn turn = fight.Next();

            Assert.Empty(fight.Walk(turn, new Cell(6, 1)));
            Assert.Equal(30, turn.Movement);
        }

        [Fact]
        public void LeavingAnEnemysReachProvokesExactlyOneSwing()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var log = new CombatLog();

            // hero: init 10. goblin: init 1. then the hero's move provokes: attack 18, damage 3
            var fight = new Encounter(new StandardResolver(new ScriptedRng(10, 1, 18, 3)), field, log);

            Actor hero = Combatants.Hero(ac: 10);
            Actor goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(2, 1));
            fight.ArmOpportunity(goblin, Combatants.Scimitar);

            fight.Begin();

            Turn turn = fight.Next();
            Assert.Equal("hero", turn.Actor.Id);

            fight.Walk(turn, new Cell(5, 1));

            Assert.Contains(log.Lines, l => l.Contains("takes a swing"));
            Assert.Equal(0, fight.ReactionsLeft(goblin));
            Assert.True(hero.Health.Current < hero.Health.Maximum);
        }

        [Fact]
        public void MovingWithinReachProvokesNothing()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var log = new CombatLog();
            var fight = new Encounter(new StandardResolver(new ScriptedRng(10, 1)), field, log);

            Actor hero = Combatants.Hero();
            Actor goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(2, 1));
            fight.ArmOpportunity(goblin, Combatants.Scimitar);

            fight.Begin();

            Turn turn = fight.Next();

            // 1,1 -> 2,0 -> 2,2 keeps the goblin in reach the whole way
            fight.Walk(turn, new Cell(2, 0));
            fight.Walk(turn, new Cell(2, 2));

            Assert.DoesNotContain(log.Lines, l => l.Contains("takes a swing"));
            Assert.Equal(1, fight.ReactionsLeft(goblin));
        }

        [Fact]
        public void AReactionComesBackNextRound()
        {
            Encounter fight = Start(out Actor hero, out Actor goblin, new ScriptedRng(10, 1));
            fight.ArmOpportunity(goblin, Combatants.Scimitar);
            fight.Begin();

            Assert.True(fight.TakeReaction(goblin));
            Assert.False(fight.TakeReaction(goblin));

            fight.Next();
            fight.Next();
            fight.Next(); // wraps into round 2

            Assert.Equal(2, fight.Round);
            Assert.Equal(1, fight.ReactionsLeft(goblin));
        }

        [Fact]
        public void ShootingPastTheNormalRangeIsAtDisadvantage()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));

            var bow = new Attack("shortbow", DiceRoll.Parse("1d6"), DamageType.Piercing,
                                 Ability.Dexterity, range: 2, longRange: 20);

            // init 10 / 1, then the shot: 19 and 2 on the two dice, damage 4
            var fight = new Encounter(new StandardResolver(new ScriptedRng(10, 1, 19, 2, 4)), field);

            Actor hero = Combatants.Hero();
            Actor goblin = Combatants.Goblin(ac: 15);

            fight.Enlist(hero, new Cell(0, 1));
            fight.Enlist(goblin, new Cell(6, 1));
            fight.Begin();

            Turn turn = fight.Next();
            Blow blow = fight.Hit(turn, goblin, bow);

            Assert.NotNull(blow);
            Assert.Equal(Advantage.Disadvantage, blow.Attempt.Roll.Advantage);
            Assert.Equal(2, blow.Attempt.Natural);
        }

        [Fact]
        public void TheFightEndsWhenTheLastEnemyGoesDown()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var log = new CombatLog();

            // init 20 / 1, then a natural 20 and maximum damage
            var fight = new Encounter(
                new StandardResolver(new ScriptedRng(20, 1, 20, 8, 8)), field, log);

            Actor hero = Combatants.Hero();
            Actor goblin = Combatants.Goblin(hp: 7);

            fight.Enlist(hero, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(2, 1));
            fight.Begin();

            Turn turn = fight.Next();
            fight.Hit(turn, goblin, Combatants.Longsword);

            Assert.True(goblin.IsDown);
            Assert.Equal(Outcome.HeroesWon, fight.Judge());
            Assert.Null(fight.Next());
        }

        [Fact]
        public void ADownedHeroTakesTheDeathSaveOnItsTurn()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var log = new CombatLog();

            // init 1 / 20 so the goblin goes first, then a 10 on the death save
            var fight = new Encounter(new StandardResolver(new ScriptedRng(1, 20, 10)), field, log);

            Actor hero = Combatants.Hero();
            Actor goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(2, 1));
            fight.Begin();

            hero.Suffer(999, DamageType.Slashing);

            fight.Next(); // the goblin's turn
            fight.Next(); // the hero is down: it saves instead

            Assert.Contains(log.Lines, l => l.Contains("death save"));
            Assert.Equal(1, hero.Health.Current);
        }
    }

    public class TacticsTests
    {
        [Fact]
        public void AGoblinWalksUpAndSwingsTwice()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var log = new CombatLog();

            // init 1 / 20 (goblin first), then two attacks that hit for 3 each
            var fight = new Encounter(
                new StandardResolver(new ScriptedRng(1, 20, 18, 3, 18, 3)), field, log);

            Actor hero = Combatants.Hero(ac: 10);
            Actor goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(0, 1));
            fight.Enlist(goblin, new Cell(4, 1));
            fight.Begin();

            Turn turn = fight.Next();
            Assert.Equal("goblin", turn.Actor.Id);

            new BasicTactics(new[] { Combatants.Scimitar }).Take(fight, turn);

            Assert.Equal(1, field.Distance(hero, goblin));
            Assert.Equal(2, log.Lines.Count(l => l.Contains("scimitar")));
        }

        [Fact]
        public void AFinisherGoesForTheWoundedOneEvenIfItIsFurtherAway()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var log = new CombatLog();
            // the pieces roll in cell order: goblin at 2,1 then near at 3,1 then hurt at 6,1
            var fight = new Encounter(
                new StandardResolver(new ScriptedRng(20, 1, 1, 18, 3, 18, 3)), field, log);

            var near = new Actor("near", 3, new AbilityScores(), Allegiance.Hero);
            near.SetHealth(new Health(30));

            var hurt = new Actor("hurt", 3, new AbilityScores(), Allegiance.Hero);
            hurt.SetHealth(new Health(30));
            hurt.Suffer(27, DamageType.Slashing);

            Actor goblin = Combatants.Goblin();

            fight.Enlist(near, new Cell(3, 1));
            fight.Enlist(hurt, new Cell(6, 1));
            fight.Enlist(goblin, new Cell(2, 1));
            fight.Begin();

            Turn turn = fight.Next();
            Assert.Equal("goblin", turn.Actor.Id);

            new BasicTactics(new[] { Combatants.Scimitar }, Instinct.Finisher).Take(fight, turn);

            // it walked past the healthy one to get at the hurt one
            Assert.True(field.Distance(goblin, hurt) < field.Distance(goblin, near));
        }

        [Fact]
        public void ASkirmisherKeepsItsDistanceAndShoots()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var log = new CombatLog();
            var fight = new Encounter(
                new StandardResolver(new ScriptedRng(1, 20, 18, 3, 18, 3)), field, log);

            Actor hero = Combatants.Hero(ac: 10);
            Actor archer = Combatants.Goblin("archer");

            fight.Enlist(hero, new Cell(0, 1));
            fight.Enlist(archer, new Cell(6, 1));
            fight.Begin();

            Turn turn = fight.Next();

            new BasicTactics(new[] { Combatants.Shortbow, Combatants.Scimitar },
                             Instinct.Skirmisher).Take(fight, turn);

            Assert.True(field.Distance(hero, archer) > 1);
            Assert.Contains(log.Lines, l => l.Contains("shortbow"));
        }

        [Fact]
        public void AnIncapacitatedMonsterDoesNothingAtAll()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var log = new CombatLog();
            var fight = new Encounter(new StandardResolver(new ScriptedRng(1, 20)), field, log);

            Actor hero = Combatants.Hero();
            Actor goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(0, 1));
            fight.Enlist(goblin, new Cell(4, 1));
            fight.Begin();

            Turn turn = fight.Next();
            goblin.Apply(Condition.Stunned);

            new BasicTactics(new[] { Combatants.Scimitar }).Take(fight, turn);

            Assert.Equal(new Cell(4, 1), field.Where(goblin));
            Assert.DoesNotContain(log.Lines, l => l.Contains("scimitar"));
        }
    }
}
