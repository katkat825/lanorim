using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Combat;
using Content.Encounters;
using Content.Maps;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;
using Core.Tables;
using Xunit;

namespace Content.Tests
{
    // 3.4 (cc_task_h): MORALE, DECIDED PER FIGHT BY THE CAMPAIGN. "leader": the others break when the leader falls;
    // "half": when half of them, rounded up, are down; none (the default): to the end. Broken, they run for the
    // nearest way off the map, or with none they surrender - and either way the fight is won
    public class MoraleTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Fighter()
        {
            Content.Classes.CharacterClass fighter = Srd.Class("fighter");
            var hero = new Hero("Tess", fighter, Srd.Kind("human"), Srd.Background("soldier"),
                                Creation.Creation.Standard(fighter), 3);
            hero.Build(null, fighter.SkillChoices.Take(2).ToList(), null, Srd.Items, null);
            return hero;
        }

        // a corridor, open at both ends unless it's closed
        static MapLayout Corridor(bool closed)
        {
            var draft = new MapDraft(8, 3);
            draft.Paint(new Cell(0, 0), new Cell(7, 2), Tile.Floor);
            if (closed) draft.Enclose();
            draft.PlaceStart(new Cell(1, 1));
            return draft.Layout();
        }

        static (Battle Battle, Actor First, Actor Second) TwoBandits(Morale morale, bool closed = false,
                                                                     bool secondLeads = true)
        {
            Content.Monsters.Monster bandit = Srd.Bestiary.Find("bandit");

            Battle battle = Battle.Set(Srd, Corridor(closed), Fighter(),
                                       new[]
                                       {
                                           new Battle.Foe(bandit, new Cell(3, 1)),
                                           new Battle.Foe(bandit, new Cell(5, 1), secondLeads),
                                       },
                                       new StandardResolver(new SeededRng(3))).Holding(morale);

            List<Actor> bandits = battle.Monsters.ToList();
            return (battle, bandits[0], bandits[1]);
        }

        static void Fell(Battle battle, Actor actor)
        {
            int took = actor.Suffer(actor.Health.Current, DamageType.Slashing);
            battle.Fight.Hurt(null, actor, took, DamageType.Slashing);
        }

        [Fact]
        public void ALeaderFightBreaksWhenTheMarkedOneFallsAndNotBefore()
        {
            (Battle battle, Actor first, Actor leader) = TwoBandits(Morale.Leader);

            Assert.Same(leader, battle.Fight.Leader);

            Fell(battle, first);
            battle.Fight.Judge();
            Assert.False(battle.Fight.Broken);

            (battle, first, leader) = TwoBandits(Morale.Leader);

            Fell(battle, leader);
            battle.Fight.Judge();
            Assert.True(battle.Fight.Broken);
            Assert.False(battle.Fight.Surrendered);
            Assert.Equal(Outcome.Open, battle.Fight.Outcome);
        }

        [Fact]
        public void WithNoneMarkedTheFirstLeads()
        {
            (Battle battle, Actor first, _) = TwoBandits(Morale.Leader, secondLeads: false);

            Assert.Same(first, battle.Fight.Leader);
        }

        [Fact]
        public void AHalfFightBreaksAtTheFirstKillOfTwo()
        {
            (Battle battle, Actor first, _) = TwoBandits(Morale.Half);

            Fell(battle, first);
            battle.Fight.Judge();

            Assert.True(battle.Fight.Broken);
        }

        [Fact]
        public void WithNoMoraleTheyFightToTheEnd()
        {
            (Battle battle, Actor first, Actor leader) = TwoBandits(Morale.None);

            Fell(battle, leader);
            battle.Fight.Judge();

            Assert.False(battle.Fight.Broken);
            Assert.Equal(Outcome.Open, battle.Fight.Outcome);
        }

        [Fact]
        public void OnAClosedMapTheySurrenderAndItsWon()
        {
            (Battle battle, _, Actor leader) = TwoBandits(Morale.Leader, closed: true);

            Fell(battle, leader);

            Assert.Equal(Outcome.HeroesWon, battle.Fight.Judge());
            Assert.True(battle.Fight.Broken);
            Assert.True(battle.Fight.Surrendered);
        }

        // broken on an open map, the one left runs off the end of the corridor on its turns, and the fight is won
        [Fact]
        public void BrokenTheyRunOffTheMapAndItsWon()
        {
            (Battle battle, Actor first, Actor leader) = TwoBandits(Morale.Leader);
            var session = new CombatSession(battle, Srd.Items, Srd.Forms);

            Fell(battle, leader);
            session.Start();

            for (int turn = 0; turn < 20 && !battle.Fight.Over; turn++)
            {
                if (session.Phase == SessionPhase.Choosing) session.EndTurn();
            }

            Assert.True(battle.Fight.Broken);
            Assert.Equal(Outcome.HeroesWon, battle.Fight.Outcome);
            Assert.True(battle.Fight.IsGone(first));
            Assert.False(first.IsDown);
        }

        [Fact]
        public void TheReaderReadsMoraleAndALeaderAndRefusesAStrayWord()
        {
            const string json = @"{ ""tables"": [ { ""id"": ""t"", ""entries"": [
                { ""id"": ""camp"", ""kind"": ""fight"", ""morale"": ""leader"",
                  ""monsters"": [ { ""monster"": ""bandit"", ""count"": ""2"" },
                                  { ""monster"": ""bandit_captain"", ""leader"": true } ] },
                { ""id"": ""road"", ""kind"": ""fight"", ""morale"": ""rout"",
                  ""monsters"": [ { ""monster"": ""wolf"" } ] } ] } ] }";

            EncounterReader.TryRead(json, out IReadOnlyList<EncounterTable> tables, out IReadOnlyList<string> problems);
            EncounterEntry camp = tables.Single().Entries.Single(e => e.Id == "camp");

            Assert.Equal(Morale.Leader, camp.Morale);
            Assert.False(camp.Monsters[0].Leader);
            Assert.True(camp.Monsters[1].Leader);
            Assert.Contains(problems, p => p.Contains("'rout' is not a morale"));
            Assert.Equal(Morale.None, tables.Single().Entries.Single(e => e.Id == "road").Morale);
        }
    }
}
