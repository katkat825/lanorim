using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;

namespace Core.Tests
{
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
