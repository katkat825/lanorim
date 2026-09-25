using System.Collections.Generic;
using System.Linq;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Tests
{
    // the zone primitive, which until tonight was a marker that did nothing: Spirit Guardians, Web,
    // Entangle, Moonbeam, Spike Growth and Ice Storm, held to what SRD 5.2.1 says they do
    public class ZoneTests
    {
        static readonly SpellBook Book = SpellBook.Srd();

        // eleven by five, all floor
        const string Hall = @"
+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+";

        static Encounter Field(IRng rng, ICombatObserver observer = null)
        {
            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);

            return new Encounter(new StandardResolver(rng), new Battlefield(map),
                                 observer ?? new CombatLog());
        }

        static Caster Cleric(out Actor actor, int level = 9)
        {
            actor = new Actor("cleric", level, new AbilityScores(10, 10, 14, 10, 18, 10),
                              Allegiance.Hero);
            actor.SetHealth(new Health(60, Die.D8, level));
            actor.Speed = 30;

            var caster = new Caster(actor, Ability.Wisdom,
                                    SpellSlots.For(CasterProgression.Full, level));

            foreach (Spell spell in Book.All) caster.Learn(spell);

            return caster;
        }

        static Actor Goblin(string id, int hp = 100)
        {
            var goblin = new Actor(id, 1, new AbilityScores());
            goblin.SetHealth(new Health(hp));
            goblin.Speed = 30;
            return goblin;
        }

        // --- Spirit Guardians --------------------------------------------------------------------

        // SRD 5.2.1: radiant for a good or neutral caster, necrotic for an evil one - picked at the
        // cast in v1
        static readonly Aim Radiant = Aim.Nothing.Choosing(DamageType.Radiant);

        [Fact]
        public void SpiritGuardiansIsFaithfulNow()
        {
            Spell guardians = Book.Find("spirit_guardians");

            Assert.False(guardians.Approximated);
            Assert.Contains(guardians.Effects, e => e.Kind == Primitive.Zone &&
                                                    e.SparesAllies && e.Reach == Reach.Around);
            // "any other creature's Speed is halved in the Emanation" (SRD p.164) - an aura, not
            // difficult terrain
            Assert.Contains(guardians.Effects, e => e.WhileInside && e.SpeedChange == SpeedChange.Half);
            Assert.Contains(guardians.Effects, e => e.ChosenDamageType &&
                                                    e.DamageChoices.Contains(DamageType.Necrotic));
            Assert.Contains(guardians.Effects, e => e.Reach == Reach.Zone &&
                                                    e.Pulses == (Pulses.Enter | Pulses.EndTurn));
        }

        [Fact]
        public void SpiritGuardiansDoesNothingOnTheCastAndBitesWhenACreatureEndsItsTurnInside()
        {
            // initiative: the cleric (20) first, then the goblin (1). the goblin's save is a 1:
            // 3d8 of 4s. (a script repeats from the top when it runs out, so every die is in it)
            Encounter fight = Field(new ScriptedRng(20, 1, 1, 4, 4, 4));
            Caster cleric = Cleric(out Actor me);
            Actor goblin = Goblin("goblin");

            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(goblin, new Cell(4, 2));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting result = cast.Cast(cleric, Book.Find("spirit_guardians"), Radiant,
                                       turn: mine, fight: fight);

            Assert.True(result.Cast, result.Refusal);
            Assert.Single(fight.Zones);
            Assert.Equal(100, goblin.Health.Current);

            fight.EndTurn();

            Turn theirs = fight.Next();
            Assert.Same(goblin, theirs.Actor);

            fight.EndTurn();

            Assert.Equal(100 - 12, goblin.Health.Current);
        }

        [Fact]
        public void AnEmanationThatMovesOntoACreatureBitesItOncePerTurn()
        {
            // cleric first. the goblin's save is a 1 (3d8 of 4s) as the cleric walks up, and there
            // is nothing more that turn however far the cleric walks
            Encounter fight = Field(new ScriptedRng(20, 1, 1, 4, 4, 4));
            Caster cleric = Cleric(out Actor me);
            Actor goblin = Goblin("goblin");

            fight.Enlist(me, new Cell(0, 2));
            fight.Enlist(goblin, new Cell(8, 2));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            cast.Cast(cleric, Book.Find("spirit_guardians"), Radiant, turn: mine, fight: fight);

            // five squares east brings the goblin inside three squares of the cleric
            fight.Walk(mine, new Cell(5, 2));

            Assert.Equal(100 - 12, goblin.Health.Current);

            fight.Walk(mine, new Cell(6, 2));

            Assert.Equal(100 - 12, goblin.Health.Current);
        }

        [Fact]
        public void SpiritGuardiansSparesTheCastersSide()
        {
            Encounter fight = Field(new ScriptedRng(20, 1, 1, 4));
            Caster cleric = Cleric(out Actor me);

            var friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(20));

            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(friend, new Cell(3, 2));
            fight.Enlist(Goblin("goblin"), new Cell(10, 4));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            cast.Cast(cleric, Book.Find("spirit_guardians"), Radiant, turn: fight.Next(),
                      fight: fight);

            IZone zone = fight.Zones.Single();

            Assert.DoesNotContain(friend, fight.CaughtIn(zone));
            Assert.Equal(30, friend.Moves);
        }

        [Fact]
        public void WalkingIntoSpiritGuardiansHalvesTheRestOfTheMove()
        {
            Encounter fight = Field(new ScriptedRng(20, 1, 20));
            Caster cleric = Cleric(out Actor me);
            Actor goblin = Goblin("goblin");

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(goblin, new Cell(8, 0));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            cast.Cast(cleric, Book.Find("spirit_guardians"), Radiant, turn: fight.Next(),
                      fight: fight);
            fight.EndTurn();

            Turn theirs = fight.Next();
            Assert.Same(goblin, theirs.Actor);

            // from (8,0) toward (2,0): squares 7 to 4 are outside the 3-square emanation (20 feet
            // used), square 3 is inside (25 used) and the speed halves to 15 - SRD's rule for a
            // speed changing mid-move leaves 15 less 25 used, which is nothing
            fight.Walk(theirs, new Cell(2, 0));

            Assert.Equal(new Cell(3, 0), fight.Field.Where(goblin));
            Assert.Equal(0, theirs.Movement);
        }

        [Fact]
        public void ACreatureStartingItsTurnInSpiritGuardiansHasHalfItsSpeed()
        {
            Encounter fight = Field(new ScriptedRng(20, 1, 20));
            Caster cleric = Cleric(out Actor me);
            Actor goblin = Goblin("goblin");

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(goblin, new Cell(2, 0));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            cast.Cast(cleric, Book.Find("spirit_guardians"), Radiant, turn: fight.Next(),
                      fight: fight);
            fight.EndTurn();

            Turn theirs = fight.Next();
            Assert.Same(goblin, theirs.Actor);
            Assert.Equal(15, theirs.Movement);
        }

        [Fact]
        public void DroppingConcentrationTakesTheZoneOffTheBoard()
        {
            Encounter fight = Field(new ScriptedRng(20, 1));
            Caster cleric = Cleric(out Actor me);

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(Goblin("goblin"), new Cell(10, 4));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            cast.Cast(cleric, Book.Find("spirit_guardians"), Radiant, turn: fight.Next(),
                      fight: fight);

            Assert.Single(fight.Zones);

            cast.Release(me);

            Assert.Empty(fight.Zones);
        }


        // --- Web and Entangle: restrained, and breaking free ------------------------------------

        [Fact]
        public void WebRestrainsWhoeverWalksInAndAStrengthCheckBreaksIt()
        {
            // cleric first; the goblin walks into the web and fails its Dex save (1); on its next
            // turn it spends an action and rolls 20 on Athletics
            Encounter fight = Field(new ScriptedRng(20, 1, 1, 20));
            Caster cleric = Cleric(out Actor me);
            Actor goblin = Goblin("goblin");

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(goblin, new Cell(10, 2));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);

            Casting web = cast.Cast(cleric, Book.Find("web"), Aim.On(new Cell(5, 2)),
                                    turn: fight.Next(), fight: fight);

            Assert.True(web.Cast, web.Refusal);
            fight.EndTurn();

            Turn theirs = fight.Next();
            fight.Walk(theirs, new Cell(6, 2));

            Assert.True(goblin.Has(Condition.Restrained));

            Attempt free = cast.BreakFree(fight, theirs, Condition.Restrained);

            Assert.NotNull(free);
            Assert.True(free.Succeeded);
            Assert.False(goblin.Has(Condition.Restrained));
            Assert.Equal(1, theirs.Actions);
        }

        [Fact]
        public void EntangleRestrainsWhoIsThereWhenItIsCastAndMakesTheSquareRough()
        {
            Encounter fight = Field(new ScriptedRng(20, 1, 1));
            Caster druid = Cleric(out Actor me);
            Actor goblin = Goblin("goblin");

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(goblin, new Cell(6, 2));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            cast.Cast(druid, Book.Find("entangle"), Aim.On(new Cell(6, 2)), turn: fight.Next(),
                      fight: fight);

            Assert.True(goblin.Has(Condition.Restrained));
            Assert.True(fight.IsRough(new Cell(5, 1), goblin));
            Assert.False(fight.IsRough(new Cell(1, 1), goblin));
        }

        [Fact]
        public void NothingToBreakOutOfIsNotAnAction()
        {
            Encounter fight = Field(new ScriptedRng(20, 1));
            Actor goblin = Goblin("goblin");

            fight.Enlist(goblin, new Cell(0, 0));
            fight.Enlist(new Actor("hero", 1, null, Allegiance.Hero), new Cell(5, 0));
            fight.Begin();

            Turn turn = fight.Next();

            Assert.Null(new Incantation(fight.Resolver).BreakFree(fight, turn, Condition.Restrained));
        }


        // --- Moonbeam, Spike Growth, Ice Storm ---------------------------------------------------

        [Fact]
        public void MoonbeamBitesWhenItAppearsAndWhenItIsMovedOntoSomebody()
        {
            // initiative 20, 1, 1. a save of 1 when the beam appears on the first goblin (2d10 of
            // 5s); at the end of that goblin's turn it is still in the beam and saves (20, half of
            // 5s); on a later turn the beam is moved onto the second goblin: a 1 and 5s again
            Encounter fight = Field(new ScriptedRng(20, 1, 1, 1, 5, 5, 20, 5, 5, 1, 5, 5));
            Caster druid = Cleric(out Actor me);

            Actor first = Goblin("first");
            Actor second = Goblin("second");

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(first, new Cell(5, 2));
            fight.Enlist(second, new Cell(9, 2));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);

            Casting beam = cast.Cast(druid, Book.Find("moonbeam"), Aim.On(new Cell(5, 2)),
                                     turn: fight.Next(), fight: fight);

            Assert.True(beam.Cast, beam.Refusal);
            Assert.Equal(90, first.Health.Current);

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            Turn later = fight.Next();
            Assert.Same(me, later.Actor);

            Casting moved = cast.Again(druid, Book.Find("moonbeam"), Aim.On(new Cell(9, 2)),
                                       later, fight);

            Assert.True(moved.Cast, moved.Refusal);
            Assert.Equal(85, first.Health.Current);
            Assert.Equal(90, second.Health.Current);

            // moving it is the Magic action
            Assert.Equal(1, later.Actions);
        }

        [Fact]
        public void SpikeGrowthHurtsForEverySquareWalkedThroughIt()
        {
            // 2d4 of 2s a square
            Encounter fight = Field(new ScriptedRng(20, 1, 2, 2));
            Caster druid = Cleric(out Actor me);
            Actor goblin = Goblin("goblin");

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(goblin, new Cell(10, 2));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            cast.Cast(druid, Book.Find("spike_growth"), Aim.On(new Cell(2, 2)),
                      turn: fight.Next(), fight: fight);
            fight.EndTurn();

            Turn theirs = fight.Next();

            // (10,2) to (6,2): (6,2) is the first square within four of (2,2) - one square in
            fight.Walk(theirs, new Cell(6, 2));

            Assert.Equal(96, goblin.Health.Current);
        }

        [Fact]
        public void IceStormsGroundIsRoughUntilTheEndOfTheCastersNextTurn()
        {
            Encounter fight = Field(new ScriptedRng(20, 1, 20, 1));
            Caster druid = Cleric(out Actor me, level: 9);
            Actor goblin = Goblin("goblin");

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(goblin, new Cell(10, 4));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);

            Casting storm = cast.Cast(druid, Book.Find("ice_storm"), Aim.On(new Cell(5, 2)),
                                      turn: fight.Next(), fight: fight);

            Assert.True(storm.Cast, storm.Refusal);
            Assert.True(fight.IsRough(new Cell(5, 2), goblin));

            fight.EndTurn();               // the cleric's turn ends: still rough
            Assert.True(fight.IsRough(new Cell(5, 2), goblin));

            fight.Next();
            fight.EndTurn();               // the goblin's turn
            Assert.True(fight.IsRough(new Cell(5, 2), goblin));

            fight.Next();
            fight.EndTurn();               // the end of the caster's NEXT turn
            Assert.False(fight.IsRough(new Cell(5, 2), goblin));
        }


        // --- the new sway fields ---------------------------------------------------------------

        [Fact]
        public void StoneskinHalvesWeaponDamage()
        {
            Caster druid = Cleric(out Actor me);

            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(druid, Book.Find("stoneskin"), Aim.At(me));

            Assert.Equal(Defense.Resistant, me.DefenseAgainst(DamageType.Slashing));
            Assert.Equal(Defense.Normal, me.DefenseAgainst(DamageType.Fire));
            Assert.Equal(5, me.Suffer(10, DamageType.Piercing));
        }

        [Fact]
        public void RayOfFrostSlowsTheTargetsNextTurn()
        {
            Caster mage = Cleric(out Actor me);
            Actor goblin = Goblin("goblin");

            new Incantation(new StandardResolver(new ScriptedRng(18, 4)))
                .Cast(mage, Book.Find("ray_of_frost"), Aim.At(goblin));

            Assert.Equal(20, goblin.Moves);
            Assert.Equal(20, new Turn(goblin, new ActionBudget(), 1).Movement);
        }

        [Fact]
        public void ShockingGraspTakesAwayTheOpportunityAttack()
        {
            Caster mage = Cleric(out Actor me);
            Actor goblin = Goblin("goblin");

            new Incantation(new StandardResolver(new ScriptedRng(18, 4)))
                .Cast(mage, Book.Find("shocking_grasp"), Aim.At(goblin));

            Assert.True(goblin.Boons.NoOpportunityAttacks);

            var swing = new OpportunityAttack(new Attack("claw", DiceRoll.Parse("1d4"),
                                                         DamageType.Slashing));
            Encounter fight = Field(new ScriptedRng(10));
            fight.Enlist(goblin, new Cell(1, 1));
            fight.Enlist(me, new Cell(2, 1));

            Assert.False(swing.CanAnswer(fight, goblin,
                                         Moment.Leaving(me, goblin, new Cell(2, 1), new Cell(3, 1))));
        }

        [Fact]
        public void ForesightIsAdvantageOnEveryD20AndDisadvantageAgainst()
        {
            Caster mage = Cleric(out Actor me, level: 17);

            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(mage, Book.Find("foresight"), Aim.At(me));

            Assert.Equal(Advantage.Advantage, me.AttackAdvantage);
            Assert.Equal(Advantage.Advantage, me.SaveAdvantage(Ability.Wisdom));
            Assert.Equal(Advantage.Advantage, me.CheckAdvantageFor(Ability.Strength, Skill.Athletics));
            Assert.Equal(Advantage.Disadvantage, me.AdvantageAgainstMe);
        }

        [Fact]
        public void FaerieFireCancelsBeingUnseen()
        {
            Caster druid = Cleric(out Actor me);
            Actor hidden = Goblin("hidden");

            hidden.Boons.Add(new Boon("invisibility", "invisibility", Duration.Encounter,
                                      disadvantageAgainst: true));

            Assert.Equal(Advantage.Disadvantage, hidden.AdvantageAgainstMe);

            // off the board, a square area is whoever the caller says was in it
            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(druid, Book.Find("faerie_fire"), Aim.At(hidden));

            Assert.Equal(Advantage.Advantage, hidden.AdvantageAgainstMe);
        }


        // --- repeat saves, with a spell written for the test -------------------------------------

        [Fact]
        public void ARepeatSaveAtTheEndOfTheTurnEndsTheCondition()
        {
            SpellReader.TryRead(@"{""spells"":[{""id"":""test_hold"",""level"":2,
              ""school"":""enchantment"",""range"":12,""concentration"":true,""effects"":[
              {""primitive"":""afflict"",""reach"":""creature"",""condition"":""stunned"",
               ""save"":""wis"",""on_save"":""negates"",""repeat_save"":true,
               ""duration"":""concentration""}]}]}",
                                out IReadOnlyList<Spell> read, out IReadOnlyList<string> problems);

            Assert.Empty(problems);

            // cleric first; the goblin fails the first save (1) and makes the repeat (20)
            Encounter fight = Field(new ScriptedRng(20, 1, 1, 20));
            Caster cleric = Cleric(out Actor me);
            cleric.Learn(read[0]);

            Actor goblin = Goblin("goblin");

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(goblin, new Cell(3, 0));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            cast.Cast(cleric, read[0], Aim.At(goblin), turn: fight.Next(), fight: fight);

            Assert.True(goblin.Has(Condition.Stunned));

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            Assert.False(goblin.Has(Condition.Stunned));
        }

        [Fact]
        public void TheReaderWantsAZoneForAZoneEffectAndPulsesToSayWhen()
        {
            SpellReader.TryRead(@"{""spells"":[{""id"":""no_zone"",""level"":1,""effects"":[
              {""primitive"":""damage"",""reach"":""zone"",""pulses"":""enter"",""amount"":""1d6"",
               ""damage_type"":""fire""}]},
              {""id"":""no_when"",""level"":1,""concentration"":true,""effects"":[
              {""primitive"":""zone"",""reach"":""place"",""radius"":2,""duration"":""concentration""},
              {""primitive"":""damage"",""reach"":""zone"",""amount"":""1d6"",""damage_type"":""fire""}]}]}",
                                out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.StartsWith("no_zone") && p.Contains("makes no zone"));
            Assert.Contains(problems, p => p.StartsWith("no_when") && p.Contains("pulses"));
        }
    }
}
