using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Content.Companions;
using Content.Dialogue;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Words;

namespace Content.Tests
{
    // COMPANION BARKS IN THE GAME (cc_task_i Part 2): Kathleen's pools imported into game-wide banks, each pool raised
    // at its moment (the folded names too), and a chatter that keeps it from talking too much
    public class BarkTests
    {
        static readonly string[] Companions =
            { "bonded_wolf", "gossiping_raven", "bound_imp", "saints_fragment", "borrowed_shape_spirit" };

        // a bank with a few lines in every v1 pool
        static Speaking Wolf(int lines = 3) =>
            new BarkBank("bonded_wolf", BarkPools.Spoken.ToDictionary(p => p, _ => lines)).Open(new SeededRng(4));

        static Attempt Roll(RollKind kind, int natural) => new Attempt(kind, D20Roll.Fixed(natural, 3), 12);

        static Actor Hero(int hp = 20)
        {
            var hero = new Actor("hero", 1, new AbilityScores(), Allegiance.Hero);
            hero.SetHealth(new Health(hp));
            return hero;
        }

        static string PoolOf(string key) => key?.Split('.')[3];

        // --- the banks ---------------------------------------------------------------------------------

        [Fact]
        public void EveryCompanionHasAGameWideBankWithEveryV1Pool()
        {
            BarkBook srd = BarkBook.Srd();

            Assert.Empty(srd.Problems);

            foreach (string companion in Companions)
            {
                BarkBank bank = srd.Of(companion);

                Assert.NotNull(bank);

                foreach (Bark pool in BarkPools.Spoken)
                    Assert.True(bank.Has(pool), $"{companion} has no {pool.Id()} lines");
            }

            // and every class's companion is one of them
            Assert.All(Library.Srd().Classes, c => Assert.Contains(c.Companion, Companions));
        }

        [Fact]
        public void ACampaignsLinesAreAddedOnTopUnderItsOwnKeys()
        {
            var game = new BarkBank("bonded_wolf", new Dictionary<Bark, int> { [Bark.Victory] = 2 });
            var campaign = new BarkBank("bonded_wolf", new Dictionary<Bark, int> { [Bark.Victory] = 1, [Bark.Camp] = 1 },
                                        campaign: "bramble");

            BarkBank both = game.Adding(campaign);

            Assert.Equal(3, both.Count(Bark.Victory));
            Assert.Equal(1, both.Count(Bark.Camp));
            Assert.Contains("dialogue.bonded_wolf.bark.victory.bramble.001", both.Keys());
            Assert.Contains("dialogue.bonded_wolf.bark.victory.002", both.Keys());
            Assert.All(both.Keys(), k => Assert.True(Core.Localization.KeyConventions.IsWellFormed(k), k));

            // dealt together: all three before any twice
            Speaking speaking = both.Open(new SeededRng(2));
            Assert.Equal(3, Enumerable.Range(0, 3).Select(_ => speaking.Next(Bark.Victory)).Distinct().Count());
        }

        [Fact]
        public void TheBubbleKeepsItsEmphasisAndTheLogAndTheVoiceDont()
        {
            Assert.Equal("Did you SEE that? It was huge.", BarkText.Plain("Did you SEE that? It was [b]huge[/b]."));
            Assert.Equal("so very small", BarkText.Plain("so [i]very[/i] small"));
        }

        // --- the importer ------------------------------------------------------------------------------

        [Fact]
        public void TheImporterTurnsAPoolFileIntoABankAndLocaleRows()
        {
            string temp = Path.Combine(Path.GetTempPath(), "lanorim_barks_" + Guid.NewGuid().ToString("N"));

            try
            {
                string vault = Path.Combine(temp, "Bark Pools");
                Directory.CreateDirectory(Path.Combine(vault, "Victory"));
                Directory.CreateDirectory(Path.Combine(vault, "Fled"));

                File.WriteAllText(Path.Combine(vault, "Victory", "borrowed_shape.md"),
                                  "mushroom\nclass(es): druid\nvoice: alien\n\n1. Is this **victory**?\n2. The *quiet* is new.\n3. ALL DONE, \"friend\".\n");
                File.WriteAllText(Path.Combine(vault, "Victory", "nobody.md"), "x\n\n1. Hello.\n");
                File.WriteAllText(Path.Combine(vault, "Fled", "bonded_wolf.md"), "dragon\n\n1. Run!\n");

                string locale = Path.Combine(temp, "game.csv");
                File.WriteAllText(locale, "keys,en\nability.cha.name,Charisma\nui.zzz.name,Last\n");

                string banks = Path.Combine(temp, "barks");

                (int code, string said) = Python("tools/import_barks.py", "--vault", vault, "--banks", banks, "--locale", locale);

                Assert.True(code == 0, said);

                Read<BarkBank> bank = BarkReader.Parse(File.ReadAllText(Path.Combine(banks, "borrowed_shape_spirit.json")), "bank");
                Assert.True(bank.Ok, string.Join("; ", bank.Problems));
                Assert.Equal(3, bank.Value.Count(Bark.Victory));

                string[] rows = File.ReadAllLines(locale);
                Assert.Equal("keys,en", rows[0]);
                Assert.Contains("dialogue.borrowed_shape_spirit.bark.victory.001,Is this [b]victory[/b]?", rows);
                Assert.Contains("dialogue.borrowed_shape_spirit.bark.victory.002,The [i]quiet[/i] is new.", rows);
                Assert.Contains("dialogue.borrowed_shape_spirit.bark.victory.003,\"ALL DONE, \"\"friend\"\".\"", rows);
                Assert.Equal("ui.zzz.name,Last", rows[^1]);

                // a pool the game doesn't know and a file it can't place are warnings, not failures
                Assert.Contains("\"Fled\" is one the game does not know yet", said);
                Assert.Contains("could not place Victory/nobody.md", said);

                // run again: nothing changes
                (int again, string twice) = Python("tools/import_barks.py", "--vault", vault, "--banks", banks, "--locale", locale, "--check");
                Assert.True(again == 0, twice);
            }
            finally
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        static (int, string) Python(params string[] args)
        {
            string root = Root();
            var start = new ProcessStartInfo("python") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, args[0]));
            foreach (string arg in args.Skip(1)) start.ArgumentList.Add(arg);

            using Process python = Process.Start(start);
            string output = python.StandardOutput.ReadToEnd() + python.StandardError.ReadToEnd();
            python.WaitForExit();
            return (python.ExitCode, output);
        }

        static string Root()
        {
            for (var here = new DirectoryInfo(AppContext.BaseDirectory); here != null; here = here.Parent)
                if (File.Exists(Path.Combine(here.FullName, "tools", "import_barks.py"))) return here.FullName;

            throw new DirectoryNotFoundException("no tools/import_barks.py above " + AppContext.BaseDirectory);
        }

        // --- each pool at its moment ----------------------------------------------------------------------

        [Theory]
        [InlineData(RollKind.Attack, 20, "maxed")]
        [InlineData(RollKind.Attack, 1, "snag")]
        [InlineData(RollKind.Check, 20, "maxed")]  // Perfect plays a Maxed line
        [InlineData(RollKind.Save, 20, "maxed")]
        [InlineData(RollKind.Check, 1, "snag")]    // Trouble plays a Snag line
        [InlineData(RollKind.Save, 1, "snag")]
        public void ANaturalOneOrTwentyOnAnyRollIsASnagOrAMaxed(RollKind kind, int natural, string pool)
        {
            Assert.Equal(pool, PoolOf(new CompanionMind(Wolf()).Sees(Roll(kind, natural))));
        }

        [Fact]
        public void TheFoldedNamesPlayThePoolsSheWrites()
        {
            Assert.Equal("maxed", PoolOf(new CompanionMind(Wolf()).Say(Bark.Perfect)));
            Assert.Equal("snag", PoolOf(new CompanionMind(Wolf()).Say(Bark.Trouble)));
        }

        [Fact]
        public void VictorySecretCampAndNearbyEachHaveTheirMoment()
        {
            Actor hero = Hero();

            var won = new CompanionMind(Wolf(), hero);
            won.Ended(Outcome.HeroesWon);
            Assert.Equal("victory", PoolOf(won.Said.Single()));

            Assert.Equal("secret", PoolOf(new CompanionMind(Wolf()).HearsASecretRoll()));
            Assert.Equal("camp", PoolOf(new CompanionMind(Wolf()).Hears(Bark.Camp)));
            Assert.Equal("nearby", PoolOf(new CompanionMind(Wolf()).Hears(Bark.Nearby)));
        }

        [Fact]
        public void TheHerosSaveInAFightIsSeenButNotAnEnemysOrAnAttack()
        {
            Actor hero = Hero();
            var mind = new CompanionMind(Wolf(), hero);

            mind.Judged(null, new Actor("goblin"), Roll(RollKind.Save, 1));
            mind.Judged(hero, null, Roll(RollKind.Attack, 1));
            Assert.Empty(mind.Said);

            mind.Judged(null, hero, Roll(RollKind.Save, 20));
            Assert.Equal("maxed", PoolOf(mind.Said.Single()));
        }

        [Fact]
        public void TheCutPoolsMoveItsMoodButSayNothing()
        {
            Actor hero = Hero();
            var mind = new CompanionMind(new BarkBank("bonded_wolf", new Dictionary<Bark, int>
            {
                [Bark.Down] = 2, [Bark.Nerve] = 2, [Bark.Nudge] = 2, [Bark.Overasked] = 2,
            }).Open(new SeededRng(1)), hero);

            mind.DeathSaved(hero, Roll(RollKind.Death, 8));
            Assert.Equal(Mood.Alert, mind.Mood);

            mind.Downed(hero);
            Assert.Equal(Mood.Quiet, mind.Mood);

            Assert.Null(mind.Say(Bark.Nudge));
            Assert.Null(mind.Say(Bark.Overasked));
            Assert.Empty(mind.Said);
        }

        // --- danger ahead ---------------------------------------------------------------------------------

        [Fact]
        public void AFightsStartSaysDangerAboutOneTimeInThree()
        {
            int said = 0;

            for (int fight = 0; fight < 600; fight++)
            {
                var mind = new CompanionMind(Wolf(), Hero(), new Chatter(new SeededRng(fight + 1)));

                if (PoolOf(mind.FightStarts()) == "danger") said++;
            }

            Assert.InRange(said, 160, 240);
        }

        [Fact]
        public void DangerPlaysWhenTheStoryAsksButNeverWhenTheHeroIsHurt()
        {
            Assert.Equal("danger", PoolOf(new CompanionMind(Wolf(), Hero()).Hears(Bark.Danger)));

            Actor hurt = Hero(20);
            hurt.Health.Take(12);

            Assert.Null(new CompanionMind(Wolf(), hurt).Hears(Bark.Danger));

            for (int fight = 0; fight < 60; fight++)
                Assert.Null(new CompanionMind(Wolf(), hurt, new Chatter(new SeededRng(fight + 1))).FightStarts());
        }

        [Fact]
        public void AFightsStartKeepsQuietRightAfterTheStoryBarked()
        {
            for (int fight = 0; fight < 60; fight++)
            {
                var mind = new CompanionMind(Wolf(), Hero(), new Chatter(new SeededRng(fight + 1)));

                mind.Hears(Bark.Nearby);
                mind.Tick(Chatter.StoryQuietSeconds - 1);

                Assert.Null(mind.FightStarts());
            }
        }

        // --- the chatter ----------------------------------------------------------------------------------

        [Fact]
        public void OneBarkThenTheCooldownAndALowerOneIsDropped()
        {
            var mind = new CompanionMind(Wolf(), Hero(), new Chatter(new SeededRng(1)));

            Assert.NotNull(mind.Say(Bark.Victory));

            mind.Tick(5);
            Assert.Null(mind.Say(Bark.Camp));     // lower, on the cooldown: dropped
            Assert.Null(mind.Say(Bark.Victory));  // the same: dropped

            mind.Tick(Chatter.CooldownSeconds);
            Assert.NotNull(mind.Say(Bark.Camp));  // the cooldown has run out, and nothing was queued
            Assert.Equal(2, mind.Said.Count);
        }

        [Fact]
        public void AHigherBarkCutsIn()
        {
            var mind = new CompanionMind(Wolf(), Hero(), new Chatter(new SeededRng(1)));

            mind.Hears(Bark.Nearby);
            mind.Tick(1);

            Assert.Equal("victory", PoolOf(mind.Say(Bark.Victory)));
            mind.Tick(1);
            Assert.Equal("snag", PoolOf(mind.Sees(Roll(RollKind.Attack, 1))));
            mind.Tick(1);
            Assert.Null(mind.Say(Bark.Maxed));    // under a Snag

            Assert.Equal(new[] { "nearby", "victory", "snag" }, mind.Said.Select(PoolOf));
        }

        [Fact]
        public void ThePriorityOrderIsKathleens()
        {
            Assert.Equal(new[] { Bark.Snag, Bark.Maxed, Bark.Danger, Bark.Victory, Bark.Secret, Bark.Camp, Bark.Nearby },
                         BarkPools.Spoken.OrderByDescending(p => p.Priority()));
            Assert.Equal(Bark.Snag.Priority(), Bark.Trouble.Priority());
            Assert.Equal(Bark.Maxed.Priority(), Bark.Perfect.Priority());
        }

        [Fact]
        public void OffSilencesEverythingAndSomeHalvesTheFrequentPools()
        {
            var off = new CompanionMind(Wolf(), Hero(), new Chatter(new SeededRng(1), () => CompanionChatter.Off));

            Assert.Null(off.Say(Bark.Victory));
            Assert.Null(off.Sees(Roll(RollKind.Attack, 1)));
            Assert.Null(off.Hears(Bark.Nearby));
            Assert.Null(off.FightStarts());
            Assert.Null(off.HearsASecretRoll());

            var frequent = new Chatter(new SeededRng(7), () => CompanionChatter.Some);
            var rare = new Chatter(new SeededRng(7), () => CompanionChatter.Some);
            int victories = 0, camps = 0;

            for (int i = 0; i < 400; i++)
            {
                double now = i * Chatter.CooldownSeconds;

                if (frequent.Allows(Bark.Victory, now)) victories++;
                if (rare.Allows(Bark.Camp, now)) camps++;
            }

            Assert.InRange(victories, 160, 240);
            Assert.Equal(400, camps);
        }

        [Fact]
        public void CompanionChatterIsASavedSettingThatDefaultsToAll()
        {
            Assert.Equal(CompanionChatter.All, new Screens.GameSettings().Chatter);

            var settings = new Screens.GameSettings { Chatter = CompanionChatter.Some };
            Screens.GameSettings back = Screens.GameSettings.Read(settings.Write(), out var problems);

            Assert.Empty(problems);
            Assert.Equal(CompanionChatter.Some, back.Chatter);
        }

        [Fact]
        public void APoolWithNoLinesRaisesNothingAndLeavesTheCooldownAlone()
        {
            var mind = new CompanionMind(new BarkBank("bonded_wolf", new Dictionary<Bark, int> { [Bark.Camp] = 2 })
                                             .Open(new SeededRng(1)), Hero(), new Chatter(new SeededRng(1)));

            Assert.Null(mind.Say(Bark.Victory));
            Assert.Null(mind.HearsASecretRoll());

            Assert.NotNull(mind.Hears(Bark.Camp));
        }
    }
}
