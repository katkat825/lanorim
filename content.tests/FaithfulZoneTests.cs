using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // zone: areas that persist - clouds, walls, cages, a globe, darkness (ZoneHandler)
    public class FaithfulZoneTests : FaithfulSpellFixture
    {
        [Fact]
        public void BlackTentaclesGrabWhoeverIsThereWhenTheyAppear()
        {
            Assert.True(Faithful("black_tentacles"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 7);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("black_tentacles"), Aim.On(new Cell(7, 2)), turn: mine,
                      fight: fight);

            Assert.True(goblin.Has(Condition.Restrained));
            Assert.Equal(97, goblin.Health.Current);
        }

        [Fact]
        public void TheGlobeStopsALowSpellCastFromOutside()
        {
            Assert.True(Faithful("globe_of_invulnerability"));

            Encounter fight = Duel(Script(20, 1, 20), out Caster wizard, out Actor me,
                                   out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("globe_of_invulnerability"), Aim.Nothing, turn: mine,
                      fight: fight);

            // an enemy mage outside the globe, which is 10 feet around (2,2)
            Caster enemy = Wizard(out Actor them);
            fight.Field.Place(them, new Cell(9, 2));

            Casting bolt = cast.Cast(enemy, Book.Find("fire_bolt"), Aim.At(me), fight: fight);
            Assert.True(bolt.Cast, bolt.Refusal);
            Assert.False(bolt.Landings.Single().Landed);
            Assert.Equal(80, me.Health.Current);
        }

        [Fact]
        public void DarknessBlocksSightAndYieldsOnlyToTruesight()
        {
            Assert.True(Faithful("fog_cloud"));
            Assert.True(Faithful("darkness"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 8);
            var cast = new Incantation(fight.Resolver);

            Assert.True(fight.Sees(me, goblin));

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("darkness"), Aim.On(new Cell(5, 2)), turn: mine,
                      fight: fight);

            // neither sees the other, so the two leans cancel: SRD's fog-fight
            Assert.False(fight.Sees(me, goblin));
            Assert.True(Strike.Lean(me, goblin, false, fight.Sees(me, goblin),
                                    fight.Sees(goblin, me)).IsFlat());

            me.Boons.Add(Boon.Of(new BoonSpec { Duration = Duration.Rest, Truesight = true }, "true_seeing"));
            Assert.True(fight.Sees(me, goblin));
        }

        [Fact]
        public void SunburstBurnsAwayADarkness()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out _, x: 8);
            var cast = new Incantation(fight.Resolver);
            Caster enemy = Wizard(out Actor them);
            fight.Field.Place(them, new Cell(10, 4));

            cast.Cast(enemy, Book.Find("darkness"), Aim.On(new Cell(8, 2)), fight: fight);
            Assert.Single(fight.Zones);
            Assert.True(them.IsConcentrating);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("sunburst"), Aim.On(new Cell(8, 3)), turn: mine,
                      fight: fight);

            Assert.Empty(fight.Zones);
            Assert.False(them.IsConcentrating);
        }

        [Fact]
        public void StinkingCloudTakesTheActionsOfWhoeverStartsATurnInIt()
        {
            Assert.True(Faithful("stinking_cloud"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 8);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("stinking_cloud"), Aim.On(new Cell(9, 2)), turn: mine,
                      fight: fight);
            fight.EndTurn();

            Turn theirs = fight.Next();

            Assert.True(goblin.Has(Condition.Poisoned));
            Assert.False(theirs.Can(Spend.Action));
            Assert.False(theirs.Can(Spend.Bonus));

            fight.EndTurn();
            Assert.False(goblin.Has(Condition.Poisoned));
        }

        [Fact]
        public void SleetStormKnocksDownAndBreaksConcentration()
        {
            Assert.True(Faithful("sleet_storm"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 8);
            var cast = new Incantation(fight.Resolver);

            // the goblin is holding a Bless when the storm lands on it
            Caster enemy = Wizard(out Actor them);
            fight.Field.Remove(goblin);
            var enemyCaster = new Caster(goblin, Ability.Wisdom,
                                         SpellSlots.For(CasterProgression.Full, 5));
            enemyCaster.Learn(Book.Find("bless"));
            fight.Field.Place(goblin, new Cell(8, 2));
            cast.Cast(enemyCaster, Book.Find("bless"), Aim.At(goblin), fight: fight);
            Assert.True(goblin.IsConcentrating);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("sleet_storm"), Aim.On(new Cell(9, 2)), turn: mine,
                      fight: fight);
            fight.EndTurn();

            // the goblin starts its turn in it: a failed Dex save, prone and no concentration
            fight.Next();
            Assert.True(goblin.Has(Condition.Prone));
            Assert.False(goblin.IsConcentrating);
            Assert.True(fight.IsRough(new Cell(9, 2), goblin));
        }

        [Fact]
        public void CloudkillDriftsAwayFromItsCaster()
        {
            Assert.True(Faithful("cloudkill"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out _, x: 10);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("cloudkill"), Aim.On(new Cell(4, 2)), turn: mine,
                      fight: fight);

            SpellZone cloud = cast.ZonesOf(wizard.Actor).Single();
            Assert.Equal(new Cell(4, 2), cloud.Centre);

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            fight.Next();

            Assert.Equal(new Cell(6, 2), cloud.Centre);
        }

        [Fact]
        public void FlamingSphereRollsIntoACreatureAndStops()
        {
            Assert.True(Faithful("flaming_sphere"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 7);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();

            Casting taken = cast.Cast(wizard, Book.Find("flaming_sphere"), Aim.On(new Cell(7, 2)),
                                      turn: mine, fight: fight);
            Assert.False(taken.Cast);

            cast.Cast(wizard, Book.Find("flaming_sphere"), Aim.On(new Cell(4, 2)), turn: mine,
                      fight: fight);
            Assert.Equal(100, goblin.Health.Current);

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            mine = fight.Next();

            // rolled at a square past the goblin: it stops at the goblin, which saves (a one)
            cast.Again(wizard, Book.Find("flaming_sphere"), Aim.On(new Cell(9, 2)), mine, fight);

            SpellZone sphere = cast.ZonesOf(wizard.Actor).Single();
            Assert.Equal(new Cell(6, 2), sphere.Centre);
            Assert.Equal(98, goblin.Health.Current);
        }

        [Fact]
        public void WallOfFireBurnsItsChosenSideAndNotTheOther()
        {
            Assert.True(Faithful("wall_of_fire"));

            // a wall running south from (5,0) for 12 squares (clipped to 5); the burning side is
            // left of south - east. a goblin two squares east at (7,2), another west at (3,2)
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 7);
            var cast = new Incantation(fight.Resolver);
            Actor west = Goblin("west");
            fight.Field.Place(west, new Cell(3, 1));

            Turn mine = fight.Next();
            Casting wall = cast.Cast(wizard, Book.Find("wall_of_fire"),
                                     new Aim(null, new Cell(5, 0), Facing.South).Choosing("line")
                                         .On(Side.Left),
                                     turn: mine, fight: fight);

            Assert.True(wall.Cast, wall.Refusal);

            // opaque: the wizard at (2,2) no longer sees the goblin at (7,2)
            Assert.False(fight.Sees(me, goblin));

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            // 5d8 of ones, no save, for ending its turn within 10 feet of the burning side
            Assert.Equal(95, goblin.Health.Current);
            Assert.Equal(100, west.Health.Current);
        }

        [Fact]
        public void BladeBarrierGivesCoverAndCutsWhoeverStandsInIt()
        {
            Assert.True(Faithful("blade_barrier"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 5);
            var cast = new Incantation(fight.Resolver);
            Actor behind = Goblin("behind");
            fight.Field.Place(behind, new Cell(8, 2));

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("blade_barrier"),
                      new Aim(null, new Cell(5, 0), Facing.South).Choosing("line"), turn: mine,
                      fight: fight);

            // the goblin standing in it when it appeared failed its save: 6d10 of ones
            Assert.Equal(94, goblin.Health.Current);
            Assert.Equal(5, fight.Cover(me, behind));
            Assert.Equal(0, fight.Cover(me, goblin));
            Assert.True(fight.IsRough(new Cell(5, 3), behind));
        }

        [Fact]
        public void AForcecageTrapsWhoeverIsInsideAndMagicOutNeedsACharismaSave()
        {
            Assert.True(Faithful("forcecage"));

            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out Actor me,
                                   out Actor goblin, x: 7);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting cage = cast.Cast(wizard, Book.Find("forcecage"),
                                     Aim.On(new Cell(7, 2)).Choosing("cage"), turn: mine,
                                     fight: fight);

            Assert.True(cage.Cast, cage.Refusal);

            // bars: seen through, never walked through
            Assert.True(fight.Sees(me, goblin));
            Assert.Null(fight.Field.RouteFor(goblin, new Cell(2, 4)));

            // a trapped caster's Misty Step out fails its Charisma save (a one)
            Caster trapped = Wizard(out Actor inside);
            fight.Field.Place(inside, new Cell(8, 3));

            Casting step = cast.Cast(trapped, Book.Find("misty_step"), Aim.On(new Cell(10, 3)),
                                     fight: fight);

            Assert.Equal(new Cell(8, 3), fight.Field.Where(inside));
            Assert.False(step.Landings.Single().Landed);
        }

        [Fact]
        public void ABurstDoesNotGoRoundAWall()
        {
            // a wall between (4,*) and (5,*) down the whole map
            const string walled = @"
+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . .|. . . . . .|
+ + + + + + + + + + + +
|. . . . .|. . . . . .|
+ + + + + + + + + + + +
|. . . . .|. . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+";

            Assert.True(MapReader.TryRead(walled, out MapLayout map, out string problem), problem);
            var field = new Battlefield(map);
            Actor near = Goblin("near");
            Actor beyond = Goblin("beyond");
            field.Place(near, new Cell(3, 1));
            field.Place(beyond, new Cell(5, 1));

            List<Actor> caught = field.Caught(new Cell(4, 1), 2).ToList();

            Assert.Contains(near, caught);
            Assert.DoesNotContain(beyond, caught);
        }
    }
}
