using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Dialogue;
using Content.Play;
using Content.Schema;
using Content.Sheet;
using Core.Dice;
using Core.Resolution;

namespace Content.Tests
{
    // WHAT CAMPAIGNS READ AND WRITE (cc_task_i Part 4): the hero's story variables and has_item, a campaign without
    // the starting shop, and its own consequences added to the game's pool (or in place of it)
    public class CampaignAuthoringTests : IDisposable
    {
        static readonly Library Srd = Library.Srd();

        readonly string _temp = Path.Combine(Path.GetTempPath(), "lanorim_authoring_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, true);
        }

        const string Reads = @"title: start
speaker: dm
---
<<if $hero_class == ""fighter"" and $hero_level == 1 and not $hero_casts and $hero_figure == 1 and $hero_name == ""Tamsin"">>
    A fighter called Tamsin. #line:fighter
<<else>>
    Someone else. #line:someone
<<endif>>
<<if has_item(""keepsake"") and not has_item(""rope_of_climbing"")>>
    You touch the keepsake. #line:keepsake
<<endif>>
===
";

        static Conversation Talk(Hero hero, string source, out DialogueBook book)
        {
            book = DialogueBook.Of("reads", ("start.yarn", source));

            Assert.True(book.Program != null, string.Join("; ", book.Problems));

            var store = new StoryVariables { Live = name => HeroVariables.Read(hero, name) };
            var talk = new Conversation(book, store);
            talk.Lets(HeroVariables.HasItem, id => HeroVariables.Has(hero, id));

            Assert.True(talk.Start("start"));
            return talk;
        }

        [Fact]
        public void AStoryReadsTheHeroAndAsksWhatIsInThePack()
        {
            Hero hero = SampleCampaignTests.Make(Srd, "fighter");
            Conversation talk = Talk(hero, Reads, out DialogueBook book);

            Assert.Empty(book.Problems);
            Assert.EndsWith("fighter", talk.Saying.Key);

            talk.Advance();
            Assert.EndsWith("keepsake", talk.Saying.Key);
            Assert.Empty(talk.Complained);
        }

        [Fact]
        public void TheVariablesFollowTheHeroAndAreNeverStored()
        {
            Hero mage = SampleCampaignTests.Make(Srd, "mage");
            mage.Figure = 1;

            Assert.Equal("mage", HeroVariables.Read(mage, HeroVariables.Class));
            Assert.Equal(true, HeroVariables.Read(mage, HeroVariables.Casts));
            Assert.Equal(2f, HeroVariables.Read(mage, HeroVariables.Figure));

            mage.LevelTo(3);
            Assert.Equal(3f, HeroVariables.Read(mage, HeroVariables.Level));

            var store = new StoryVariables { Live = name => HeroVariables.Read(mage, name) };
            store.SetValue(HeroVariables.Level, 9f);

            Assert.True(store.TryGetValue(HeroVariables.Level, out float level));
            Assert.Equal(3f, level);
            Assert.Empty(store.Numbers);
        }

        [Fact]
        public void SettingAHeroVariableIsACaution()
        {
            DialogueBook book = DialogueBook.Of("sets", ("start.yarn", @"title: start
---
<<set $hero_level to 9>>
Hello. #line:hello
==="));

            Assert.NotNull(book.Program);
            Assert.Contains(book.Problems, p => p.IsACaution && p.Where == HeroVariables.Level);
        }

        // --- the campaign's consequences -----------------------------------------------------------------

        [Fact]
        public void ACampaignsConsequencesAreAddedToTheGamesWhileItRuns()
        {
            Package pack = Package.Read(CrucibleTests.Folder);
            ConsequencePool pool = pack.PoolOn(Srd.Consequences);

            Assert.Equal(Srd.Consequences.All.Count + 2, pool.All.Count);
            Assert.Contains(pool.All, c => c.Id == "sand_in_your_eye");
            Assert.Contains(pool.All, c => c.Id == "stubbed_toe");

            // the game's own pool is untouched, and a campaign with none of its own uses it as it is
            Assert.DoesNotContain(Srd.Consequences.All, c => c.Id == "sand_in_your_eye");
            Assert.Same(Srd.Consequences, Package.Read(SampleCampaignTests.Folder).PoolOn(Srd.Consequences));
        }

        [Fact]
        public void AFlavourConsequenceIsALineAndNothingElse()
        {
            Hero hero = SampleCampaignTests.Make(Srd, "fighter");
            int hp = hero.Actor.Health.Current, copper = hero.Pack.Copper;

            Consequence sand = Package.Read(CrucibleTests.Folder).Consequences.Single(c => c.Id == "sand_in_your_eye");
            Consequences.Visit visit = Consequences.Befall(sand, new StandardResolver(new SeededRng(1)), hero.Actor, hero.Pack);

            Assert.Equal("consequence.sand_in_your_eye.line", visit.LineKey);
            Assert.Equal(hp, hero.Actor.Health.Current);
            Assert.Equal(copper, hero.Pack.Copper);
        }

        [Fact]
        public void ACampaignCanReplaceThePoolButNeedsBothSides()
        {
            string copy = Copy(@"""consequences"": ""add""", @"""consequences"": ""replace""");
            Package pack = Package.Read(copy);

            Assert.True(pack.Sound, string.Join("; ", pack.Problems));
            Assert.Equal(2, pack.PoolOn(Srd.Consequences).All.Count);

            File.WriteAllText(Path.Combine(copy, "consequences", "arena.json"),
                              @"{ ""consequences"": [ { ""id"": ""sand_in_your_eye"", ""polarity"": ""bane"", ""kind"": ""flavour"" } ] }");

            Assert.Contains(Package.Read(copy).Faults, p => p.What.Contains("no boon"));
        }

        [Fact]
        public void AnAddedConsequenceCantTakeOneOfTheGamesIds()
        {
            string copy = Copy("", "");
            File.WriteAllText(Path.Combine(copy, "consequences", "arena.json"),
                              @"{ ""consequences"": [ { ""id"": ""stubbed_toe"", ""polarity"": ""bane"", ""kind"": ""flavour"" } ] }");

            Assert.Contains(Package.Read(copy).Faults, p => p.Where == "stubbed_toe");
        }

        [Fact]
        public void TheRefereeDrawsFromTheCampaignsPool()
        {
            string copy = Copy(@"""consequences"": ""add""", @"""consequences"": ""replace""");
            Package pack = Package.Read(copy);
            Hero hero = SampleCampaignTests.Make(Srd, "fighter");
            var referee = new Referee(hero, new StandardResolver(new SeededRng(1)), new Core.Tables.GmScreen(new SeededRng(2)), Srd, pack);

            Consequences.Visit visit = referee.Consequence(new Attempt(RollKind.Check, D20Roll.Fixed(1), 10));

            Assert.Equal("consequence.sand_in_your_eye.line", visit.LineKey);
        }

        // the fixture, copied to change, with one line of pack.json swapped
        string Copy(string from, string to)
        {
            string copy = Path.Combine(_temp, "crucible_fixture");

            foreach (string file in Directory.EnumerateFiles(CrucibleTests.Folder, "*", SearchOption.AllDirectories))
            {
                string there = Path.Combine(copy, Path.GetRelativePath(CrucibleTests.Folder, file));
                Directory.CreateDirectory(Path.GetDirectoryName(there));
                File.Copy(file, there);
            }

            string manifest = Path.Combine(copy, "pack.json");
            if (from.Length > 0) File.WriteAllText(manifest, File.ReadAllText(manifest).Replace(from, to));

            return copy;
        }
    }
}
