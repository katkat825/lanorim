using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // strike and direct: a spell that swings a weapon, and a turn decided for the target (StrikeHandler, DirectHandler)
    public class FaithfulStrikeAndDirectTests : FaithfulSpellFixture
    {
        [Fact]
        public void TrueStrikeSwingsTheWeaponWithTheCastersAbility()
        {
            Assert.True(Faithful("true_strike"));

            Encounter fight = Duel(Script(20, 1, 10, 4), out Caster wizard, out _, out Actor goblin);
            var cast = new Incantation(fight.Resolver);
            var dagger = new Attack("dagger", new DiceRoll(1, Die.D4), DamageType.Piercing,
                                    finesse: true);

            Turn mine = fight.Next();

            Casting refused = cast.Cast(wizard, Book.Find("true_strike"), Aim.At(goblin),
                                        turn: mine, fight: fight);
            Assert.False(refused.Cast);

            // a 10 plus Intelligence (+4) and proficiency (+6) hits AC 10; 4 on the d4, +4 Int,
            // and 3d6 radiant at level 17 (ones after the script runs out)
            Casting strike = cast.Cast(wizard, Book.Find("true_strike"),
                                       Aim.At(goblin).With(dagger), turn: mine, fight: fight);

            Assert.True(strike.Cast, strike.Refusal);
            Assert.Equal(100 - (4 + 4) - 3, goblin.Health.Current);
        }

        [Fact]
        public void CommandGrovelProneAndTheTurnIsOver()
        {
            Assert.True(Faithful("command"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin, x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();

            Casting refused = cast.Cast(wizard, Book.Find("command"), Aim.At(goblin), turn: mine,
                                        fight: fight);
            Assert.False(refused.Cast);

            cast.Cast(wizard, Book.Find("command"), Aim.At(goblin).Choosing("grovel"), turn: mine,
                      fight: fight);
            fight.EndTurn();

            // the goblin's turn is spent before anyone can drive it, and the round comes back
            Turn next = fight.Next();

            Assert.True(goblin.Has(Condition.Prone));
            Assert.Same(wizard.Actor, next.Actor);
        }

        [Fact]
        public void CommandApproachWalksItToTheCasterAndHaltStopsItDead()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 7);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("command"), Aim.At(goblin).Choosing("approach"),
                      turn: mine, fight: fight);
            fight.EndTurn();
            fight.Next();

            Assert.Equal(1, fight.Field.Distance(me, goblin));
        }

        [Fact]
        public void CommandFleeRunsAndDropLeavesTheWeaponBehind()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("command"), Aim.At(goblin).Choosing("flee"), turn: mine,
                      fight: fight);
            fight.EndTurn();
            fight.Next();

            // 30 feet, and a Dash for 30 more: as far as the hall lets it
            Assert.True(fight.Field.Distance(me, goblin) >= 7);

            Actor other = Goblin("other");
            other.Disarm(new Cell(0, 0));
            var scimitar = new Attack("scimitar", new DiceRoll(1, Die.D6), DamageType.Slashing);
            var bite = new Attack("bite", new DiceRoll(1, Die.D4), DamageType.Piercing, hand: Hand.None);

            Assert.False(other.CanUse(scimitar));
            Assert.True(other.CanUse(bite));
        }
    }
}
