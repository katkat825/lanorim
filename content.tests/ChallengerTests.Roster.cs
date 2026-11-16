using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Challengers;
using Content.Combat;
using Content.Dialogue;
using Content.Encounters;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Core.Tables;

namespace Content.Tests
{
    // the roster, a bout, the story and the screen (cc_task_cg Parts 3-6); the fixture and helpers are ChallengerTests.cs
    public partial class ChallengerTests
    {
        // --- Part 3: the roster --------------------------------------------------------------------------------

        [Fact]
        public void ASeededRosterIsTheSameTwiceAndSurvivesASave()
        {
            CampaignRun first = Run(out _, seed: 11);
            CampaignRun second = Run(out _, seed: 11);

            Assert.Equal(Scene.Roster, ToTheRoster(first));
            Assert.Equal(Scene.Roster, ToTheRoster(second));
            Assert.Equal(Ids(first), Ids(second));
            Assert.Equal(2, first.Arena.Roster.Count);

            first.Arena.Reputation = 7;
            Read<SaveGame> read = SaveReader.Parse(SaveWriter.Write(first.Capture()));
            Assert.True(read.Value.Arena != null, string.Join("; ", read.Problems));

            CampaignRun back = CampaignRun.Resume(read.Value, Srd, Pack(), new StandardResolver(new SeededRng(99)),
                                                  new SeededRng(99), null, out _);

            // a different seed, and still the same roster: it was saved, not rerolled
            Assert.Equal(Scene.Roster, back.Continue());
            Assert.Equal(Ids(first), Ids(back));
            Assert.Equal(7, back.Arena.Reputation);
            Assert.Equal(first.Arena.Bags.Keys, back.Arena.Bags.Keys);
            Assert.Equal(first.Arena.Bags.Values.Select(b => string.Join(",", b.Left)),
                         back.Arena.Bags.Values.Select(b => string.Join(",", b.Left)));
        }

        [Fact]
        public void ANameOrADescriptionDoesNotComeRoundAgainUntilItsListRunsOut()
        {
            ChallengerType goblin = Pack().Challengers.Single(t => t.Id == "goblin_warrior");
            var one = new ArenaRules { Least = 1, Most = 1 };
            var arena = new Arena();
            var draw = new RosterDraw(arena, new[] { goblin }, Find(Pack()), new SeededRng(5));

            var names = new List<int>();
            var descriptions = new List<int>();

            for (int bout = 0; bout < 6; bout++)
            {
                draw.Draw(1, one, null);
                names.Add(arena.Roster.Single().Name);
                descriptions.Add(arena.Roster.Single().Description);
            }

            // three names: each once in the first three, and again in the next three
            Assert.Equal(new[] { 1, 2, 3 }, names.Take(3).OrderBy(n => n));
            Assert.Equal(new[] { 1, 2, 3 }, names.Skip(3).OrderBy(n => n));
            Assert.Equal(new[] { 1, 2 }, descriptions.Take(2).OrderBy(n => n));
            Assert.Equal(new[] { 1, 2 }, descriptions.Skip(2).Take(2).OrderBy(n => n));

            // and no two on one roster share a name while the list has more
            for (int seed = 1; seed < 30; seed++)
            {
                Arena three = Drawn(1, seed, reputation: 10, types: new[] { goblin });
                Assert.Equal(3, three.Roster.Select(c => c.Name).Distinct().Count());
            }
        }

        [Fact]
        public void ATeamFitsItsBudgetAndTheEnemyCap()
        {
            Package pack = Pack();
            ChallengerType raiders = pack.Challengers.Single(t => t.Id == "raiders");

            for (int level = 1; level <= 10; level++)
                for (int seed = 1; seed <= 40; seed++)
                {
                    IReadOnlyList<Mustered> team = ChallengerFit.Team(raiders.Monsters, Find(pack), level, new SeededRng(seed),
                                                                      out EncounterDifficulty aimed);
                    int xp = ChallengerFit.XpOf(team, Find(pack));

                    Assert.InRange(team.Sum(m => m.Count), 1, FightBudget.Cap(level));
                    Assert.True(xp <= FightBudget.BudgetFor(level, aimed), $"level {level} seed {seed}: {xp} XP over {aimed}");
                    Assert.All(team, m => Assert.Contains(m.Monster, new[] { "goblin", "guard" }));
                }

            // and on the roster it says the difficulty it came to
            Challenger drawn = Drawn(3, 2, reputation: 40).Roster.First(c => c.Type == "raiders");
            Assert.Equal(ChallengerFit.DifficultyOf(drawn.Xp, 3), drawn.Difficulty);
            Assert.Equal(ChallengerShape.Team, drawn.Shape);
        }

        [Fact]
        public void ASingleOutOfBandForTheHerosLevelIsLeftOut()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                // level 1: the ogre (450 XP) is over a high fight's 100
                Assert.DoesNotContain(Drawn(1, seed, reputation: 40).Roster, c => c.Type == "ogre");

                // level 5: a goblin (50) or a guard (25) is under half a low fight's 500; the ogre fits now. (a special
                // fight is the author's own, so it isn't held to the band)
                Assert.All(Drawn(5, seed, reputation: 40).Roster.Where(c => !c.Special),
                           c => Assert.Contains(c.Type, new[] { "ogre", "raiders", "bruiser", "ogre_champion" }));
            }

            // nothing fits at all: the closest ones are offered, and the roster says how hard they are
            Package pack = Pack();
            ChallengerType[] small = pack.Challengers.Where(t => t.Id is "goblin_warrior" or "guard").ToArray();
            Arena late = Drawn(10, 4, reputation: 40, types: small);

            List<Challenger> generated = late.Roster.Where(c => !c.Special).ToList();

            // three goblin names, so three goblins: no name twice on one roster
            Assert.Equal(3, generated.Count);
            Assert.Equal(3, generated.Select(c => c.Name).Distinct().Count());
            Assert.All(generated, c => Assert.Equal("goblin_warrior", c.Type));
            Assert.All(generated, c => Assert.Equal(EncounterDifficulty.Low, c.Difficulty));
        }

        [Fact]
        public void ASpecialFightAppearsOnlyFromItsReputationAndNotOnceBeaten()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                Assert.DoesNotContain(Drawn(1, seed, reputation: 9).Roster, c => c.Special);

                Challenger special = Drawn(1, seed, reputation: 10).Roster.Single(c => c.Special);
                Assert.Equal("spear_trial", special.Id);
                Assert.Equal("challenger.spear_trial.name", special.NameKey);
            }

            // beaten, it doesn't come back
            Package pack = Pack();
            var arena = new Arena { Reputation = 30 };
            arena.Beaten.Add("spear_trial");
            new RosterDraw(arena, pack.Challengers, Find(pack), new SeededRng(1)).Draw(1, ArenaRules.Default, pack.SpecialChallengers);
            Assert.DoesNotContain(arena.Roster, c => c.Special);
        }

        // --- 3.5 and Part 4: after a bout ------------------------------------------------------------------------

        [Fact]
        public void TheOneTheHeroLostToStaysAndTheOneTheyBeatLeaves()
        {
            CampaignRun run = Run(out Hero hero, seed: 3);
            ToTheRoster(run);

            Challenger rival = run.Arena.Roster[0];
            run.Pick(rival.Id);

            Assert.Equal(Scene.Fight, On(run));
            Assert.Same(rival, run.Fight.Challenger);

            run.EndFight(Outcome.HeroesLost);

            // lost: still there for a rematch, and reputation can't go under 0
            Assert.Equal(Scene.Roster, On(run));
            Assert.Contains(run.Arena.Roster, c => c.Id == rival.Id);
            Assert.Equal(0, run.Arena.Reputation);
            Assert.Equal(0, hero.Experience);

            // yielded counts as lost
            run.Pick(rival.Id);
            On(run);
            run.EndFight(Outcome.Yielded);
            Assert.Equal(Scene.Roster, On(run));
            Assert.Contains(run.Arena.Roster, c => c.Id == rival.Id);

            // won: gone, reputation up by 5, and the bout's XP in place of the monster's
            run.Pick(rival.Id);
            On(run);
            int bout = run.BoutXp;
            run.EndFight(Outcome.HeroesWon);

            Assert.Equal(Scene.Roster, On(run));
            Assert.DoesNotContain(run.Arena.Roster, c => c.Id == rival.Id);
            Assert.Equal(5, run.Arena.Reputation);
            Assert.Equal(BoutXp.For(1, rival.Difficulty, ArenaRules.Default), bout);
            Assert.Equal(bout, hero.Experience);
            Assert.NotEqual(run.FightXp, bout);
        }

        [Fact]
        public void TheBoutXpRuleAtLevelsOneThreeAndTen()
        {
            ArenaRules rules = ArenaRules.Default;

            // level 1 needs 300 to level 2: a quarter is 75
            Assert.Equal(56, BoutXp.For(1, EncounterDifficulty.Low, rules));
            Assert.Equal(75, BoutXp.For(1, EncounterDifficulty.Moderate, rules));
            Assert.Equal(113, BoutXp.For(1, EncounterDifficulty.High, rules));

            // level 3 needs 1,800 (900 to 2,700)
            Assert.Equal(338, BoutXp.For(3, EncounterDifficulty.Low, rules));
            Assert.Equal(450, BoutXp.For(3, EncounterDifficulty.Moderate, rules));
            Assert.Equal(675, BoutXp.For(3, EncounterDifficulty.High, rules));

            // level 10 needs 21,000 (64,000 to 85,000)
            Assert.Equal(3938, BoutXp.For(10, EncounterDifficulty.Low, rules));
            Assert.Equal(5250, BoutXp.For(10, EncounterDifficulty.Moderate, rules));
            Assert.Equal(7875, BoutXp.For(10, EncounterDifficulty.High, rules));

            Assert.Equal(0, BoutXp.For(20, EncounterDifficulty.High, rules));

            // the pack's share and multipliers
            Assert.Equal(150, BoutXp.For(1, EncounterDifficulty.Moderate, new ArenaRules { Share = 0.5 }));
            Assert.Equal(150, BoutXp.For(1, EncounterDifficulty.High, new ArenaRules { High = 2 }));
        }

        // --- Part 5: the story's side ----------------------------------------------------------------------------

        [Fact]
        public void TheStoryNamesTheChallengerAndTheFightDoesToo()
        {
            CampaignRun run = Run(out Hero hero, seed: 3);
            ToTheRoster(run);

            Challenger single = run.Arena.Roster.First(c => c.Alone);
            run.Pick(single.Id);

            Assert.Equal(single.Id, run.Recall(ArenaVariables.Challenger) ?? ArenaVariables.Read(run.Arena, ArenaVariables.Challenger));
            Assert.Equal(single.NameKey, ArenaVariables.Read(run.Arena, ArenaVariables.Name));
            Assert.Equal(false, ArenaVariables.Read(run.Arena, ArenaVariables.Special));

            // the announcer: {$hero_name} faces {$challenger_name}! - the key comes out as the name
            Assert.Equal(Scene.Line, run.Now);
            ILocalizer words = Words();
            Assert.Equal($"{hero.Name} faces {words.Get(single.NameKey)}!", run.Talk.Saying.Text(words));

            // in the fight, the lone challenger is called by their name; the HUD and the log ask NameKeyOf
            Assert.Equal(Scene.Fight, On(run));
            Battle battle = run.BattleFor(new StandardResolver(new SeededRng(1)));
            Core.Characters.Actor foe = battle.Monsters.Single();
            Assert.Equal(single.NameKey, battle.NameKeyOf(foe));
            Assert.Null(battle.NameKeyOf(hero.Actor));
        }

        [Fact]
        public void AGroupsMembersKeepTheirStatblockNames()
        {
            CampaignRun run = Run(out _, seed: 3);
            ToTheRoster(run);

            var pair = new Challenger
            {
                Id = "guard_pair_001_001", Type = "guard_pair", Shape = ChallengerShape.Group, Name = 1, Description = 1,
                Group = new[] { new Mustered("guard", 2) },
            };
            run.Arena.Roster.Add(pair);
            run.Pick(pair.Id);
            On(run);

            Battle battle = run.BattleFor(new StandardResolver(new SeededRng(1)));
            Assert.Equal(2, battle.Monsters.Count());
            Assert.All(battle.Monsters, m => Assert.Equal("monster.guard.name", battle.NameKeyOf(m)));
        }

        [Fact]
        public void BackToTheStoryPicksNobody()
        {
            CampaignRun run = Run(out _);
            ToTheRoster(run);

            Assert.Equal(Scene.Line, run.LeaveRoster());
            Assert.Equal("", ArenaVariables.Read(run.Arena, ArenaVariables.Challenger));
            Assert.EndsWith("roster_back", run.Talk.Heard.Last().Key);
        }

        [Fact]
        public void TheCommandTakesNothingAndIsAnEngineVerb()
        {
            Assert.Equal(RequestKind.Challengers, Request.Parse("challengers", out string none).Kind);
            Assert.Null(none);

            Assert.Null(Request.Parse("challengers normal", out string problem));
            Assert.Contains("<<challengers>>", problem);
        }

        // --- Part 6: what the roster screen shows -------------------------------------------------------------

        [Fact]
        public void TheRosterScreenSaysEachChallengersDifficultyAndWhoTheyAre()
        {
            Package pack = Pack();
            Arena arena = Drawn(3, 2, reputation: 40);
            var view = new RosterView(arena, Find(pack));

            Assert.Same(arena.Roster[0], view.Selected);
            Assert.Contains(arena.Roster, c => c.Special);

            Challenger team = arena.Roster.First(c => c.Shape == ChallengerShape.Team);
            Assert.True(view.Select(team.Id));
            Assert.Equal(RosterView.TeamKey, RosterView.ShapeKey(team));
            Assert.All(view.Members(team), m => Assert.StartsWith("monster.", m.NameKey));
            Assert.StartsWith("ui.workshop.difficulty_", RosterView.DifficultyKey(team));

            // every word the screen says is in the game's English
            string csv = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game.csv"));
            Assert.Empty(Locale.Audit(csv, RosterView.Keys().Concat(arena.Roster.Select(RosterView.DifficultyKey))));
        }
        // --- Kathleen's lists of 2026-11-05: generic names for any stat block, and "a single thug or scout" ---------------

        // --- Kathleen, 2026-11-09: a type's own lists first; the shared lists for the rest, and for any stat block --------

        [Fact]
        public void ATypeUsesItsOwnListsAndOneWithNoneTakesTheSharedList()
        {
            Package pack = Pack();
            ChallengerType goblin = pack.Challengers.Single(t => t.Id == "goblin_warrior");
            ChallengerType pair = pack.Challengers.Single(t => t.Id == "bandit_pair");

            // its own, and nothing shared mixed in
            Assert.Equal(3, goblin.NameKeys(pack.Challengers).Count);
            Assert.All(goblin.NameKeys(pack.Challengers), k => Assert.StartsWith("challenger.goblin_warrior.", k));

            // none of its own: a group takes the teams' shared list
            Assert.Equal((0, 0), (pair.Names, pair.Descriptions));
            Assert.Equal(new[] { "challenger.generic_team.name.001" }, pair.NameKeys(pack.Challengers));
            Assert.Equal(new[] { "challenger.generic_team.description.001" }, pair.DescriptionKeys(pack.Challengers));

            var drawn = Enumerable.Range(1, 30).SelectMany(seed => Drawn(1, seed, reputation: 40).Roster)
                                  .Where(c => !c.Special).ToList();

            Assert.All(drawn.Where(c => c.Type is "goblin_warrior" or "guard" or "raiders"),
                       c => Assert.DoesNotContain("generic", c.NameKey));
            Assert.Contains(drawn, c => c.Type == "bandit_pair" && c.NameKey == "challenger.generic_team.name.001");

            ILocalizer words = Words();
            Assert.All(drawn, c => Assert.True(words.Has(c.NameKey) && words.Has(c.DescriptionKey), c.ToString()));
        }

        [Fact]
        public void AStatBlockNoTypeClaimsIsOfferedUnderTheSharedLists()
        {
            Package pack = Pack();

            // claimed: goblin (goblin_warrior, bruiser), guard, ogre (the plain ogre type), bandit (bandit_pair). the ogre
            // champion is a champion, so it claims nothing
            string[] claimed = { "goblin", "guard", "ogre", "bandit" };
            Assert.False(pack.Challengers.Single(t => t.Id == "ogre_champion").Claims("ogre"));

            var drawn = Enumerable.Range(1, 40).SelectMany(seed => Drawn(1, seed, reputation: 40, bestiary: Bestiary()).Roster)
                                  .Where(c => c.Type.StartsWith("generic")).ToList();

            List<Challenger> singles = drawn.Where(c => c.Type == "generic_single").ToList();
            Assert.NotEmpty(singles);
            Assert.All(singles, c =>
            {
                Assert.True(c.Alone);
                Assert.DoesNotContain(c.Group.Single().Monster, claimed);
                Assert.StartsWith("challenger.generic_single.name.", c.NameKey);
                Assert.True(ChallengerFit.Fits(c.Xp, 1), c.ToString());
            });

            // a team of any stat block: one stat block, more than one of it, within the budget and the cap
            List<Challenger> teams = drawn.Where(c => c.Type == "generic_team").ToList();
            Assert.NotEmpty(teams);
            Assert.All(teams, c =>
            {
                Assert.Single(c.Group);
                Assert.DoesNotContain(c.Group.Single().Monster, claimed);
                Assert.InRange(c.Enemies, 1, FightBudget.Cap(1));
                Assert.True(c.Xp <= FightBudget.BudgetFor(1, EncounterDifficulty.High), c.ToString());
            });

            // the new SRD stat blocks are among them at their levels: a scout fits level 1
            Assert.Contains(singles, c => c.Group.Single().Monster == "scout");
        }

        [Fact]
        public void AChampionLeavesItsStatBlocksToTheSharedListsToo()
        {
            Package pack = Pack();
            ChallengerType champion = pack.Challengers.Single(t => t.Id == "ogre_champion");

            Assert.Equal(ChallengerKind.Champion, champion.Kind);
            Assert.Equal(ChallengerKind.Own, pack.Challengers.Single(t => t.Id == "ogre").Kind);

            // with only the champion for ogres, a plain ogre still comes up under the shared lists at level 5; with the
            // plain ogre type as well, it doesn't
            ChallengerType[] alone = pack.Challengers.Where(t => t.Id == "ogre_champion" || t.Shared).ToArray();
            var plain = Enumerable.Range(1, 30).SelectMany(seed => Drawn(5, seed, reputation: 40, types: alone,
                                                                         bestiary: new[] { "ogre" }).Roster).ToList();

            Assert.Contains(plain, c => c.Type == "ogre_champion" && c.NameKey == "challenger.ogre_champion.name.001");
            Assert.Contains(plain, c => c.Type == "generic_single" && c.Group.Single().Monster == "ogre");

            ChallengerType[] claimed = pack.Challengers.Where(t => t.Id is "ogre_champion" or "ogre" || t.Shared).ToArray();
            Assert.DoesNotContain(Enumerable.Range(1, 30).SelectMany(seed => Drawn(5, seed, reputation: 40, types: claimed,
                                                                                   bestiary: new[] { "ogre" }).Roster),
                                  c => c.Type.StartsWith("generic"));

            // a champion fights as its stat block: the ogre's name key and mini are the statblock's, its name the champion's
            Assert.All(plain.Where(c => c.Type == "ogre_champion"), c => Assert.Equal("ogre", c.Group.Single().Monster));
        }

        [Fact]
        public void AKindWrittenWrongIsRefused()
        {
            ChallengerReader.Entries.TryRead(@"{""challengers"":[
                {""id"":""a"",""shape"":""single"",""monsters"":[{""monster"":""ogre""}],""names"":1,""descriptions"":1,""kind"":""hero""},
                {""id"":""b"",""shape"":""single"",""names"":1,""descriptions"":1,""kind"":""champion""}]}",
                                             out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.StartsWith("a:") && p.Contains("'hero' is not a kind"));
            Assert.Contains(problems, p => p.StartsWith("b:") && p.Contains("no champion"));
        }

        [Fact]
        public void ASingleThatIsOneOfSeveralFieldsWhicheverFitsTheHerosLevel()
        {
            ChallengerType bruiser = Pack().Challengers.Single(t => t.Id == "bruiser");

            for (int seed = 1; seed <= 20; seed++)
            {
                // level 1: the ogre is over a high fight; level 5: the goblin warrior is under half a low one
                Assert.All(Drawn(1, seed, types: new[] { bruiser }).Roster, c => Assert.Equal("goblin", c.Group.Single().Monster));
                Assert.All(Drawn(5, seed, types: new[] { bruiser }).Roster, c => Assert.Equal("ogre", c.Group.Single().Monster));
            }

            Assert.All(Drawn(1, 3, types: new[] { bruiser }).Roster, c => Assert.True(c.Alone));
        }

        [Fact]
        public void TheGoblinGoesByItsSrdNameAndTheImporterKnowsItByThat()
        {
            Assert.Equal("Goblin Warrior", Words().Get("monster.goblin.name"));
            Assert.Equal("goblin", Pack().Challengers.Single(t => t.Id == "goblin_warrior").Monsters.Single().Monster);
        }
    }
}
