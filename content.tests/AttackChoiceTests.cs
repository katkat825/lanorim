using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Rules;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // WHICH ATTACK A MONSTER MAKES IS A RULE, NOT AN ID (cc_task_open-questions-answers.md 2.7). Renaming the goblin's
    // "goblin_shortbow" changed play: attacks that tied on damage were chosen by id, alphabetically, so a goblin
    // beside the hero shot its bow point-blank at Disadvantage. Now: no Disadvantage first, then average damage, then
    // the statblock's order (Tactics.Best)
    public class AttackChoiceTests
    {
        static readonly Library Srd = Library.Srd();

        sealed class Swings : CombatObserver
        {
            public List<string> Attacks { get; } = new();

            public override void Struck(Blow blow) => Attacks.Add(blow.Attack.Id);
        }

        static List<string> GoblinSwingsFrom(int x, IReadOnlyList<Attack> attacks = null)
        {
            Assert.True(MapReader.TryRead(Fights.Hall, out MapLayout map, out string problem), problem);
            var swings = new Swings();

            // the hero goes first, then passes; the goblin's whole turn is what is watched
            var fight = new Encounter(new StandardResolver(Script(20, 1)), new Battlefield(map), swings);
            var hero = new Actor("hero", 1, new AbilityScores(), Allegiance.Hero);
            hero.SetHealth(new Health(200));
            Actor goblin = Srd.Bestiary.Find("goblin").Spawn();

            fight.Enlist(hero, new Cell(1, 2));
            fight.Enlist(goblin, new Cell(x, 2));
            fight.Begin();

            Turn turn = fight.Next();
            if (ReferenceEquals(turn.Actor, hero)) turn = fight.Next();

            new BasicTactics(attacks ?? Srd.Bestiary.Find("goblin").Attacks).Take(fight, turn);

            return swings.Attacks;
        }

        static Attack Renamed(Attack a) =>
            new Attack("zz_" + a.Id, a.Damage, a.DamageType, a.Ability, a.Proficient, a.Reach, a.Range, a.LongRange,
                       a.Hand, a.Finesse, a.AttackBonus, a.DamageBonus, a.AddsAbilityToDamage)
            {
                Thrown = a.Thrown, Light = a.Light, Heavy = a.Heavy, Versatile = a.Versatile, Category = a.Category,
                OnHit = a.OnHit,
            };

        [Fact]
        public void BesideTheHeroAGoblinUsesItsScimitar() =>
            Assert.Equal(new[] { "scimitar" }, GoblinSwingsFrom(2).Distinct());

        [Fact]
        public void AtRangeItUsesItsBow() =>
            Assert.Equal(new[] { "goblin_shortbow" }, GoblinSwingsFrom(8).Distinct());

        // SWITCHING TARGETS MID-MULTIATTACK (cc_task_open-questions-answers.md 2.6): the goblin boss's two attacks; the
        // first drops a summoned ally on one hit point, and the second goes at the hero beside it
        [Fact]
        public void AMultiattackTurnsOnTheNextOneWhenItsTargetDrops()
        {
            Assert.True(MapReader.TryRead(Fights.Hall, out MapLayout map, out string problem), problem);
            var swings = new List<(string Attack, string Target)>();
            var fight = new Encounter(new StandardResolver(new Core.Dice.ScriptedRng(1, 20, 20, 20, 20, 20, 20, 20, 20)),
                                      new Battlefield(map), new Watcher(swings));

            var weak = new Actor("summoned", 1, new AbilityScores(), Allegiance.Hero);
            weak.SetHealth(new Health(1));
            var hero = new Actor("hero", 1, new AbilityScores(), Allegiance.Hero);
            hero.SetHealth(new Health(200));
            Content.Monsters.Monster statblock = Srd.Bestiary.Find("goblin_boss");
            Actor boss = statblock.Spawn();

            fight.Enlist(weak, new Cell(3, 1));
            fight.Enlist(hero, new Cell(3, 3));
            fight.Enlist(boss, new Cell(3, 2), statblock.Budget());
            fight.Begin();

            Turn turn;
            while (!ReferenceEquals((turn = fight.Next()).Actor, boss)) fight.EndTurn();

            new BasicTactics(statblock.Attacks).Take(fight, turn);

            Assert.True(weak.IsDown);
            Assert.Equal(new[] { "summoned", "hero" }, swings.Select(s => s.Target));
        }

        sealed class Watcher : CombatObserver
        {
            readonly List<(string, string)> _swings;

            public Watcher(List<(string, string)> swings) => _swings = swings;

            public override void Struck(Blow blow) => _swings.Add((blow.Attack.Id, blow.Target.Id));
        }

        // the same goblin with its attacks in the other order and other ids plays the same turn: the id decides nothing
        [Fact]
        public void TheIdsDecideNothing()
        {
            IReadOnlyList<Attack> statblock = Srd.Bestiary.Find("goblin").Attacks;
            List<Attack> renamed = statblock.Select(Renamed).Reverse().ToList();

            Assert.Equal(GoblinSwingsFrom(2).Select(id => "zz_" + id), GoblinSwingsFrom(2, renamed));
        }
    }
}
