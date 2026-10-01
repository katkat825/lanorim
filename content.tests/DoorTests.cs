using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // DOORS IN A FIGHT (cc_task_open-questions-answers.md 2.1). SRD 5.2.1, Interacting with Things: one object or
    // feature for free during your move or action ("you could open a door during your move as you stride toward a
    // foe"); a second needs the Utilize action. Monsters open them too (Tactics.OpenTheWay)
    public class DoorTests
    {
        static readonly Library Srd = Library.Srd();

        // two rooms: a door on the line east of (2,1) and another south of (2,1); the hero starts at (2,1)
        const string Rooms = @"
+-+-+-+-+-+-+-+
|. . .|. . . .|
+ + + + + + + +
|. . @x. . . .|
+ + +x+ + + + +
|. . .|. . . .|
+-+-+-+-+-+-+-+";

        static Encounter Fight(out Actor hero, out Actor goblin, int goblinX = 6, int goblinY = 1)
        {
            Assert.True(MapReader.TryRead(Rooms, out MapLayout map, out string problem), problem);

            var fight = new Encounter(new StandardResolver(Script(20, 1)), new Battlefield(map), new CombatLog());
            hero = new Actor("hero", 1, new AbilityScores(), Allegiance.Hero);
            hero.SetHealth(new Health(200));
            goblin = Srd.Bestiary.Find("goblin").Spawn();

            fight.Enlist(hero, map.Start);
            fight.Enlist(goblin, new Cell(goblinX, goblinY));
            fight.Begin();

            return fight;
        }

        static Turn HerosTurn(Encounter fight, Actor hero)
        {
            Turn turn = fight.Next();
            return ReferenceEquals(turn.Actor, hero) ? turn : null;
        }

        [Fact]
        public void TheFirstDoorIsFreeAndTheSecondCostsAnAction()
        {
            Encounter fight = Fight(out Actor hero, out _);
            Turn turn = HerosTurn(fight, hero);
            Assert.NotNull(turn);

            IReadOnlyList<Border> doors = fight.DoorsBeside(hero);
            Assert.Equal(2, doors.Count);
            Assert.Equal(Spend.Free, Encounter.DoorCost(turn));

            int actions = turn.Actions;
            Assert.True(fight.OpenDoor(turn, doors[0]));
            Assert.Equal(actions, turn.Actions);
            Assert.Equal(Edge.None, fight.Field.Map.At(doors[0]));

            Assert.Equal(Spend.Action, Encounter.DoorCost(turn));
            Assert.True(fight.OpenDoor(turn, doors[1]));
            Assert.Equal(actions - 1, turn.Actions);

            Assert.Contains("hero opens the door", fight.Observer.ToString());
        }

        // SRD 5.2.1 Gaseous Form: the mist "can't ... manipulate objects", so it can't open a door
        [Fact]
        public void AMistCantOpenADoor()
        {
            Assert.True(MapReader.TryRead(Rooms, out MapLayout map, out string problem), problem);
            var fight = new Encounter(new StandardResolver(Script(20, 1)), new Battlefield(map), new CombatLog());
            Core.Magic.Caster wizard = Wizard(out Actor me);
            Actor goblin = Srd.Bestiary.Find("goblin").Spawn();

            fight.Enlist(me, map.Start);
            fight.Enlist(goblin, new Cell(6, 0));
            fight.Begin();

            Assert.NotEmpty(fight.DoorsBeside(me));

            new Core.Magic.Incantation(fight.Resolver).Cast(wizard, Book.Find("gaseous_form"), Core.Magic.Aim.At(me), fight: fight);

            Assert.Empty(fight.DoorsBeside(me));
        }

        [Fact]
        public void ADoorThatIsNotBesideYouStaysShut()
        {
            Encounter fight = Fight(out Actor hero, out _);
            Turn turn = HerosTurn(fight, hero);

            var far = new Border(new Cell(3, 0), true);
            Assert.False(fight.OpenDoor(turn, far));
        }

        [Fact]
        public void TheHeroIsOfferedEachDoorBesideThem()
        {
            Assert.True(MapReader.TryRead(Rooms, out MapLayout map, out string problem), problem);
            var hero = new Content.Sheet.Hero("Tess", Srd.Class("fighter"), Srd.Kind("human"), Srd.Background("soldier"),
                                              Creation.Creation.Standard(Srd.Class("fighter")), 3);
            hero.Build(null, Srd.Class("fighter").SkillChoices.Take(2).ToList(), null, Srd.Items, new List<Core.Magic.Spell>());

            var resolver = new TableResolver(new Core.Dice.ScriptedRng(1), new AlwaysTwenty(), a => ReferenceEquals(a, hero.Actor));
            Battle battle = Battle.Set(Srd, map, hero, new[] { new Battle.Foe(Srd.Bestiary.Find("goblin"), new Cell(6, 0)) }, resolver);
            var session = new CombatSession(battle, Srd.Items, Srd.Forms);
            session.Start();

            List<ActionOption> doors = session.Options().Where(o => o.Kind == OptionKind.OpenDoor).ToList();
            Assert.Equal(new[] { "open_door_east", "open_door_south" }, doors.Select(o => o.Id).OrderBy(id => id));
            Assert.All(doors, o => Assert.Equal(Spend.Free, o.Cost));

            Assert.True(session.Take(doors.First(o => o.Id == "open_door_east")).Done);
            Assert.Contains(session.Options(), o => o.Id == "open_door_south" && o.Cost == Spend.Action);
        }

        // the goblin across the shut door from the hero: it opens it for free and comes through
        [Fact]
        public void AMonsterOpensTheDoorBetweenItAndItsQuarry()
        {
            Encounter fight = Fight(out Actor hero, out Actor goblin, goblinX: 3, goblinY: 1);

            Turn turn = fight.Next();
            if (ReferenceEquals(turn.Actor, hero)) { fight.EndTurn(); turn = fight.Next(); }

            new BasicTactics(Srd.Bestiary.Find("goblin").Attacks).Take(fight, turn);

            Assert.Equal(Edge.None, fight.Field.Map.At(new Border(new Cell(3, 1), true)));
            Assert.Contains("goblin opens the door", fight.Observer.ToString());
        }

        sealed class AlwaysTwenty : IDiceSource
        {
            public IReadOnlyList<int> Throw(IReadOnlyList<Core.Dice.Die> dice) => dice.Select(d => System.Math.Min(20, Core.Dice.DieExtensions.Sides(d))).ToList();
        }
    }
}
