using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Tests
{
    // the SRD 5.2.1 check of 2026-09-25: the conditions against the Rules Glossary, and the core
    // rules the engine calls SRD. each test names its page
    public class SrdRulesTests
    {
        // a 7 x 3 open room; the hero at (1,1), the goblin at (x,1)
        static Encounter Room(out Actor hero, out Actor goblin, int x = 2, params int[] rolls)
        {
            var fight = new Encounter(new StandardResolver(new ScriptedRng(rolls.Length > 0 ? rolls : new[] { 10 })),
                                      new Battlefield(Rooms.Read(Rooms.Open)), new CombatLog());

            hero = Combatants.Hero(ac: 10);
            goblin = Combatants.Goblin(hp: 500);

            fight.Enlist(hero, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(x, 1));

            return fight;
        }

        // --- Frightened (SRD p.182) ---------------------------------------------------------------

        [Fact]
        public void FrightenedAttacksAtDisadvantageOnlyWhileTheSourceIsInSight()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin);
            hero.Apply(Condition.Frightened, goblin);

            Assert.True(fight.FearInSight(hero));
            Assert.Equal(Advantage.Disadvantage, Strike.Lean(hero, goblin, true, true, true,
                                                            fearInSight: fight.FearInSight(hero)));

            hero.Apply(Condition.Blinded);
            Assert.False(fight.FearInSight(hero));
        }

        [Fact]
        public void AFrightenedCreatureCannotStepCloserToItsFear()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin, x: 5);
            fight.Begin();
            hero.Apply(Condition.Frightened, goblin);

            Turn turn = fight.Next();
            if (!ReferenceEquals(turn.Actor, hero)) { fight.EndTurn(); turn = fight.Next(); }

            Assert.DoesNotContain(new Cell(3, 1), fight.Field.Reachable(hero, 6).Keys);

            fight.Walk(turn, new Cell(3, 1));
            Assert.Equal(new Cell(1, 1), fight.Field.Where(hero));

            // away is fine
            fight.Walk(turn, new Cell(0, 1));
            Assert.Equal(new Cell(0, 1), fight.Field.Where(hero));
        }


        // --- Grappled, and the Unarmed Strike's Grapple and Shove (SRD p.182, p.190) ----------------

        [Fact]
        public void AGrappledCreatureAttacksAnyoneButItsGrapplerAtDisadvantage()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin);
            Actor other = Combatants.Goblin("other");
            hero.Apply(Condition.Grappled, goblin);

            Assert.Equal(Advantage.Flat, Strike.Lean(hero, goblin, true, true, true));
            Assert.Equal(Advantage.Disadvantage, Strike.Lean(hero, other, true, true, true));
        }

        [Fact]
        public void AGrappleIsASaveAgainstEightPlusStrengthAndProficiency()
        {
            // the hero goes first; the goblin's save is a 1
            Encounter fight = Room(out Actor hero, out Actor goblin, 2, 20, 1, 1);
            fight.Begin();

            Turn turn = fight.Next();
            Assert.Same(hero, turn.Actor);

            Assert.Equal(8 + 4 + 3, Encounter.UnarmedDc(hero));

            Attempt save = fight.Grapple(turn, goblin);

            Assert.True(save.Failed);
            Assert.True(goblin.Has(Condition.Grappled));
            Assert.True(goblin.HasFrom(Condition.Grappled, hero));
            Assert.True(fight.IsGrappledBy(goblin, hero));
        }

        [Fact]
        public void TheGrapplerDragsWhatItHoldsAndLettingItGetAwayEndsIt()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin, 2, 20, 1, 1);
            fight.Begin();

            Turn turn = fight.Next();
            fight.Grapple(turn, goblin);

            // dragging costs double: two squares west is 20 feet
            int before = turn.Movement;
            fight.Walk(turn, new Cell(0, 1));

            Assert.Equal(new Cell(0, 1), fight.Field.Where(hero));
            Assert.Equal(new Cell(1, 1), fight.Field.Where(goblin));
            Assert.Equal(before - 10, turn.Movement);

            // pushed out of reach, the grapple ends
            fight.Shove(goblin, new Cell(4, 1));
            Assert.False(goblin.Has(Condition.Grappled));
        }

        [Fact]
        public void AGrapplerThatCannotActLetsGo()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin, 2, 20, 1, 1);
            fight.Begin();

            fight.Grapple(fight.Next(), goblin);
            Assert.True(goblin.Has(Condition.Grappled));

            hero.Apply(Condition.Stunned);
            fight.Changed(hero, Condition.Stunned, true);

            Assert.False(goblin.Has(Condition.Grappled));
        }

        [Fact]
        public void AShoveKnocksProneOrPushesFiveFeet()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin, 2, 20, 1, 1, 1);
            fight.Begin();

            Turn turn = fight.Next();

            fight.ShoveAway(turn, goblin, prone: false);
            Assert.Equal(new Cell(3, 1), fight.Field.Where(goblin));

            fight.Walk(turn, new Cell(2, 1));
            fight.ShoveAway(turn, goblin, prone: true);
            Assert.True(goblin.Has(Condition.Prone));
        }

        [Fact]
        public void EscapingAGrappleIsAnActionAndAnAthleticsOrAcrobaticsCheck()
        {
            // the goblin's escape roll is a 20
            Encounter fight = Room(out Actor hero, out Actor goblin, 2, 20, 1, 1, 20);
            fight.Begin();

            fight.Grapple(fight.Next(), goblin);
            fight.EndTurn();

            Turn its = fight.Next();
            Assert.Same(goblin, its.Actor);
            int actions = its.Actions;

            Attempt free = fight.EscapeGrapple(its);

            Assert.True(free.Succeeded);
            Assert.False(goblin.Has(Condition.Grappled));
            Assert.Equal(actions - 1, its.Actions);
        }


        // --- Prone (SRD p.186) and crawling (p.179) ---------------------------------------------------

        [Fact]
        public void AProneCreatureCrawlsAtDoubleCostAndCannotStandWithASpeedOfZero()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin, x: 6);
            fight.Begin();
            hero.Apply(Condition.Prone);

            Turn turn = fight.Next();
            if (!ReferenceEquals(turn.Actor, hero)) { fight.EndTurn(); turn = fight.Next(); }

            int before = turn.Movement;
            fight.Walk(turn, new Cell(2, 1));
            Assert.Equal(before - 10, turn.Movement);

            hero.Apply(Condition.Grappled, goblin);
            Assert.False(turn.StandUp());
        }


        // --- Unconscious drops what it holds (SRD p.191) ------------------------------------------------

        [Fact]
        public void FallingUnconsciousDropsTheWeapon()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin);

            hero.Suffer(1000, DamageType.Slashing);
            fight.Hurt(goblin, hero, 1);

            Assert.True(hero.Has(Condition.Unconscious));
            Assert.True(hero.Disarmed);
            Assert.Equal(new Cell(1, 1), hero.DroppedAt);
        }


        // --- opportunity attacks (SRD p.15, p.185) ------------------------------------------------------

        [Fact]
        public void AnOpportunityAttackNeedsToSeeTheCreatureLeaving()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin);
            var swing = new OpportunityAttack(Combatants.Scimitar);

            Moment leaving = Moment.Leaving(hero, goblin, new Cell(1, 1), new Cell(0, 0));
            Assert.True(swing.CanAnswer(fight, goblin, leaving));

            goblin.Apply(Condition.Blinded);
            Assert.False(swing.CanAnswer(fight, goblin, leaving));
        }

        [Fact]
        public void ABowIsNoOpportunityAttack()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin);

            fight.ArmOpportunity(goblin, Combatants.Shortbow);

            Assert.Null(fight.OpportunityAttackOf(goblin));
        }


        // --- initiative is a Dexterity check (SRD p.13) ---------------------------------------------------

        [Fact]
        public void APoisonedCreatureRollsInitiativeAtDisadvantage()
        {
            var log = new LoggingResolver(new StandardResolver(new ScriptedRng(15, 3)));
            Actor goblin = Combatants.Goblin();
            goblin.Apply(Condition.Poisoned);

            Initiative.Roll(log, new[] { goblin });

            Assert.Equal(Advantage.Disadvantage, log.Attempts.Single().Roll.Advantage);
        }


        // --- ranged attacks in close combat (SRD p.15) -----------------------------------------------------

        [Fact]
        public void AnIncapacitatedOrBlindedNeighbourDoesNotCrowdARangedAttack()
        {
            Encounter fight = Room(out Actor hero, out Actor goblin);

            Assert.True(fight.Crowded(hero));

            goblin.Apply(Condition.Blinded);
            Assert.False(fight.Crowded(hero));

            goblin.Remove(Condition.Blinded);
            goblin.Apply(Condition.Incapacitated);
            Assert.False(fight.Crowded(hero));
        }


        // --- rests need at least 1 hit point (SRD p.185, p.187) ---------------------------------------------

        [Fact]
        public void ADownedCreatureCannotLongRest()
        {
            Actor hero = Combatants.Hero(hp: 40);
            hero.Suffer(1000, DamageType.Slashing);

            hero.LongRest();

            Assert.Equal(0, hero.Health.Current);
        }
    }
}
