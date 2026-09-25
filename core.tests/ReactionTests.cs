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
    // a Shield with no spell behind it: when hit, +5 armor class until the start of your next
    // turn. core has to be able to test its own window without the content layer's spell files
    sealed class Brace : IReaction
    {
        public int Answered { get; private set; }

        public string Id => "brace";

        public Trigger Trigger => Trigger.Hit;

        public int Deflects => 5;

        public bool CanAnswer(Encounter fight, Actor reactor, Moment moment) =>
            ReferenceEquals(moment.Target, reactor);

        public void Answer(Encounter fight, Actor reactor, Moment moment)
        {
            Answered++;
            reactor.Boons.Add(new Boon("brace", "brace", Duration.NextTurn, armorClass: 5));
        }
    }

    // answers anything of one trigger and remembers what it was shown
    sealed class Watcher : IReaction
    {
        public Watcher(Trigger trigger, bool stops = false)
        {
            Trigger = trigger;
            _stops = stops;
        }

        readonly bool _stops;

        public List<Moment> Seen { get; } = new List<Moment>();

        public string Id => "watcher_" + Trigger.Id();

        public Trigger Trigger { get; }

        public int Deflects => 0;

        public bool CanAnswer(Encounter fight, Actor reactor, Moment moment) => true;

        public void Answer(Encounter fight, Actor reactor, Moment moment)
        {
            Seen.Add(moment);

            if (_stops) moment.Stop();
        }
    }

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

    public class TurnShapedBoonTests
    {
        [Fact]
        public void UntilTheEndOfTheOwnersNextTurnSurvivesTheTurnItWasPutOnIn()
        {
            var caster = Combatants.Hero();
            var target = Combatants.Goblin();

            // put on during the caster's own turn: that turn's end is not the one that counts
            target.Boons.Add(new Boon("glimmer", "glimmer", Duration.NextTurnEnd,
                                      advantageAgainst: true) { Owner = caster });

            target.Boons.TurnEnding(caster, target);
            Assert.True(target.Boons.Has("glimmer"));

            // the target's own turn is not the owner's
            target.Boons.TurnStarting(target, target);
            target.Boons.TurnEnding(target, target);
            Assert.True(target.Boons.Has("glimmer"));

            target.Boons.TurnStarting(caster, target);
            Assert.True(target.Boons.Has("glimmer"));

            target.Boons.TurnEnding(caster, target);
            Assert.False(target.Boons.Has("glimmer"));
        }

        [Fact]
        public void AOnceBoonAgainstIsSpentByTheNextAttackAtItsBearer()
        {
            var attacker = Combatants.Hero();
            var target = Combatants.Goblin();

            target.Boons.Add(new Boon("glimmer", "glimmer", Duration.Encounter,
                                      advantageAgainst: true) { Once = true });

            Assert.Equal(Advantage.Advantage, target.AdvantageAgainstMe);

            Strike.Roll(new StandardResolver(new ScriptedRng(10, 10)), attacker, target,
                        Combatants.Longsword);

            Assert.Equal(Advantage.Flat, target.AdvantageAgainstMe);
        }

        [Fact]
        public void AMarkPaysOutOnlyOnItsOwnersHits()
        {
            var owner = Combatants.Hero();
            var stranger = Combatants.Hero();
            var target = Combatants.Goblin(hp: 100);
            target.SetHealth(new Health(100));

            target.Boons.Add(new Boon("hex", "hex", Duration.Concentration)
            {
                Owner = owner,
                Mark = DiceRoll.Parse("1d6"),
                MarkType = DamageType.Necrotic,
            });

            // a hit for 5 on the longsword, and 6 on the mark
            Blow marked = Strike.Make(new StandardResolver(new ScriptedRng(19, 5, 6)), owner,
                                      target, Combatants.Longsword);

            Assert.Equal(5 + 4 + 6, marked.Suffered);

            Blow plain = Strike.Make(new StandardResolver(new ScriptedRng(19, 5, 6)), stranger,
                                     target, Combatants.Longsword);

            Assert.Equal(5 + 4, plain.Suffered);
        }
    }

    public class FleeingTests
    {
        // the middle of the left side is open: the outline has a gap at (0, 1)
        const string Gap = @"
+-+-+-+-+-+-+-+
|@ . . . . . .|
+ + + + + + + +
 . . . . . . .|
+ + + + + + + +
|. . . . . . .|
+-+-+-+-+-+-+-+";

        static Encounter Start(string map, out Actor hero, out Actor goblin, params int[] rolls)
        {
            var field = new Battlefield(Rooms.Read(map));
            var fight = new Encounter(new StandardResolver(new ScriptedRng(rolls)), field);

            hero = Combatants.Hero();
            goblin = Combatants.Goblin();

            fight.Enlist(hero, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(6, 1));
            fight.Begin();

            return fight;
        }

        [Fact]
        public void AGapInTheOutlineIsAWayOut()
        {
            var field = new Battlefield(Rooms.Read(Gap));

            Assert.Equal(new[] { new Cell(0, 1) }, field.Exits);
        }

        [Fact]
        public void AClosedRoomHasNoWayOut()
        {
            Assert.Empty(new Battlefield(Rooms.Read(Rooms.Open)).Exits);
        }

        [Fact]
        public void StandingOnTheWayOutTheHeroCanFleeAndTheFightEndsAsFled()
        {
            Encounter fight = Start(Gap, out Actor hero, out _, 10, 1);

            Turn turn = fight.Next();
            Assert.Same(hero, turn.Actor);

            Assert.False(fight.CanFlee(turn));

            fight.Walk(turn, new Cell(0, 1));

            Assert.True(fight.Flee(turn));
            Assert.Equal(Outcome.Fled, fight.Outcome);
            Assert.Null(fight.Next());
        }

        [Fact]
        public void AwayFromTheEdgeThereIsNoFleeing()
        {
            Encounter fight = Start(Gap, out _, out _, 10, 1);

            Turn turn = fight.Next();

            Assert.False(fight.Flee(turn));
            Assert.Equal(Outcome.Open, fight.Outcome);
        }

        [Fact]
        public void AMonsterDoesNotFleeByTheRules()
        {
            Encounter fight = Start(Gap, out _, out Actor goblin, 1, 20);

            Turn turn = fight.Next();
            Assert.Same(goblin, turn.Actor);

            fight.Walk(turn, new Cell(6, 0));

            Assert.False(fight.CanFlee(turn));
        }
    }
}
