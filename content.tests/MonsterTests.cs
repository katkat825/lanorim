using System.Collections.Generic;
using System.Linq;
using Content.Creation;
using Content.Maps;
using Content.Monsters;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Tests
{
    public class BestiaryTests
    {
        static readonly Library Srd = Library.Srd();

        [Fact]
        public void TheMonsterFileLoadsWithoutAProblem() =>
            Assert.True(Srd.Bestiary.Sound, string.Join("\n", Srd.Bestiary.Problems));

        [Fact]
        public void TheGoblinOfTheFirstSliceIsThere()
        {
            Monster goblin = Srd.Bestiary.Find("goblin");

            Assert.NotNull(goblin);
            // SRD 5.2.1 Goblin Warrior, p.290: HP 10 (3d6)
            Assert.Equal(10, goblin.HitPoints);
            Assert.Equal(15, goblin.ArmorClass);
            Assert.Equal(0.2, goblin.Challenge);
        }

        [Fact]
        public void EachMultiattackIsTheSrdStatblocks()
        {
            // SRD 5.2.1, checked 2026-09-25. a monster not listed here has no Multiattack, and
            // attacks once a turn (decisions_checklist.md section 1: the two actions are the hero's)
            var srd = new Dictionary<string, string>
            {
                ["goblin_boss"] = "any + any",
                ["werewolf"] = "claw|longbow + claw|longbow|werewolf_bite",
                ["ghoul"] = "ghoul_bite + ghoul_bite",
                ["brown_bear"] = "bear_bite + bear_claw",
                ["owlbear"] = "owlbear_rend + owlbear_rend",
                ["priest"] = "priest_mace|radiant_flame + priest_mace|radiant_flame",
                ["mage"] = "any + any + any",
                // not an SRD 5.2.1 statblock at all - see the 2026-09-25 run log
                ["death_knight"] = "any + any",
            };

            foreach (Monster monster in Srd.Bestiary.All)
            {
                if (srd.TryGetValue(monster.Id, out string listed))
                    Assert.Equal(listed, monster.Multiattack?.ToString());
                else
                    Assert.True(monster.Multiattack == null, $"{monster.Id} has a Multiattack");
            }
        }

        [Fact]
        public void AMultiattackOfThreeLoads()
        {
            Monster mage = Srd.Bestiary.Find("mage");

            Assert.Equal(3, mage.AttacksPerTurn);
            Assert.Equal(1, mage.Budget().ActionsFor(2));
        }

        [Fact]
        public void AMultiattackHasNoCapAndNamesOnlyItsOwnAttacks()
        {
            const string four = @"{""monsters"": [{""id"": ""hydra_ish"", ""multiattack"": 4,
                ""attacks"": [{""id"": ""bite"", ""damage"": ""1d10"", ""damage_type"": ""piercing""}]}]}";

            Assert.True(MonsterReader.TryRead(four, out IReadOnlyList<Monster> read, out var problems),
                        string.Join("\n", problems));
            Assert.Equal(4, read[0].AttacksPerTurn);

            const string wrong = @"{""monsters"": [{""id"": ""bear_ish"", ""multiattack"": [""bite"", ""tail""],
                ""attacks"": [{""id"": ""bite"", ""damage"": ""1d10"", ""damage_type"": ""piercing""}]}]}";

            Assert.False(MonsterReader.TryRead(wrong, out _, out problems));
            Assert.Contains(problems, p => p.Contains("tail"));
        }

        [Fact]
        public void AMonsterHasABonusActionOnlyIfItsStatblockDoes()
        {
            // the goblin's Nimble Escape; a wolf has none
            Monster goblin = Srd.Bestiary.Find("goblin");

            Assert.Equal(Manoeuvre.Disengage | Manoeuvre.Hide, goblin.BonusManoeuvres);
            Assert.Equal(1, goblin.Budget().BonusActionsFor(2));
            Assert.Equal(Manoeuvre.Disengage | Manoeuvre.Hide, goblin.Spawn().QuickOnBonus);

            Assert.Equal(0, Srd.Bestiary.Find("wolf").Budget().BonusActionsFor(2));

            // the mage's Misty Step is a bonus-action spell
            Monster mage = Srd.Bestiary.Find("mage");
            Assert.Equal(1, mage.Budget(mage.CasterFor(mage.Spawn(), Srd.Spells)).BonusActionsFor(2));
        }

        [Fact]
        public void EveryMonsterHasSomethingToAttackWith() =>
            Assert.All(Srd.Bestiary.All, m => Assert.NotEmpty(m.Attacks));

        [Fact]
        public void EveryMonstersMiniIsNamed()
        {
            // v1_minis_map.md: a small Quaternius pack stretched across many statblocks, so the
            // model is named on the statblock and several share one
            foreach (Monster monster in Srd.Bestiary.All)
                Assert.False(string.IsNullOrEmpty(monster.Mini), monster.Id);

            Assert.True(Srd.Bestiary.All.Select(m => m.Mini).Distinct().Count() <
                        Srd.Bestiary.Count,
                        "no mini is reused - the point of the map is that several statblocks share one");
        }

        [Fact]
        public void ASpawnedMonsterHasTheStatblocksNumbers()
        {
            Actor goblin = Srd.Bestiary.Find("goblin").Spawn();

            Assert.Equal(10, goblin.Health.Maximum);
            Assert.Equal(15, goblin.ArmorClass);
            Assert.Equal(Allegiance.Enemy, goblin.Side);

            // Stealth +6: Expertise (SRD 5.2.1 p.290)
            Assert.Equal(Training.Expert, goblin.TrainingIn(Skill.Stealth));
        }

        [Fact]
        public void TwoSpawnsAreTwoDifferentCreatures()
        {
            Monster goblin = Srd.Bestiary.Find("goblin");

            Actor one = goblin.Spawn("goblin_1");
            Actor two = goblin.Spawn("goblin_2");

            one.Suffer(5, DamageType.Slashing);

            Assert.Equal(10, two.Health.Current);
        }

        [Fact]
        public void ASkeletonShrugsOffPoisonAndFearsAMace()
        {
            Actor skeleton = Srd.Bestiary.Find("skeleton").Spawn();

            Assert.Equal(0, skeleton.Suffer(10, DamageType.Poison));

            // doubled to 20, but a skeleton only has 13 to give: Suffer reports what actually
            // came off the hit points, which is what the damage line on screen says
            Assert.Equal(13, skeleton.Suffer(10, DamageType.Bludgeoning));
            Assert.True(skeleton.IsDown);
        }

        [Fact]
        public void TheSrd521WerewolfHasNoResistanceToSteel()
        {
            // SRD 5.2.1 p.339 prints no Resistances line for the werewolf (the older SRD's
            // nonmagical-weapon resistance is gone)
            Actor werewolf = Srd.Bestiary.Find("werewolf").Spawn();

            Assert.Equal(10, werewolf.Suffer(10, DamageType.Slashing));
            Assert.Equal(71, Srd.Bestiary.Find("werewolf").HitPoints);
        }

        [Fact]
        public void TheBestiaryCanBeAskedForSomethingOfARoughSize()
        {
            Assert.Contains(Srd.Bestiary.Around(0.2, 0.1), m => m.Id == "goblin");
            Assert.DoesNotContain(Srd.Bestiary.Around(0.2, 0.1), m => m.Id == "ogre");
        }

        [Fact]
        public void TagsAreThereForTurnUndeadToRead()
        {
            string[] undead = Srd.Bestiary.Tagged("undead").Select(m => m.Id).ToArray();

            Assert.Contains("skeleton", undead);
            Assert.Contains("zombie", undead);
            Assert.DoesNotContain("goblin", undead);
        }
    }

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
