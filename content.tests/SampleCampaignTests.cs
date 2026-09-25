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
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;

namespace Content.Tests
{
    // THE SAMPLE TEST CAMPAIGN (Tier 2.12/3e of the 2026-09-24 run), played headless end to end on
    // the real engine: a character made the way creation makes one, a story with checks, saves and
    // choices, a merchant, three fights on three maps (one from a rolled encounter table), loot,
    // rests, milestone levels to 4 with the ability score improvement, a save and a load, and a
    // death that reloads. campaigns/sample_millbrook is written by tools/make_sample_campaign.py.
    public class SampleCampaignTests : IDisposable
    {
        static readonly string Folder =
            Path.Combine(AppContext.BaseDirectory, "campaigns", "sample_millbrook");

        readonly string _saves = Path.Combine(Path.GetTempPath(), "lanorim_sample_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_saves)) Directory.Delete(_saves, true);
        }

        static Package Pack()
        {
            Package pack = Package.Read(Folder);

            Assert.True(pack.Sound, string.Join("\n", pack.Problems));

            return pack;
        }

        static Hero Make(Library library, string className)
        {
            var making = new Creation.Creation(library, library.Backgrounds);

            making.Pick(library.Class(className));
            making.Pick(library.Kind("human"));
            making.Pick(library.Background("soldier"));

            foreach (Core.Characters.Skill skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList())
                making.Train(skill);

            foreach (Core.Characters.Skill skill in making.Skills.Take(making.ExpertisePicksLeft).ToList())
                making.Master(skill);

            // the first spells on offer, cantrips and levelled alike, until the picks run out
            foreach (Core.Magic.Spell spell in making.SpellChoices.ToList())
                if (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0) making.Learn(spell);

            making.Call("Tamsin");

            Assert.True(making.Ready, string.Join("; ", making.Problems));

            Hero hero = making.Finish();

            Assert.NotNull(hero);

            return hero;
        }

        [Fact]
        public void ItIsATestCampaignAndEveryKeyItUsesHasEnglish()
        {
            Package pack = Pack();

            Assert.Contains("test", pack.Manifest.Tags);
            Assert.Equal("old_mill", pack.Manifest.Start);

            DialogueBook book = DialogueBook.Read(Path.Combine(Folder, Package.DialogueFolder), pack.Id);

            Assert.True(book.Problems.Count == 0, string.Join("\n", book.Problems));

            string csv = File.ReadAllText(Path.Combine(Folder, Package.LocaleFolder, pack.Id + ".csv"));

            IReadOnlyList<string> missing = Locale.Audit(csv, pack.Keys().Concat(book.Keys()));

            Assert.True(missing.Count == 0, string.Join("\n", missing));
        }

        // the table's side of a fight, played through the combat screen's own session by a player who
        // plays it plainly: rage or a stance first, heal when low, the best previewed damage, walk in
        static Outcome Play(Battle battle, Library library)
        {
            var session = new CombatSession(battle, library.Items, library.Forms);

            return new AutoPlayer().Play(session);
        }

        sealed class Playthrough
        {
            public int Fights, Shops, Deaths, Reloads, Lines, Choices;
            public readonly List<string> Maps = new List<string>();
            public bool Saved;
            public readonly List<string> Starts = new List<string>();
        }

        // plays from wherever the run is to the end, reloading on death; returns the run it ended on
        CampaignRun PlayOut(CampaignRun run, Library library, Package pack, SaveLibrary saves,
                            IRng rng, Playthrough seen)
        {
            for (int step = 0; step < 5000; step++)
            {
                switch (run.Now)
                {
                    case Scene.Line:
                        seen.Lines++;
                        run.Next();
                        break;

                    case Scene.Choice:
                        // the first choice goes shopping; every later one takes the first option
                        run.Choose(seen.Choices++ == 0 ? 1 : 0);
                        break;

                    case Scene.Shop:
                    {
                        seen.Shops++;

                        Merchant shop = run.Shop.Open(library.Items);
                        Item potion = library.Items.Find("potion_of_healing");

                        shop.Buy(run.Hero.Pack, potion, run.Hero.Actor, run.Hero.Class.Id);

                        run.LeaveShop();
                        break;
                    }

                    case Scene.Fight:
                    {
                        seen.Fights++;
                        seen.Maps.Add(run.Fight.MapId);
                        seen.Starts.Add($"{run.Fight.MapId}@{run.Hero.Actor.Health.Current}/{run.Hero.Actor.Health.Maximum}");

                        Battle battle = run.BattleFor(new StandardResolver(rng));

                        Assert.NotNull(battle);
                        Assert.NotEmpty(battle.Monsters);

                        Outcome outcome = Play(battle, library);

                        run.EndFight(outcome == Outcome.Open ? Outcome.Fled : outcome);

                        // save and load once, after the first fight: the story, the hero and
                        // the variables come back and it plays on from the node it was on
                        if (!seen.Saved && run.Now != Scene.Dead)
                        {
                            seen.Saved = true;

                            SaveGame game = run.Capture();
                            Read<SaveGame> back = SaveReader.Parse(SaveWriter.Write(game));

                            Assert.True(back.Ok, string.Join("\n", back.Problems));

                            CampaignRun again = CampaignRun.Resume(back.Value, library, pack,
                                new StandardResolver(rng), rng, saves, out var problems);

                            Assert.NotNull(again);
                            Assert.Empty(problems.Where(p => p.IsAFault));
                            Assert.Equal(run.Hero.Level, again.Hero.Level);
                            Assert.Equal(run.Hero.Pack.Gold, again.Hero.Pack.Gold);

                            run = again;
                            run.Continue();
                        }

                        break;
                    }

                    case Scene.Dead:
                    {
                        seen.Deaths++;

                        SaveShelf.Saved last = saves.ForReload(pack.Id, run.Slot);

                        Assert.NotNull(last);


                        run = CampaignRun.Resume(last.Game, library, pack, new StandardResolver(rng),
                                                 rng, saves, out _);
                        seen.Reloads++;
                        run.Continue();
                        break;
                    }

                    case Scene.Over:
                        return run;
                }

                // a level with an improvement waiting takes the suggested one, as the level-up
                // screen's "suggested" button does
                while (run.Hero.PendingImprovements > 0) run.Hero.ImproveAsSuggested();
            }

            throw new InvalidOperationException($"the campaign never ended: {run}, fights {seen.Fights}, deaths {seen.Deaths}, " +
                                                $"lines {seen.Lines}, maps {string.Join(",", seen.Maps)}");
        }

        [Theory]
        [InlineData("fighter", 3)]
        [InlineData("rogue", 11)]
        [InlineData("cleric", 7)]
        public void ItPlaysFromTheFirstLineToTheLast(string className, int seed)
        {
            Library library = Library.Srd();
            Package pack = Pack();
            var rng = new SeededRng(seed);
            var saves = new SaveLibrary(_saves);

            Hero hero = Make(library, className);
            var run = new CampaignRun(library, pack, hero, new StandardResolver(rng), rng, saves);
            var seen = new Playthrough();

            run.Start();
            run = PlayOut(run, library, pack, saves, rng, seen);

            Assert.Equal(Scene.Over, run.Now);

            // a death reloads the fight's autosave; more than a handful means the fights are out of
            // reach for a hero played the plain way
            Assert.True(seen.Deaths <= 6, $"{className} died {seen.Deaths} times: " +
                        string.Join(" ", seen.Starts.GroupBy(x => x).Select(g => $"{g.Key}x{g.Count()}")));
            Assert.Empty(run.Talk.Complained);

            // the cellar, maybe the road, and the camp - each on its own map
            Assert.InRange(seen.Fights - seen.Deaths, 2, 3);
            Assert.Contains("mill_cellar", seen.Maps);
            Assert.Contains("mill_yard", seen.Maps);
            Assert.True(seen.Shops >= 1);

            // milestone levels to 4, and the improvement spent
            Assert.Equal(4, run.Hero.Level);
            Assert.Equal(0, run.Hero.PendingImprovements);
            Assert.NotEmpty(run.Hero.Improvements);

            // a long rest at the end: whole again
            Assert.Equal(run.Hero.Actor.Health.Maximum, run.Hero.Actor.Health.Current);

            Assert.True(run.Hero.Pack.Gold > 0);
            Assert.True(run.Hero.Pack.CountOf("potion_of_healing") >= 1);

            // autosaves on the way: a chapter, fights, rests, levels
            IReadOnlyList<SaveShelf.Saved> all = saves.Of(pack.Id, 0);
            Assert.Contains(all, s => s.Game.Kind == SaveKind.FightStart);
            Assert.Contains(all, s => s.Game.Kind == SaveKind.LevelUp);
            Assert.Contains(all, s => s.Game.Kind == SaveKind.Rest);
        }
    
        [Fact]
        public void AShopAndARestAreStoryVerbs()
        {
            Request shop = Request.Parse("shop millbrook_store", out string problem);

            Assert.Null(problem);
            Assert.Equal(RequestKind.Shop, shop.Kind);
            Assert.Equal("millbrook_store", shop.Id);

            Assert.Equal(RequestKind.Rest, Request.Parse("rest long", out _).Kind);

            Assert.Null(Request.Parse("rest sideways", out problem));
            Assert.Contains("rest short", problem);

            MerchantDef store = Pack().Merchant("millbrook_store");

            Assert.NotNull(store);
            Assert.Equal(50, store.SellPercent);
            Assert.Contains("potion_of_healing", store.Stock);
        }

        [Fact]
        public void ALoadAfterAWonFightDoesNotFightItAgain()
        {
            Library library = Library.Srd();
            Package pack = Pack();
            var rng = new SeededRng(3);

            var run = new CampaignRun(library, pack, Make(library, "fighter"), new StandardResolver(rng), rng);

            run.Start();

            while (run.Now != Scene.Fight)
            {
                if (run.Now == Scene.Choice) run.Choose(2);
                else run.Next();
            }

            run.EndFight(Outcome.HeroesWon);

            SaveGame saved = run.Capture();

            Assert.Equal("cellar", saved.Node);
            Assert.Contains(saved.Steps, s => s.StartsWith("answer:"));

            CampaignRun back = CampaignRun.Resume(SaveReader.Parse(SaveWriter.Write(saved)).Value,
                                                  library, pack, new StandardResolver(rng), rng, null, out _);

            back.Continue();

            Assert.NotEqual(Scene.Fight, back.Now);
            Assert.Equal("cellar", back.Talk.Node);
            Assert.Empty(back.Talk.Complained);
        }
}
}
