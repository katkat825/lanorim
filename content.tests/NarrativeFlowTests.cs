using System.Collections.Generic;
using System.Linq;
using Content.Dialogue;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Tables;

namespace Content.Tests
{
    // BRANCH OUT AND RETURN (v1_build_checklist.md section 8): a story stops for a check or a
    // fight, the table does it, and the story comes back down the branch the result picked.
    // played headless end to end, on the real Yarn runtime and the real rules
    public class NarrativeFlowTests
    {
        static readonly Library Srd = Library.Srd();

        const string Gate = @"title: gate
speaker: dm
---
The gate is shut. #line:gate_shut
<<check athletics 15>>
<<if $passed>>
    It gives. #line:it_gives
<<else>>
    It holds. #line:it_holds
<<endif>>
<<fight gate_guards>>
<<if $fight == ""won"">>
    The guards are down. #line:guards_down
<<elseif $fight == ""fled"">>
    You run for the trees. #line:you_run
<<else>>
    It goes dark. #line:dark
<<endif>>
<<roll 1d20>>
<<if $roll >= 15>>
    Something glints in the grass. #line:glint
<<endif>>
<<give potion_of_healing 2>>
<<gold 25>>
The road goes on. #line:road
===
";

        static Conversation Talk(out DialogueBook book)
        {
            book = DialogueBook.Of("gatehouse", ("gate.yarn", Gate));

            Assert.True(book.Problems.Count == 0, string.Join("; ", book.Problems));

            var talk = new Conversation(book);

            Assert.True(talk.Start("gate"));

            return talk;
        }

        static Hero Fighter()
        {
            var hero = new Hero("Brenna", Srd.Class("fighter"), Srd.Kind("human"),
                                Srd.Background("soldier"),
                                Creation.Creation.Standard(Srd.Class("fighter")), 3);

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Strength] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Athletics, Skill.Perception }, null, Srd.Items);

            return hero;
        }

        // reads on until the story stops for something, and says which line it stopped on
        static string ReadUntilItStops(Conversation talk)
        {
            string last = null;

            while (!talk.IsOver && !talk.IsWaiting && !talk.IsChoosing)
            {
                if (talk.Saying != null) last = talk.Saying.Key;

                talk.Advance();
            }

            return last;
        }

        [Fact]
        public void TheStoryStopsForACheckAndWaits()
        {
            Conversation talk = Talk(out _);

            Assert.EndsWith("gate_shut", talk.Saying.Key);

            talk.Advance();

            Assert.True(talk.IsWaiting);
            Assert.Equal(RequestKind.Check, talk.Pending.Kind);
            Assert.Equal(Skill.Athletics, talk.Pending.Skill);
            Assert.Equal(15, talk.Pending.Dc);

            // advancing without an answer goes nowhere: the branch waits for the roll
            talk.Advance();
            Assert.True(talk.IsWaiting);
        }

        [Fact]
        public void APassedCheckTakesTheBranchForIt()
        {
            Conversation talk = Talk(out _);
            talk.Advance();

            Hero hero = Fighter();
            var referee = new Referee(hero, new StandardResolver(new ScriptedRng(18)),
                                      new GmScreen(new ScriptedRng(1)), Srd);

            Settled settled = referee.Settle(talk.Pending);

            Assert.True(settled.Attempt.Succeeded);

            talk.Answer(settled.Answer);

            Assert.EndsWith("it_gives", talk.Saying.Key);
        }

        [Fact]
        public void AFailedCheckTakesTheOtherBranch()
        {
            Conversation talk = Talk(out _);
            talk.Advance();

            var referee = new Referee(Fighter(), new StandardResolver(new ScriptedRng(2)),
                                      new GmScreen(new ScriptedRng(1)), Srd);

            talk.Answer(referee.Settle(talk.Pending).Answer);

            Assert.EndsWith("it_holds", talk.Saying.Key);
        }

        [Fact]
        public void AFightIsHandedBackAndItsOutcomePicksTheBranch()
        {
            Conversation talk = Talk(out _);
            talk.Advance();

            talk.Answer(new Answer { Passed = true });
            ReadUntilItStops(talk);

            Assert.Equal(RequestKind.Fight, talk.Pending.Kind);
            Assert.Equal("gate_guards", talk.Pending.Id);

            // the referee cannot run a fight; the game does, and answers with the outcome
            var referee = new Referee(Fighter(), new StandardResolver(new ScriptedRng(10)),
                                      new GmScreen(new ScriptedRng(1)), Srd);

            Assert.True(referee.Settle(talk.Pending).NeedsTheTable);

            talk.Answer(new Answer { Outcome = Outcome.Fled });

            Assert.EndsWith("you_run", talk.Saying.Key);
        }

        [Fact]
        public void AHiddenRollIsMadeBehindTheScreenAndTheStoryReadsIt()
        {
            Conversation talk = Talk(out _);
            talk.Advance();
            talk.Answer(new Answer { Passed = true });
            ReadUntilItStops(talk);
            talk.Answer(new Answer { Outcome = Outcome.HeroesWon });
            ReadUntilItStops(talk);

            Assert.Equal(RequestKind.Roll, talk.Pending.Kind);

            var log = new ScreenLog();
            var referee = new Referee(Fighter(), new StandardResolver(new ScriptedRng(10)),
                                      new GmScreen(new ScriptedRng(17), log), Srd);

            Settled settled = referee.Settle(talk.Pending);

            Assert.True(settled.Roll.IsHidden);
            Assert.Single(log.Lines);

            talk.Answer(settled.Answer);

            Assert.EndsWith("glint", talk.Saying.Key);
        }

        [Fact]
        public void AGiftGoesInThePackAndGoldIsEarned()
        {
            Conversation talk = Talk(out _);
            Hero hero = Fighter();
            int gold = hero.Pack.Gold;

            var referee = new Referee(hero, new StandardResolver(new ScriptedRng(18)),
                                      new GmScreen(new ScriptedRng(1)), Srd);

            // play the whole thing through, settling everything the referee can
            talk.Advance();

            while (!talk.IsOver)
            {
                if (talk.IsWaiting)
                {
                    Settled settled = referee.Settle(talk.Pending);

                    talk.Answer(settled.Answer ?? new Answer { Outcome = Outcome.HeroesWon });
                }
                else talk.Advance();
            }

            Assert.Equal(2, hero.Pack.CountOf("potion_of_healing"));
            Assert.Equal(gold + 25, hero.Pack.Gold);
            Assert.Contains(talk.Heard, s => s.Key.EndsWith("road"));
        }

        [Fact]
        public void AMilestoneLevelRaisesTheHero()
        {
            Hero hero = Fighter();

            Request level = Request.Parse("level 5", out string problem);

            Assert.Null(problem);

            new Referee(hero, new StandardResolver(new ScriptedRng(10)),
                        new GmScreen(new ScriptedRng(1)), Srd).Settle(level);

            Assert.Equal(5, hero.Level);
        }

        [Fact]
        public void TheDcCanBeAWordFromTheLadder()
        {
            Request hard = Request.Parse("check stealth hard", out string problem);

            Assert.Null(problem);
            Assert.Equal(Difficulty.Hard.Dc(), hard.Dc);

            Request ability = Request.Parse("check str 12", out _);

            Assert.Equal(Skill.None, ability.Skill);
            Assert.Equal(Ability.Strength, ability.Ability);
        }

        [Fact]
        public void AVerbWrittenWrongIsNamedAndACampaignsOwnCommandIsStillOnlyRecorded()
        {
            Assert.Null(Request.Parse("check athletics", out string wrong));
            Assert.Contains("check", wrong);

            Assert.Null(Request.Parse("summon_dragon now", out string none));
            Assert.Null(none);

            Assert.Null(Request.Parse("check juggling 12", out string skill));
            Assert.Contains("juggling", skill);
        }

        [Fact]
        public void TheTablesVariablesNeedNoDeclarationInTheCampaign()
        {
            // the gate script reads $passed, $fight and $roll without declaring any of them, and
            // compiled without a problem - which is the whole of this test
            Talk(out DialogueBook book);

            Assert.NotNull(book.Program);
        }

        [Fact]
        public void LootIsAVerbAndNamesItsTable()
        {
            Request loot = Request.Parse("loot goblin_pockets", out string problem);

            Assert.Null(problem);
            Assert.Equal(RequestKind.Loot, loot.Kind);
            Assert.Equal("goblin_pockets", loot.Id);

            // with no campaign there is no table to open, and the story goes on regardless
            Settled settled = new Referee(Fighter(), new StandardResolver(new ScriptedRng(10)),
                                          new GmScreen(new ScriptedRng(1)), Srd)
                .Settle(loot);

            Assert.NotNull(settled.Answer);
            Assert.Contains("goblin_pockets", settled.Problem);
        }

        [Fact]
        public void AnEncounterWithNoTableIsAProblemAndTheStoryGoesOn()
        {
            Request encounter = Request.Parse("encounter north_road", out _);

            Settled settled = new Referee(Fighter(), new StandardResolver(new ScriptedRng(10)),
                                          new GmScreen(new ScriptedRng(1)), Srd)
                .Settle(encounter);

            Assert.NotNull(settled.Answer);
            Assert.Equal("", settled.Answer.Entry);
            Assert.Contains("north_road", settled.Problem);
        }
    }
}
