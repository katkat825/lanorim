using System.Linq;
using Content.Maps;
using Content.Monsters;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;

namespace Content.Tests
{
    // the vertical slice of v1_build_order.md: a character, a map you could have built in the
    // editor, a goblin fight, and a level-up. played headless, on the real engine.
    public class FirstSliceTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero MakeAFighter()
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);

            making.Pick(Srd.Class("fighter"));
            making.Pick(Srd.Kind("human"));
            making.Pick(Srd.Background("soldier"));
            making.SuggestTraits();

            foreach (Skill skill in making.SkillChoices.Take(making.SkillPicksLeft))
                making.Train(skill);

            making.Call("Brenna");

            Hero hero = making.Finish();

            Assert.NotNull(hero);

            return hero;
        }

        static MapLayout BuildAMap(out Cell start, out Cell spawn)
        {
            var draft = new MapDraft(8, 6);

            draft.Paint(new Cell(0, 0), new Cell(7, 5), Tile.Floor);
            draft.Enclose();
            draft.PlaceStart(new Cell(0, 2));
            draft.PlaceSpawn(1, new Cell(7, 3));
            draft.PlaceProp("barrel", new Cell(4, 4));

            Assert.True(draft.Sound, string.Join("; ", draft.Problems()));

            MapLayout map = draft.Layout();

            start = map.Start;
            spawn = map.SpawnAt(1).Value;

            return map;
        }

        [Fact]
        public void MakeACharacterStandOnYourMapBeatAGoblinAndLevelUp()
        {
            Hero hero = MakeAFighter();

            Assert.True(hero.Actor.Health.Maximum > 0);
            Assert.NotEmpty(hero.Attacks);

            MapLayout map = BuildAMap(out Cell start, out Cell spawn);

            var field = new Battlefield(map);
            var log = new CombatLog();

            // a seed, not a script: the fight has to come out right however the dice fall, and
            // this one is played to the end
            var fight = new Encounter(new StandardResolver(new SeededRng(4)), field, log);

            Monster statblock = Srd.Bestiary.Find("goblin");
            Actor goblin = statblock.Spawn();

            fight.Enlist(hero.Actor, start, hero.Budget);
            fight.Enlist(goblin, spawn);

            fight.ArmOpportunity(hero.Actor, hero.Attacks.First(a => !a.IsRanged));
            fight.ArmOpportunity(goblin, statblock.Opportunity);

            ITactics brain = statblock.Brain();

            fight.Begin();

            Core.Characters.Attack sword = hero.Attacks.First(a => !a.IsRanged);

            Turn turn;

            while ((turn = fight.Next()) != null && fight.Round <= 30)
            {
                if (turn.Actor.Side != Allegiance.Hero)
                {
                    brain.Take(fight, turn);
                    continue;
                }

                // the player's turn, played the plain way: walk up and swing
                while (turn.Can(Spend.Action))
                {
                    if (hero.Hit(fight, turn, goblin, sword) != null) continue;

                    Cell? there = field.Where(goblin);

                    if (!there.HasValue) break;

                    Cell? step = field.Reachable(hero.Actor, turn.SquaresLeft)
                                      .OrderBy(p => Battlefield.Distance(p.Key, there.Value))
                                      .Select(p => (Cell?)p.Key)
                                      .FirstOrDefault();

                    if (!step.HasValue || fight.Walk(turn, step.Value).Count < 2) break;
                }

                fight.EndTurn();
            }

            Assert.Equal(Outcome.HeroesWon, fight.Judge());
            Assert.Contains(log.Lines, l => l.Contains("goes down"));

            hero.FightOver();

            int was = hero.Actor.Health.Maximum;

            // milestone levelling: the campaign says when
            hero.LevelTo(2);

            Assert.Equal(2, hero.Level);
            Assert.True(hero.Actor.Health.Maximum > was);
            Assert.Contains(hero.Features, f => f.Id == "action_surge");
        }

        [Fact]
        public void AFighterBeatsOneGoblinFarMoreOftenThanNot()
        {
            // the same slice, a hundred times, on a hundred different seeds. one fight can go any
            // way; a hundred is a balance claim, and this is the number the sim watches
            int wins = 0;

            for (int seed = 0; seed < 100; seed++) if (Slice(seed)) wins++;

            Assert.InRange(wins, 80, 100);
        }

        static bool Slice(int seed)
        {
            Hero hero = MakeAFighter();

            MapLayout map = BuildAMap(out Cell start, out Cell spawn);

            var field = new Battlefield(map);
            var fight = new Encounter(new StandardResolver(new SeededRng(seed)), field);

            Monster statblock = Srd.Bestiary.Find("goblin");
            Actor goblin = statblock.Spawn();

            fight.Enlist(hero.Actor, start, hero.Budget);
            fight.Enlist(goblin, spawn);
            fight.ArmOpportunity(goblin, statblock.Opportunity);

            ITactics brain = statblock.Brain();

            fight.Begin();

            Core.Characters.Attack sword = hero.Attacks.First(a => !a.IsRanged);

            Turn turn;

            while ((turn = fight.Next()) != null && fight.Round <= 30)
            {
                if (turn.Actor.Side != Allegiance.Hero)
                {
                    brain.Take(fight, turn);
                    continue;
                }

                while (turn.Can(Spend.Action))
                {
                    if (hero.Hit(fight, turn, goblin, sword) != null) continue;

                    Cell? there = field.Where(goblin);

                    if (!there.HasValue) break;

                    Cell? step = field.Reachable(hero.Actor, turn.SquaresLeft)
                                      .OrderBy(p => Battlefield.Distance(p.Key, there.Value))
                                      .Select(p => (Cell?)p.Key)
                                      .FirstOrDefault();

                    if (!step.HasValue || fight.Walk(turn, step.Value).Count < 2) break;
                }

                fight.EndTurn();
            }

            return fight.Judge() == Outcome.HeroesWon;
        }
    }
}
