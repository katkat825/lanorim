using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Tests
{
    public class ReactionWindowTests
    {
        // the goblin first (initiative 20 against 1), standing next to the hero
        static Encounter GoblinFirst(out Actor hero, out Actor goblin, params int[] rolls)
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var fight = new Encounter(new StandardResolver(new ScriptedRng(rolls)), field,
                                      new CombatLog());

            hero = Combatants.Hero(ac: 16);
            goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(2, 1));

            return fight;
        }

        [Fact]
        public void AReactionToBeingHitCanTurnTheHitIntoAMiss()
        {
            // initiative 1 and 20, then the goblin's 15 + 4 = 19 against armor class 16
            Encounter fight = GoblinFirst(out Actor hero, out Actor goblin, 1, 20, 15, 4);

            var brace = new Brace();
            fight.Arm(hero, brace);
            fight.Begin();

            Turn turn = fight.Next();
            Assert.Same(goblin, turn.Actor);

            Blow blow = fight.Hit(turn, hero, Combatants.Scimitar);

            // the die said 19, and 19 does not beat 21
            Assert.Equal(1, brace.Answered);
            Assert.False(blow.Hit);
            Assert.Equal(19, blow.Attempt.Total);
            Assert.Equal(21, blow.Attempt.Against);
            Assert.Equal(hero.Health.Maximum, hero.Health.Current);
            Assert.Equal(0, fight.ReactionsLeft(hero));
        }

        [Fact]
        public void AMissIsNotOfferedAnything()
        {
            // 5 + 4 misses armor class 16 on its own
            Encounter fight = GoblinFirst(out Actor hero, out Actor goblin, 1, 20, 5);

            var brace = new Brace();
            fight.Arm(hero, brace);
            fight.Begin();

            fight.Hit(fight.Next(), hero, Combatants.Scimitar);

            Assert.Equal(0, brace.Answered);
            Assert.Equal(1, fight.ReactionsLeft(hero));
        }

        [Fact]
        public void AnArmorClassBoonUntilYourNextTurnEndsWhenYourTurnStarts()
        {
            Encounter fight = GoblinFirst(out Actor hero, out Actor goblin, 1, 20, 15, 4);

            fight.Arm(hero, new Brace());
            fight.Begin();

            Turn turn = fight.Next();
            fight.Hit(turn, hero, Combatants.Scimitar);

            Assert.Equal(21, hero.ArmorClass);

            fight.EndTurn();

            // still up through the rest of the goblin's turn and right up to the hero's own
            Assert.Equal(21, hero.ArmorClass);

            Turn mine = fight.Next();
            Assert.Same(hero, mine.Actor);
            Assert.Equal(16, hero.ArmorClass);
        }

        [Fact]
        public void NobodyReactsTwiceInARound()
        {
            // both of the goblin's swings hit (19 each); the hero can brace for the first only
            Encounter fight = GoblinFirst(out Actor hero, out Actor goblin, 1, 20, 15, 4, 15, 4);

            var brace = new Brace();
            fight.Arm(hero, brace);
            fight.Begin();

            Turn turn = fight.Next();

            fight.Hit(turn, hero, Combatants.Scimitar);
            fight.Hit(turn, hero, new Attack("second_scimitar", DiceRoll.Parse("1d6"),
                                             DamageType.Slashing, Ability.Dexterity,
                                             finesse: true));

            Assert.Equal(1, brace.Answered);
        }

        [Fact]
        public void AChooserThatDeclinesKeepsTheReactionAndTheBlowLands()
        {
            Encounter fight = GoblinFirst(out Actor hero, out Actor goblin, 1, 20, 15, 4);

            var brace = new Brace();
            fight.Arm(hero, brace);
            fight.ChooseReactionsWith(hero, ReactionChoosers.Never);
            fight.Begin();

            Blow blow = fight.Hit(fight.Next(), hero, Combatants.Scimitar);

            Assert.True(blow.Hit);
            Assert.Equal(0, brace.Answered);
            Assert.Equal(1, fight.ReactionsLeft(hero));
        }

        [Fact]
        public void TheSensibleChooserOnlyBracesWhenItWouldTurnTheHit()
        {
            // 20 + 4 = 24 hits armor class 21 as well, so bracing would be a wasted reaction.
            // a natural 20 is also a critical, which hits whatever the armor class is
            Encounter fight = GoblinFirst(out Actor hero, out Actor goblin, 1, 20, 20, 4, 4);

            var brace = new Brace();
            fight.Arm(hero, brace);
            fight.ChooseReactionsWith(hero, ReactionChoosers.WhenItHelps);
            fight.Begin();

            Blow blow = fight.Hit(fight.Next(), hero, Combatants.Scimitar);

            Assert.True(blow.Hit);
            Assert.Equal(0, brace.Answered);
        }

        [Fact]
        public void BeingHurtOffersTheDamagedWindowWithHowMuch()
        {
            Encounter fight = GoblinFirst(out Actor hero, out Actor goblin, 1, 20, 15, 4);

            var watcher = new Watcher(Trigger.Damaged);
            fight.Arm(hero, watcher);
            fight.Begin();

            Blow blow = fight.Hit(fight.Next(), hero, Combatants.Scimitar);

            Moment seen = Assert.Single(watcher.Seen);
            Assert.Same(goblin, seen.Source);
            Assert.Equal(blow.Suffered, seen.Damage);
        }

        [Fact]
        public void TheOpportunityAttackStillFiresThroughTheWindow()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var log = new CombatLog();
            var fight = new Encounter(new StandardResolver(new ScriptedRng(10, 1, 18, 3)), field,
                                      log);

            Actor hero = Combatants.Hero(ac: 10);
            Actor goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(2, 1));
            fight.ArmOpportunity(goblin, Combatants.Scimitar);
            fight.Begin();

            fight.Walk(fight.Next(), new Cell(5, 1));

            Assert.Contains(log.Lines, l => l.Contains("reacts with opportunity_attack"));
            Assert.Contains(log.Lines, l => l.Contains("takes a swing"));
            Assert.Same(Combatants.Scimitar, fight.OpportunityAttackOf(goblin));
        }

        [Fact]
        public void ACastIsOfferedToEveryEnemyAndOneStopIsEnough()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));
            var fight = new Encounter(new StandardResolver(new ScriptedRng(10)), field);

            Actor hero = Combatants.Hero();
            Actor first = Combatants.Goblin("first");
            Actor second = Combatants.Goblin("second");

            fight.Enlist(hero, new Cell(0, 1));
            fight.Enlist(first, new Cell(5, 0));
            fight.Enlist(second, new Cell(6, 2));

            var stops = new Watcher(Trigger.Cast, stops: true);
            var late = new Watcher(Trigger.Cast);

            fight.Arm(first, stops);
            fight.Arm(second, late);
            fight.Begin();

            Moment casting = Moment.Cast(hero, "fireball", 3);
            fight.Offer(casting, fight.Field.Enemies(hero));

            Assert.True(casting.Stopped);
            Assert.Single(stops.Seen);

            // stopped is stopped: the second goblin keeps its reaction
            Assert.Empty(late.Seen);
            Assert.Equal(1, fight.ReactionsLeft(second));
        }

        [Fact]
        public void ADownedCreatureIsOfferedNothing()
        {
            Encounter fight = GoblinFirst(out Actor hero, out Actor goblin, 1, 20);

            var watcher = new Watcher(Trigger.Cast);
            fight.Arm(goblin, watcher);
            fight.Begin();

            goblin.Suffer(100, DamageType.Slashing);

            fight.Offer(Moment.Cast(hero, "fire_bolt", 0), goblin);

            Assert.Empty(watcher.Seen);
        }
    }
}
