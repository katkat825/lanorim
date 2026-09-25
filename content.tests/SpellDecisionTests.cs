using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Magic;
using Core.Resolution;
using Core.Space;

namespace Content.Tests
{
    // Kathleen's spell decisions of 2026-09-25 (_design_docs/REVIEW_spell_names.md), built from the
    // SRD 5.2.1 text: Fly and Gaseous Form fly, Slow is the whole spell less its Somatic clause,
    // Banishment and Maze take a creature off the board and bring it back
    public class SpellDecisionTests
    {
        static readonly SpellBook Book = SpellBook.Srd();

        // eleven by five; the middle row is difficult ground from x = 3 to x = 7
        const string Hall = @"
+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . ~ ~ ~ ~ ~ . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+";

        // the two initiative rolls, then whatever else, then ones for ever after
        static IRng Script(params int[] rolls) =>
            new ScriptedRng(rolls.Concat(Enumerable.Repeat(1, 400)).ToArray());

        static Encounter Field(IRng rng)
        {
            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);

            return new Encounter(new StandardResolver(rng), new Battlefield(map), new CombatLog());
        }

        static Caster Wizard(out Actor actor, int level = 17)
        {
            actor = new Actor("wizard", level, new AbilityScores(10, 10, 14, 18, 10, 10),
                              Allegiance.Hero);
            actor.SetHealth(new Health(80, Die.D6, level));

            var caster = new Caster(actor, Ability.Intelligence,
                                    SpellSlots.For(CasterProgression.Full, level));

            foreach (Spell spell in Book.All) caster.Learn(spell);

            return caster;
        }

        static Actor Goblin(string id = "goblin", int hp = 100, params string[] tags)
        {
            var goblin = new Actor(id, 1, new AbilityScores());
            goblin.SetHealth(new Health(hp));
            goblin.Armor = new ArmorProfile(ArmorWeight.Heavy, 10);
            goblin.Speed = 30;

            foreach (string tag in tags) goblin.Tag(tag);

            return goblin;
        }

        // the wizard at (1,2) going first, the goblin at (x,2) going second
        static Encounter Duel(IRng rng, out Caster wizard, out Actor me, out Actor goblin,
                              int x = 9, params string[] tags)
        {
            Encounter fight = Field(rng);

            wizard = Wizard(out me);
            goblin = Goblin(tags: tags);

            fight.Enlist(me, new Cell(1, 2));
            fight.Enlist(goblin, new Cell(x, 2));
            fight.Begin();

            return fight;
        }

        // every turn ended, round after round, until the goblin's turn in the round asked for
        static Turn GoblinsTurnIn(Encounter fight, Actor goblin, int round)
        {
            Turn turn;

            while ((turn = fight.Next()) != null)
            {
                if (ReferenceEquals(turn.Actor, goblin) && fight.Round >= round) return turn;

                fight.EndTurn();
            }

            return null;
        }


        // --- Fly -----------------------------------------------------------------------------------

        [Fact]
        public void FlyIsASpeedOfSixtyThatPassesOverDifficultGround()
        {
            // SRD p.133: a Fly Speed of 60 feet
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out _);
            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            Assert.Equal(30, me.Moves);

            Casting fly = cast.Cast(wizard, Book.Find("fly"), Aim.At(me), turn: mine, fight: fight);
            Assert.True(fly.Cast, fly.Refusal);

            Assert.True(me.IsFlying);
            Assert.Equal(60, me.Moves);

            // across the difficult row: one square a square
            IReadOnlyList<Cell> route = fight.Field.RouteFor(me, new Cell(7, 2));
            Assert.Equal(6, fight.Field.CostOf(route, me));

            // and letting go lands it
            cast.Release(me);
            Assert.False(me.IsFlying);
            Assert.Equal(30, me.Moves);
        }

        [Fact]
        public void FlyTakesNoExtraCreatureForAHigherSlot()
        {
            // Kathleen 2026-09-25: the higher-level part is dropped
            SpellEffect effect = Book.Find("fly").Effects.Single();

            Assert.Equal(1, effect.TargetsAt(3, 9, 17));
        }

        [Fact]
        public void AFlyerPassesOverAZoneOnTheGround()
        {
            // Spike Growth's spikes and Entangle's plants are on the ground: a flyer's route across
            // them is not difficult terrain, and a walker's is
            Encounter fight = Duel(Script(20, 1, 20, 20, 20), out Caster wizard, out Actor me,
                                   out Actor goblin);
            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            Casting plants = cast.Cast(wizard, Book.Find("entangle"), Aim.On(new Cell(5, 0)),
                                       turn: mine, fight: fight);
            Assert.True(plants.Cast, plants.Refusal);

            var inside = new Cell(5, 1);
            Assert.True(fight.IsRough(inside, goblin));

            me.Boons.Add(new Boon("fly", "fly", Duration.Encounter) { FlySpeed = 60 });
            Assert.False(fight.IsRough(inside, me));
        }


        // --- Gaseous Form ---------------------------------------------------------------------------

        [Fact]
        public void GaseousFormIsAMistThatFliesSlowlyAndCanNeitherAttackNorCast()
        {
            // SRD p.134-135
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 2);
            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            Casting mist = cast.Cast(wizard, Book.Find("gaseous_form"), Aim.At(me), turn: mine,
                                     fight: fight);
            Assert.True(mist.Cast, mist.Refusal);

            Assert.Equal(10, me.Moves);
            Assert.True(me.IsFlying);
            Assert.True(me.Boons.Resist(DamageType.Slashing));
            Assert.True(me.Boons.AdvantageOnSave(Ability.Constitution));
            Assert.False(me.Boons.AdvantageOnSave(Ability.Wisdom));

            Assert.False(me.Apply(Condition.Prone));

            var dagger = new Attack("dagger", DiceRoll.Parse("1d4"), DamageType.Piercing);
            Assert.Null(fight.Hit(mine, goblin, dagger));

            Casting bolt = cast.Cast(wizard, Book.Find("fire_bolt"), Aim.At(goblin), turn: mine,
                                     fight: fight);
            Assert.False(bolt.Cast);
        }

        [Fact]
        public void GaseousFormEndsOnATargetThatDropsToZero()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 2);
            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            cast.Cast(wizard, Book.Find("gaseous_form"), Aim.At(goblin), turn: mine, fight: fight);
            Assert.True(goblin.IsFlying);

            fight.Hurt(me, goblin, goblin.Suffer(1000, DamageType.Force));

            Assert.False(goblin.IsFlying);
        }


        // --- Slow ------------------------------------------------------------------------------------

        [Fact]
        public void SlowIsTheWholeSpellOnUpToSixCreatures()
        {
            // SRD p.163: up to six creatures of your choice in a 40-foot cube, Wis save
            Encounter fight = Field(Script(Enumerable.Repeat(1, 8).Prepend(20).ToArray()));
            Caster wizard = Wizard(out Actor me);
            fight.Enlist(me, new Cell(0, 0));

            var goblins = new List<Actor>();

            for (int i = 0; i < 7; i++)
            {
                Actor g = Goblin($"goblin_{i}");
                goblins.Add(g);
                fight.Enlist(g, new Cell(3 + i % 4, 3 + i / 4));
            }

            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();
            Assert.Same(me, mine.Actor);

            Casting slow = cast.Cast(wizard, Book.Find("slow"), Aim.On(new Cell(3, 3)), turn: mine,
                                     fight: fight);
            Assert.True(slow.Cast, slow.Refusal);

            List<Actor> slowed = goblins.Where(g => g.Boons.SpeedHalved).ToList();
            Assert.Equal(6, slowed.Count);

            Actor one = slowed[0];
            Assert.Equal(15, one.Moves);
            Assert.Equal(8, one.ArmorClass);
            Assert.True(one.Boons.NoReactions);
            Assert.True(one.Boons.ActionOrBonus);
        }

        [Fact]
        public void ASlowedCreatureShakesItOffWithARepeatSave()
        {
            // the save at the end of each of its turns; a 20 ends it
            Encounter fight = Duel(Script(20, 1, 1, 20), out Caster wizard, out Actor me,
                                   out Actor goblin);
            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            cast.Cast(wizard, Book.Find("slow"), Aim.On(new Cell(9, 2)), turn: mine, fight: fight);
            Assert.True(goblin.Boons.SpeedHalved);

            fight.EndTurn();
            Turn its = fight.Next();
            Assert.Same(goblin, its.Actor);
            fight.EndTurn();

            Assert.False(goblin.Boons.SpeedHalved);
        }


        // --- Banishment ------------------------------------------------------------------------------

        [Fact]
        public void BanishmentTakesItOffTheBoardAndBringsItBackWhenTheSpellEnds()
        {
            // SRD p.111: Cha save or transported to a demiplane, Incapacitated; it reappears in the
            // space it left when the spell ends
            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 4);
            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            Casting gone = cast.Cast(wizard, Book.Find("banishment"), Aim.At(goblin), turn: mine,
                                     fight: fight);
            Assert.True(gone.Cast, gone.Refusal);

            Assert.True(fight.IsAway(goblin));
            Assert.Null(fight.Field.Where(goblin));
            Assert.True(goblin.Has(Condition.Incapacitated));
            Assert.False(fight.Over);

            cast.Release(me);

            Assert.False(fight.IsAway(goblin));
            Assert.Equal(new Cell(4, 2), fight.Field.Where(goblin));
            Assert.False(goblin.Has(Condition.Incapacitated));
        }

        [Fact]
        public void BanishmentBringsItBackToTheNearestFreeSquareIfItsOwnIsTaken()
        {
            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 4);
            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            cast.Cast(wizard, Book.Find("banishment"), Aim.At(goblin), turn: mine, fight: fight);

            fight.Field.Place(me, new Cell(4, 2));
            cast.Release(me);

            Cell? back = fight.Field.Where(goblin);
            Assert.True(back.HasValue);
            Assert.Equal(1, Battlefield.Distance(new Cell(4, 2), back.Value));
        }

        [Fact]
        public void HeldTheFullMinuteAFiendDoesNotComeBack()
        {
            // "If the target is an Aberration, a Celestial, an Elemental, a Fey, or a Fiend, the
            // target doesn't return if the spell lasts for 1 minute"
            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out Actor me, out Actor imp,
                                   x: 4, tags: "fiend");
            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            Casting gone = cast.Cast(wizard, Book.Find("banishment"), Aim.At(imp), turn: mine,
                                     fight: fight);
            Assert.True(gone.Cast, gone.Refusal);

            Assert.Null(GoblinsTurnIn(fight, imp, 11));

            Assert.True(fight.IsGone(imp));
            Assert.Equal(Outcome.HeroesWon, fight.Outcome);
            Assert.False(me.IsConcentrating);
        }

        [Fact]
        public void HeldTheFullMinuteACreatureOfThisPlaneComesBack()
        {
            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 4);
            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            cast.Cast(wizard, Book.Find("banishment"), Aim.At(goblin), turn: mine, fight: fight);

            Turn back = GoblinsTurnIn(fight, goblin, 11);

            Assert.NotNull(back);
            Assert.False(fight.IsAway(goblin));
            Assert.False(me.IsConcentrating);
        }


        // --- Maze -------------------------------------------------------------------------------------

        [Fact]
        public void MazeHasNoSaveAndIsEscapedByStudyingIt()
        {
            // SRD p.146: a Study action, DC 20 Intelligence (Investigation); a success escapes and
            // ends the spell. the goblin's first study rolls a 1, its second a 20
            Encounter fight = Duel(Script(20, 1, 1, 20), out Caster wizard, out Actor me,
                                   out Actor goblin, x: 5);
            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();

            Casting maze = cast.Cast(wizard, Book.Find("maze"), Aim.At(goblin), turn: mine,
                                     fight: fight);
            Assert.True(maze.Cast, maze.Refusal);
            Assert.True(fight.IsAway(goblin));

            fight.EndTurn();
            Turn first = fight.Next();
            Assert.True(cast.CanStudy(goblin));
            Assert.False(cast.Study(fight, first).Succeeded);
            Assert.True(fight.IsAway(goblin));
            fight.EndTurn();

            fight.Next();
            fight.EndTurn();
            Turn second = fight.Next();
            Attempt out_ = cast.Study(fight, second);

            Assert.True(out_.Succeeded);
            Assert.Equal(20, out_.Against);
            Assert.False(fight.IsAway(goblin));
            Assert.Equal(new Cell(5, 2), fight.Field.Where(goblin));
            Assert.False(me.IsConcentrating);
        }

        [Fact]
        public void AMonsterInAMazeSpendsItsTurnsStudyingIt()
        {
            Library library = Library.Srd();
            Content.Monsters.Monster ogre = library.Bestiary.Find("ogre");

            Encounter fight = Field(Script(20, 1));
            Caster wizard = Wizard(out Actor me);
            Actor brute = ogre.Spawn();

            fight.Enlist(me, new Cell(1, 2));
            fight.Enlist(brute, new Cell(6, 2), ogre.Budget());
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("maze"), Aim.At(brute), turn: mine, fight: fight);
            fight.EndTurn();

            Turn its = fight.Next();
            ogre.Brain(null, cast).Take(fight, its);

            Assert.True(its.Ended);
            Assert.True(fight.IsAway(brute));
            Assert.Equal(0, its.Actions);
        }


        // --- renamed -------------------------------------------------------------------------------

        [Fact]
        public void WallOfForceIsACubeOfBarsAndTeleportIsAWaypoint()
        {
            IReadOnlyDictionary<string, string> english =
                Locale.Read(System.IO.File.ReadAllText("game.csv"));

            Assert.Equal("Cube of Force", english["spell.wall_of_force.name"]);
            Assert.Equal("Waypoint", english["spell.teleport.name"]);

            SpellEffect cube = Book.Find("wall_of_force").Effects.Single();
            Assert.Equal(Edge.Bars, cube.Encloses);
            Assert.Equal(Reach.Square, cube.Reach);
        }
    }
}
