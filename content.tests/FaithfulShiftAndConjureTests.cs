using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // shift and conjure: squares changed, and items made (ShiftHandler, ConjureHandler)
    public class FaithfulShiftAndConjureTests : FaithfulSpellFixture
    {
        [Fact]
        public void TelekinesisMovesAndHoldsACreatureAndOneTargetAtATime()
        {
            Assert.True(Faithful("telekinesis"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin, x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting grip = cast.Cast(wizard, Book.Find("telekinesis"),
                                     new Aim(new[] { goblin }, new Cell(9, 4)).Choosing("creature"),
                                     turn: mine, fight: fight);

            Assert.True(grip.Cast, grip.Refusal);
            Assert.Equal(new Cell(9, 4), fight.Field.Where(goblin));
            Assert.True(goblin.Has(Condition.Restrained));

            // a gargantuan creature is too big to move
            Actor titan = Goblin("titan");
            titan.Size = Size.Gargantuan;
            fight.Field.Place(titan, new Cell(5, 0));

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            mine = fight.Next();

            cast.Again(wizard, Book.Find("telekinesis"),
                       new Aim(new[] { titan }, new Cell(6, 0)).Choosing("creature"), mine, fight);

            Assert.Equal(new Cell(5, 0), fight.Field.Where(titan));
            Assert.False(goblin.Has(Condition.Restrained));
        }

        [Fact]
        public void GoodberryPutsTenBerriesInThePackThatHealAndVanish()
        {
            Assert.True(Faithful("goodberry"));

            Content.Schema.Library srd = Content.Schema.Library.Srd();
            var hero = new Content.Sheet.Hero("Wren", srd.Class("druid"), srd.Kind("human"),
                                              srd.Background("sage"),
                                              Content.Creation.Creation.Standard(srd.Class("druid")),
                                              3);
            hero.Build(spells: new[] { Book.Find("goodberry") });
            hero.Actor.Suffer(5, DamageType.Slashing);

            Casting berries = new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(hero.Caster, Book.Find("goodberry"), Aim.Nothing);

            hero.Receive(berries, srd.Items);
            Assert.Equal(10, hero.Pack.CountOf("goodberry"));

            int hp = hero.Actor.Health.Current;
            Assert.Equal(1, hero.Use("goodberry", new StandardResolver(new ScriptedRng(1))));
            Assert.Equal(hp + 1, hero.Actor.Health.Current);
            Assert.Equal(9, hero.Pack.CountOf("goodberry"));

            hero.LongRest();
            Assert.Equal(0, hero.Pack.CountOf("goodberry"));
        }

        [Fact]
        public void MistyStepMovesTheCasterAndThunderwavePushes()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("thunderwave"), Aim.Toward(Facing.East), turn: mine,
                      fight: fight);

            // a failed save: pushed 10 feet east of (3,2)
            Assert.Equal(new Cell(5, 2), fight.Field.Where(goblin));

            cast.Cast(wizard, Book.Find("misty_step"), Aim.On(new Cell(2, 4)), turn: mine,
                      fight: fight);
            Assert.Equal(new Cell(2, 4), fight.Field.Where(me));
        }
    }
}
