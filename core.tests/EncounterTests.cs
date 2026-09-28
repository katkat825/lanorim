using System.Collections.Generic;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Tests
{
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
            Actor goblin = Combatants.Goblin();

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
}
