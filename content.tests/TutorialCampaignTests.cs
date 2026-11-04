using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Dialogue;
using Content.Inventory;
using Content.Items;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Content.Tests
{
    // THE THREE TUTORIALS (2026-11-04): Kathleen's beginner, intermediate and expert scripts
    // (campaign_authoring/Tutorial M&MR Campaigns), one campaign each, picked by how much the player knows about
    // tabletop games. campaigns/tutorial_* are written by tools/make_tutorials.py around the hand-written .yarn
    public class TutorialCampaignTests : IDisposable
    {
        public static readonly TheoryData<string, TutorialLevel> Tiers = new()
        {
            { "tutorial_beginner", TutorialLevel.Beginner },
            { "tutorial_intermediate", TutorialLevel.Intermediate },
            { "tutorial_expert", TutorialLevel.Advanced },
        };

        readonly string _saves = Path.Combine(Path.GetTempPath(), "lanorim_tutorial_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_saves)) Directory.Delete(_saves, true);
        }

        static string Folder(string id) => Path.Combine(AppContext.BaseDirectory, "campaigns", id);

        static Package Pack(string id) => SampleCampaignTests.Sound(Folder(id));

        static DialogueBook Book(string id) => DialogueBook.Read(Path.Combine(Folder(id), Package.DialogueFolder), id);

        [Theory]
        [MemberData(nameof(Tiers))]
        public void ItLoadsWithNothingToWarnAboutAndEveryKeyHasEnglish(string id, TutorialLevel level)
        {
            Package pack = Pack(id);

            Assert.Equal(level, TutorialPicker.LevelOf(pack.Manifest));
            Assert.False(pack.Manifest.Rules.StartingShop);   // the story teaches shopping (TUTORIAL_BEGINNER_CONTENTS 0.4)
            Assert.True(!pack.Cautions.Any(), string.Join("\n", pack.Cautions));

            DialogueBook book = Book(id);

            Assert.True(book.Problems.Count == 0, string.Join("\n", book.Problems));

            string csv = File.ReadAllText(Path.Combine(Folder(id), Package.LocaleFolder, id + ".csv"));
            IReadOnlyList<string> missing = Locale.Audit(csv, pack.Keys().Concat(book.Keys()));

            Assert.True(missing.Count == 0, string.Join("\n", missing));
        }

        [Fact]
        public void ThePickerHasOneTutorialForEachLevel()
        {
            var picker = new TutorialPicker(Tiers.Select(row => Pack((string)row[0]).Manifest));

            foreach (object[] row in Tiers)
            {
                TutorialChoice choice = picker.Choices.Single(c => c.Level == (TutorialLevel)row[1]);

                Assert.True(choice.Available);
                Assert.Equal((string)row[0], choice.CampaignId);
            }
        }

        // a cue speaks in a fight the campaign has, and says something
        [Theory]
        [MemberData(nameof(Tiers))]
        public void EveryCueIsForAFightTheCampaignHas(string id, TutorialLevel level)
        {
            Package pack = Pack(id);
            DialogueBook book = Book(id);

            HashSet<string> fights = pack.Encounters.SelectMany(t => t.Entries)
                                         .Where(e => e.Kind == Core.Tables.EntryKind.Fight).Select(e => e.Id).ToHashSet();

            Assert.NotEmpty(book.Cues);

            foreach (Cue cue in book.Cues)
            {
                Assert.True(fights.Contains(cue.Fight), $"{id}: {cue} - no fight called {cue.Fight}");
                Assert.NotEmpty(book.CueLines(cue));
            }

            // the beginner's first fight: the map, the first turn, the first swing and the rat's turn
            if (level == TutorialLevel.Beginner)
            {
                Assert.Equal(3, book.CueLines(new Cue("granary_rat", CueMoment.Start, 0)).Count);
                Assert.Equal("cue_rat_map", book.CueLines(new Cue("granary_rat", CueMoment.Start, 0))[0].Local);
                Assert.NotEmpty(book.CueLines(new Cue("granary_rat", CueMoment.HeroAttack, 1)));
                Assert.Empty(book.CueLines(new Cue("granary_rat", CueMoment.HeroTurn, 2)));
            }
        }

        [Theory]
        [InlineData("granary_rat start", "granary_rat", CueMoment.Start, 0)]
        [InlineData("granary_rat hero_turn 1", "granary_rat", CueMoment.HeroTurn, 1)]
        [InlineData("loft foe_turn 2", "loft", CueMoment.FoeTurn, 2)]
        [InlineData("loft hero_attack 3", "loft", CueMoment.HeroAttack, 3)]
        public void ACueSaysWhichFightAndWhichMoment(string text, string fight, CueMoment moment, int count)
        {
            Assert.True(Cue.TryParse(text, out Cue cue, out string problem), problem);
            Assert.Equal(new Cue(fight, moment, count), cue);
            Assert.Equal(text, cue.ToString());
        }

        [Theory]
        [InlineData("granary_rat")]
        [InlineData("granary_rat hero_turn")]
        [InlineData("granary_rat hero_turn 0")]
        [InlineData("granary_rat start 1")]
        [InlineData("granary_rat sometime 1")]
        public void AWrongCueIsRefusedAndSaysWhy(string text)
        {
            Assert.False(Cue.TryParse(text, out _, out string problem));
            Assert.NotNull(problem);

            DialogueBook book = DialogueBook.Of("cue_test",
                ("cues.yarn", $"title: start\nspeaker: dm\n---\nHello. #line:hello\n===\n" +
                              $"title: bad\nspeaker: companion\ncue: {text}\n---\nHm. #line:hm\n===\n"));

            Assert.Contains(book.Problems, p => p.ToString().Contains("cue"));
            Assert.Empty(book.Cues);
        }

        // THE CUES IN A REAL FIGHT: the granary rat, fought the plain way, says the start, the first turn and the
        // first swing, and the rat's turn when the rat gets one
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void TheRatFightSaysItsLinesAtItsMoments(int seed)
        {
            Library library = Library.Srd();
            Package pack = Pack("tutorial_beginner");
            var rng = new SeededRng(seed);
            var run = new CampaignRun(library, pack, SampleCampaignTests.Make(library, "fighter"),
                                      new StandardResolver(rng), rng);

            run.Start();

            while (run.Now != Scene.Fight)
            {
                if (run.Now == Scene.Choice) run.Choose(0);
                else if (run.Now == Scene.Shop) run.LeaveShop();
                else run.Next();
            }

            Assert.Equal("granary_rat", run.Fight.Entry.Id);

            var said = new List<string>();
            var cues = new CueWatcher(run.Fight.Entry.Id, run.Hero.Actor, run.Book.CueLines);
            cues.Said += lines => said.AddRange(lines.Select(l => l.Local));

            Battle battle = run.BattleFor(new StandardResolver(rng), null, cues);
            Outcome outcome = new AutoPlayer().Play(new CombatSession(battle, library.Items, library.Forms));

            Assert.NotEqual(Outcome.Open, outcome);
            Assert.Equal(new[] { "cue_rat_map", "cue_rat_rounds", "cue_rat_initiative" }, said.Take(3));
            Assert.Contains("cue_rat_lit_squares", said);
            Assert.Contains("cue_rat_attack_roll", said);

            // the hero's first turn comes before the first swing
            Assert.True(said.IndexOf("cue_rat_weapon") < said.IndexOf("cue_rat_attack_roll"));
        }

        // EVERY CLASS PLAYS EVERY TIER TO THE END: the gate, the store, two granary fights, the road, the farm, home,
        // and level 2. A death reloads, as it does for the player
        [Theory]
        [MemberData(nameof(EveryClassInEveryTier))]
        public void ItPlaysFromTheGateToTheEnd(string id, string className)
        {
            Library library = Library.Srd();
            Package pack = Pack(id);
            var rng = new SeededRng(7);
            var saves = new SaveLibrary(_saves);
            var run = new CampaignRun(library, pack, SampleCampaignTests.Make(library, className),
                                      new StandardResolver(rng), rng, saves);

            int choices = 0, deaths = 0, shops = 0;
            var maps = new List<string>();

            run.Start();

            for (int step = 0; run.Now != Scene.Over; step++)
            {
                Assert.True(step < 5000, $"{id} never ended: {run}");

                switch (run.Now)
                {
                    case Scene.Line:
                        run.Next();
                        break;

                    case Scene.Choice:
                        run.Choose(choices++ % 3);
                        break;

                    case Scene.Shop:
                        {
                            shops++;

                            // the companion's lesson: sell the torches, buy the rope
                            Merchant store = run.Shop.Open(library.Items);
                            Assert.True(run.Hero.Pack.CountOf("torch") >= 2);
                            store.Sell(run.Hero.Pack, library.Items.Find("torch"), 2);
                            store.Buy(run.Hero.Pack, library.Items.Find("rope"), run.Hero.Actor, run.Hero.Class.Id);
                            run.LeaveShop();
                            break;
                        }

                    case Scene.Fight:
                        {
                            maps.Add(run.Fight.MapId);
                            Battle battle = run.BattleFor(new StandardResolver(rng));
                            Assert.NotNull(battle);
                            Outcome outcome = new AutoPlayer().Play(new CombatSession(battle, library.Items, library.Forms));
                            run.EndFight(outcome == Outcome.Open ? Outcome.Fled : outcome);
                            break;
                        }

                    case Scene.Dead:
                        {
                            deaths++;
                            SaveShelf.Saved last = saves.ForReload(pack.Id, run.Slot);
                            Assert.NotNull(last);
                            run = CampaignRun.Resume(last.Game, library, pack, new StandardResolver(rng), rng, saves, out _);
                            run.Continue();
                            break;
                        }
                }

                while (run.Hero.PendingImprovements > 0) run.Hero.ImproveAsSuggested();
            }

            Assert.Empty(run.Talk.Complained);
            Assert.Equal(1, shops);
            Assert.Contains("granary_floor", maps);
            Assert.Contains("granary_loft", maps);
            Assert.Equal(2, run.Hero.Level);
            Assert.True(run.Hero.Pack.CountOf("rope") >= 1);
            Assert.True(run.Hero.Pack.CountOf("potion_of_healing") >= 1);

            // nothing in the tutorial should be able to kill a hero played the plain way more than now and then
            Assert.True(deaths <= 2, $"{className} died {deaths} times in {id}");
        }

        public static IEnumerable<object[]> EveryClassInEveryTier() =>
            Tiers.SelectMany(row => Roster.Classes.Select(c => new[] { row[0], c }));
    }
}
