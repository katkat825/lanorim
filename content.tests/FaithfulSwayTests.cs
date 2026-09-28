using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // sway: a boon on the creature, Aid's raised maximum, Banishment's trip away (SwayHandler)
    public class FaithfulSwayTests : FaithfulSpellFixture
    {
        [Fact]
        public void AidRaisesTheMaximumAndTheCurrentAndLastsUntilALongRest()
        {
            Assert.True(Faithful("aid"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor friend = Goblin("friend", hp: 20);
            friend.Suffer(10, DamageType.Slashing);

            cast.Cast(wizard, Book.Find("aid"), Aim.At(friend), castAt: 3);

            Assert.Equal(30, friend.Health.Maximum);
            Assert.Equal(20, friend.Health.Current);

            friend.ShortRest();
            Assert.Equal(30, friend.Health.Maximum);

            friend.LongRest();
            Assert.Equal(20, friend.Health.Maximum);
            Assert.Equal(20, friend.Health.Current);
        }

        [Fact]
        public void DeathWardTurnsTheFirstDropToZeroIntoOneAndStopsAPowerWordKill()
        {
            Assert.True(Faithful("death_ward"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor friend = Goblin("friend", hp: 30);
            cast.Cast(wizard, Book.Find("death_ward"), Aim.At(friend));

            friend.Suffer(999, DamageType.Necrotic);
            Assert.Equal(1, friend.Health.Current);

            friend.Suffer(999, DamageType.Necrotic);
            Assert.True(friend.IsDown);

            Actor other = Goblin("other", hp: 50);
            cast.Cast(wizard, Book.Find("death_ward"), Aim.At(other));
            cast.Cast(wizard, Book.Find("power_word_kill"), Aim.At(other));

            Assert.Equal(50, other.Health.Current);
            Assert.Null(other.Boons.DeathWard);
        }

        [Fact]
        public void RaiseDeadBringsTheDeadBackWithAPenaltyThatEasesEachLongRest()
        {
            Assert.True(Faithful("raise_dead"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            var friend = new Actor("friend", 5, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(30));
            friend.Perish();

            cast.Cast(wizard, Book.Find("raise_dead"), Aim.At(friend));

            Assert.False(friend.IsDead);
            Assert.Equal(1, friend.Health.Current);
            Assert.Equal(-4, friend.Boons.FlatOnSave(Ability.Wisdom));

            friend.LongRest();
            Assert.Equal(-3, friend.Boons.FlatOnSave(Ability.Wisdom));

            friend.LongRest();
            friend.LongRest();
            friend.LongRest();
            Assert.Equal(0, friend.Boons.FlatOnSave(Ability.Wisdom));
        }

        [Fact]
        public void TrueSeeingSeesTheInvisible()
        {
            Assert.True(Faithful("true_seeing"));

            Caster wizard = Wizard(out Actor me);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor ghost = Goblin("ghost");
            ghost.Apply(Condition.Invisible);

            Assert.False(me.CanSee(ghost));

            cast.Cast(wizard, Book.Find("true_seeing"), Aim.At(me));

            Assert.True(me.CanSee(ghost));
        }

        [Fact]
        public void MirrorImageDuplicatesTakeHitsOnAThreeOrBetter()
        {
            Assert.True(Faithful("mirror_image"));

            // a hit (15) against the wizard rolls three d6 - a 3 among them
            Caster wizard = Wizard(out Actor me);
            var resolver = new StandardResolver(new ScriptedRng(15, 1, 1, 3));
            var cast = new Incantation(resolver);

            cast.Cast(wizard, Book.Find("mirror_image"), Aim.Nothing);
            Assert.Equal(3, me.Boons.Decoy.Decoys);

            Actor goblin = Goblin();
            var claw = new Attack("claw", new DiceRoll(1, Die.D4), DamageType.Slashing);

            Attempt hit = Strike.Roll(resolver, goblin, me, claw);
            Attempt landed = Strike.Decoyed(resolver, goblin, me, hit);

            Assert.True(hit.Succeeded);
            Assert.False(landed.Succeeded);
            Assert.Equal(2, me.Boons.Decoy.Decoys);
        }

        [Fact]
        public void HasteDoublesSpeedAddsANarrowActionAndLeavesLethargyWhenItEnds()
        {
            Assert.True(Faithful("haste"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 8);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("haste"), Aim.At(me), turn: mine, fight: fight);

            Assert.Equal(60, me.Moves);
            Assert.True(me.Boons.LimitedAction);

            // the next turn: two ordinary actions and the narrow one
            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            mine = fight.Next();

            Assert.Equal(1, mine.Limited);
            Assert.True(fight.Dash(mine));
            Assert.True(fight.Dash(mine));
            Assert.True(fight.Dash(mine));
            Assert.False(fight.Dash(mine));

            cast.Release(me);

            Assert.True(me.Has(Condition.Incapacitated));
            Assert.Equal(0, me.Moves);
        }

        [Fact]
        public void ShillelaghRewritesAClubToTheCastersAbilityAndDie()
        {
            Assert.True(Faithful("shillelagh"));

            var druid = new Actor("druid", 5, new AbilityScores(8, 10, 10, 10, 18, 10),
                                  Allegiance.Hero);
            druid.SetHealth(new Health(30));
            var caster = new Caster(druid, Ability.Wisdom,
                                    SpellSlots.For(CasterProgression.Full, 5));
            caster.Learn(Book.Find("shillelagh"));

            var club = new Attack("club", new DiceRoll(1, Die.D4), DamageType.Bludgeoning);
            Assert.Equal(Ability.Strength, club.AbilityFor(druid));

            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(caster, Book.Find("shillelagh"), Aim.Nothing);

            Assert.Equal(Ability.Wisdom, club.AbilityFor(druid));
            Assert.Equal(Die.D10, club.DamageFor(druid).Die);

            // a creature that resists bludgeoning takes the force instead
            Actor skeleton = Goblin("skeleton");
            skeleton.SetDefense(DamageType.Bludgeoning, Defense.Resistant);
            Assert.Equal(DamageType.Force, club.DamageTypeFor(druid, skeleton));
        }

        [Fact]
        public void EnlargeAndReduceAreModesAndOnlyTheUnwillingSave()
        {
            Assert.True(Faithful("enlarge_reduce"));

            Caster wizard = Wizard(out Actor me);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(20)));

            // willing: no save, even on a 20
            cast.Cast(wizard, Book.Find("enlarge_reduce"), Aim.At(me).Choosing("enlarge"));
            Assert.Equal(Size.Large, me.CurrentSize);
            Assert.Equal(Advantage.Advantage, me.SaveAdvantage(Ability.Strength));

            // unwilling: the goblin saves on the 20
            Actor goblin = Goblin();
            cast.Cast(wizard, Book.Find("enlarge_reduce"), Aim.At(goblin).Choosing("reduce"));
            Assert.Equal(Size.Medium, goblin.CurrentSize);
        }

        [Fact]
        public void PassWithoutTraceIsOnWhoeverStandsInTheAura()
        {
            Assert.True(Faithful("pass_without_trace"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            var friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(10));
            fight.Field.Place(friend, new Cell(10, 4));

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("pass_without_trace"), Aim.Nothing, turn: mine,
                      fight: fight);

            Assert.Equal(10, me.Boons.FlatOnCheck(Skill.Stealth));
            Assert.Equal(0, goblin.Boons.FlatOnCheck(Skill.Stealth));

            // off the board it is simply on the caster
            Caster alone = Wizard(out Actor sneak);
            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(alone, Book.Find("pass_without_trace"), Aim.Nothing);
            Assert.Equal(10, sneak.Boons.FlatOnCheck(Skill.Stealth));
        }
    }
}
