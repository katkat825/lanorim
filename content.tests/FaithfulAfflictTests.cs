using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // afflict: a condition, and how it ends - repeat saves, worsening, damage, shaking, fleeing (AfflictHandler)
    public class FaithfulAfflictTests : FaithfulSpellFixture
    {
        [Fact]
        public void HoldPersonParalyzesAHumanoidAndLeavesAnythingElseAlone()
        {
            Assert.True(Faithful("hold_person"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor bandit = Goblin("bandit", tags: "humanoid");
            Actor wolf = Goblin("wolf", tags: "beast");

            cast.Cast(wizard, Book.Find("hold_person"), Aim.At(bandit, wolf), castAt: 3);

            Assert.True(bandit.Has(Condition.Paralyzed));
            Assert.False(wolf.Has(Condition.Paralyzed));
        }

        [Fact]
        public void HoldMonsterIsSavedOffAtTheEndOfTheTargetsTurn()
        {
            Assert.True(Faithful("hold_monster"));

            // initiative 20 and 1; the goblin fails the first save and makes the second
            Encounter fight = Duel(Script(20, 1, 1, 20), out Caster wizard, out _, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("hold_monster"), Aim.At(goblin), turn: mine, fight: fight);
            Assert.True(goblin.Has(Condition.Paralyzed));

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            Assert.False(goblin.Has(Condition.Paralyzed));
        }

        [Fact]
        public void SleepIncapacitatesThenPutsToSleepOnASecondFailure()
        {
            Assert.True(Faithful("sleep"));

            Encounter fight = Duel(Script(20, 1, 1, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting sleep = cast.Cast(wizard, Book.Find("sleep"), Aim.On(new Cell(6, 2)),
                                      turn: mine, fight: fight);

            Assert.True(sleep.Cast, sleep.Refusal);
            Assert.True(goblin.Has(Condition.Incapacitated));
            Assert.False(goblin.Has(Condition.Unconscious));

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            Assert.True(goblin.Has(Condition.Unconscious));
            Assert.True(goblin.Has(Condition.Prone));
            Assert.False(goblin.Has(Condition.Incapacitated));
        }

        [Fact]
        public void SleepEndsOnDamageAndHealingDoesNotEndIt()
        {
            Encounter fight = Duel(Script(20, 1, 1, 1), out Caster wizard, out Actor me,
                                   out Actor goblin, x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("sleep"), Aim.On(new Cell(6, 2)), turn: mine, fight: fight);

            goblin.Mend(5);
            Assert.True(goblin.Has(Condition.Incapacitated));

            goblin.Suffer(1, DamageType.Slashing);
            fight.Hurt(me, goblin, 1);

            Assert.False(goblin.Has(Condition.Incapacitated));
        }

        [Fact]
        public void ASleeperCanBeShakenAwakeByAnActionFromBesideIt()
        {
            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out _, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("sleep"), Aim.On(new Cell(3, 2)), turn: mine, fight: fight);

            Assert.True(goblin.Has(Condition.Incapacitated));
            Assert.True(cast.CanBeShaken(goblin));

            Assert.True(cast.Shake(fight, mine, goblin));
            Assert.False(goblin.Has(Condition.Incapacitated));
        }

        [Fact]
        public void SleepSparesTheCastersSideAndTheSleepless()
        {
            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            var friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(10));
            Actor elf = Goblin("elf", tags: "sleepless");
            Actor goblin = Goblin();

            cast.Cast(wizard, Book.Find("sleep"), Aim.At(friend, elf, goblin));

            Assert.False(friend.Has(Condition.Incapacitated));
            Assert.False(elf.Has(Condition.Incapacitated));
            Assert.True(goblin.Has(Condition.Incapacitated));
        }

        [Fact]
        public void HideousLaughterPinsTheTargetProneAndDamageCallsASaveWithAdvantage()
        {
            Assert.True(Faithful("hideous_laughter"));

            // the first save fails; the damage-triggered one rolls twice with advantage: 20, 20
            Encounter fight = Duel(Script(20, 1, 1, 20, 20), out Caster wizard, out Actor me,
                                   out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("hideous_laughter"), Aim.At(goblin), turn: mine,
                      fight: fight);

            Assert.True(goblin.Has(Condition.Prone));
            Assert.True(goblin.Has(Condition.Incapacitated));
            Assert.False(new Turn(goblin, new ActionBudget(), 1).StandUp());

            goblin.Suffer(1, DamageType.Slashing);
            fight.Hurt(me, goblin, 1);

            Assert.False(goblin.Has(Condition.Incapacitated));
            Assert.False(goblin.Has(Condition.Prone));
        }

        [Fact]
        public void HypnoticPatternCharmsIncapacitatesAndStopsUntilHurt()
        {
            Assert.True(Faithful("hypnotic_pattern"));

            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out Actor me,
                                   out Actor goblin, x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("hypnotic_pattern"), Aim.On(new Cell(6, 2)), turn: mine,
                      fight: fight);

            Assert.True(goblin.Has(Condition.Charmed));
            Assert.True(goblin.Has(Condition.Incapacitated));
            Assert.Equal(0, goblin.Moves);

            goblin.Suffer(1, DamageType.Slashing);
            fight.Hurt(me, goblin, 1);

            Assert.False(goblin.Has(Condition.Charmed));
            Assert.False(goblin.Has(Condition.Incapacitated));
            Assert.True(goblin.Moves > 0);
        }

        [Fact]
        public void ACharmedCreatureCannotAttackItsCharmerAndTheCharmBreaksOnTheCastersDamage()
        {
            Assert.True(Faithful("charm_person"));

            // the goblin saves with advantage because it is being fought: two ones
            Encounter fight = Duel(Script(20, 1, 1, 1), out Caster wizard, out Actor me,
                                   out Actor goblin, tags: "humanoid");
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("charm_person"), Aim.At(goblin), turn: mine, fight: fight);

            Assert.True(goblin.HasFrom(Condition.Charmed, me));

            fight.EndTurn();
            Turn theirs = fight.Next();

            var claw = new Attack("claw", new DiceRoll(1, Die.D4), DamageType.Slashing);
            Assert.Null(fight.Hit(theirs, me, claw));

            // somebody on the goblin's own side hurting it changes nothing; the wizard does
            fight.Hurt(Goblin("other"), goblin, 1);
            Assert.True(goblin.Has(Condition.Charmed));

            fight.Hurt(me, goblin, 1);
            Assert.False(goblin.Has(Condition.Charmed));
        }

        [Fact]
        public void FleshToStonePetrifiesOnTheThirdFailure()
        {
            Assert.True(Faithful("flesh_to_stone"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _,
                                   out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("flesh_to_stone"), Aim.At(goblin), turn: mine,
                      fight: fight);
            Assert.True(goblin.Has(Condition.Restrained));

            // the goblin's turn: its second failed save (1)
            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            Assert.True(goblin.Has(Condition.Restrained));

            // a round later: the third
            fight.Next();
            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            Assert.True(goblin.Has(Condition.Petrified));
            Assert.False(goblin.Has(Condition.Restrained));
        }

        [Fact]
        public void FleshToStoneOnASuccessOrAConstructOnlyStopsItMoving()
        {
            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor golem = Goblin("golem", tags: "construct");

            cast.Cast(wizard, Book.Find("flesh_to_stone"), Aim.At(golem));

            Assert.False(golem.Has(Condition.Restrained));
            Assert.Equal(0, golem.Moves);
        }

        [Fact]
        public void PowerWordStunStunsAtOneHundredFiftyAndSlowsAnythingTougher()
        {
            Assert.True(Faithful("power_word_stun"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor weak = Goblin("weak", hp: 150);
            Actor strong = Goblin("strong", hp: 151);

            cast.Cast(wizard, Book.Find("power_word_stun"), Aim.At(weak));
            cast.Cast(wizard, Book.Find("power_word_stun"), Aim.At(strong));

            Assert.True(weak.Has(Condition.Stunned));
            Assert.False(strong.Has(Condition.Stunned));
            Assert.Equal(0, strong.Moves);
        }

        [Fact]
        public void SunbeamBlindsUntilTheStartOfTheCastersNextTurn()
        {
            Assert.True(Faithful("sunbeam"));

            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting beam = cast.Cast(wizard, Book.Find("sunbeam"), Aim.Toward(Facing.East),
                                     turn: mine, fight: fight);

            Assert.True(beam.Cast, beam.Refusal);
            Assert.True(goblin.Has(Condition.Blinded));

            fight.EndTurn();
            fight.Next();
            Assert.True(goblin.Has(Condition.Blinded));
            fight.EndTurn();

            fight.Next();
            Assert.False(goblin.Has(Condition.Blinded));
        }

        [Fact]
        public void BlindnessDeafnessAsksWhichAndBlindsOnBlindness()
        {
            Assert.True(Faithful("blindness_deafness"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));
            Actor goblin = Goblin();

            Casting refused = cast.Cast(wizard, Book.Find("blindness_deafness"), Aim.At(goblin));
            Assert.False(refused.Cast);

            cast.Cast(wizard, Book.Find("blindness_deafness"), Aim.At(goblin).Choosing("deafness"));
            Assert.True(goblin.Has(Condition.Deafened));
            Assert.False(goblin.Has(Condition.Blinded));

            Actor other = Goblin("other");
            cast.Cast(wizard, Book.Find("blindness_deafness"), Aim.At(other).Choosing("blindness"));
            Assert.True(other.Has(Condition.Blinded));
        }

        [Fact]
        public void FearDropsTheWeaponAndDrivesTheFrightenedAway()
        {
            Assert.True(Faithful("fear"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("fear"), Aim.Toward(Facing.East), turn: mine, fight: fight);

            Assert.True(goblin.Has(Condition.Frightened));
            Assert.True(goblin.Disarmed);

            fight.EndTurn();
            fight.Next();

            Assert.True(fight.Field.Distance(me, goblin) > 1);
        }
    }
}
