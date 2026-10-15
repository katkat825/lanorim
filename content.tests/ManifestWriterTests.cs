using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Maps;
using Content.Schema;
using Content.Workshop;
using Core.Combat;

namespace Content.Tests
{
    // cc_task_m Part 3 (WORKSHOP_DESIGN.md principle 6): pack.json had a reader and no writer. read -> write -> read gives
    // the same manifest, on the sample campaign and on a wizard-made pack, and the sample's own file comes back byte for
    // byte. The campaign's locale CSV is edited the same way (LocaleSheet): every row it doesn't change, kept
    public class ManifestWriterTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_manifest_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        static Manifest Parse(string json, string folder) =>
            ManifestReader.Parse(json, ManifestReader.PackFileName, folder) is { Ok: true } read
                ? read.Value
                : throw new Xunit.Sdk.XunitException(string.Join("; ", ManifestReader.Parse(json, ManifestReader.PackFileName, folder).Problems));

        // everything a manifest says, as one line, so two can be compared whole
        internal static string Said(Manifest m) =>
            $"{m.Id}|{m.Kind}|{m.Format}|{m.Engine}|{m.Author}|{string.Join(",", m.Tags)}|{m.Preview}|{string.Join(",", m.Dependencies)}|" +
            $"{string.Join(";", m.Chapters.Select(c => c.Id + ":" + string.Join(",", c.Maps)))}|{m.Start}|{m.GmScreen}|{m.Rules}";

        [Fact]
        public void TheSampleCampaignsPackJsonComesBackByteForByte()
        {
            string file = Path.Combine(SampleCampaignTests.Folder, ManifestReader.PackFileName);
            string text = File.ReadAllText(file).Replace("\r\n", "\n");

            Manifest read = Parse(text, "sample_millbrook");
            string written = ManifestWriter.Write(read);

            Assert.Equal(text, written);
            Assert.Equal(Said(read), Said(Parse(written, "sample_millbrook")));
        }

        [Fact]
        public void EveryRuleAndListRoundTripsAndADefaultIsLeftOut()
        {
            var draft = new CampaignDraft
            {
                Id = "the_long_road", Author = "K", Tags = { "horror", "mature" }, Preview = "preview.png",
                Dependencies = { "grimdark_minis" }, GmScreen = "cave", Defeat = Defeat.Continues, Leveling = Leveling.Xp,
                Characters = 12, StartingShop = false, Consequences = Layering.Replace, Start = "two",
                Chapters = { new ChapterDraft { Id = "one", Maps = { "a", "b" } }, new ChapterDraft { Id = "two", Maps = { "c" } } },
            };

            string written = ManifestWriter.Write(draft.ToManifest());
            Manifest read = Parse(written, "the_long_road");

            Assert.Equal(Said(draft.ToManifest()), Said(read));
            Assert.Equal(written, ManifestWriter.Write(read));

            // the game's own rules, a blank screen and no lists: none of them written, as a hand-made pack leaves them out
            string plain = ManifestWriter.Write(new CampaignDraft { Id = "plain", Chapters = { new ChapterDraft { Id = "one", Maps = { "a" } } } }.ToManifest());

            foreach (string unsaid in new[] { "defeat", "leveling", "characters", "starting_shop", "consequences", "gm_screen", "tags", "dependencies", "preview", "author" })
                Assert.DoesNotContain($"\"{unsaid}\"", plain);

            Assert.Equal(CampaignRules.Default.ToString(), Parse(plain, "plain").Rules.ToString());
        }

        [Fact]
        public void AWizardMadePackRoundTrips()
        {
            var wizard = new CampaignWizard(_ => false);
            wizard.Draft.Name = "Round Trip";
            wizard.Draft.Defeat = Defeat.Continues;
            wizard.Draft.GmScreen = "plains";
            wizard.Tag("mystery", true);
            wizard.ChapterTitle = "The Gate";

            string folder = wizard.Finish(_root, MapPlace.Library(Path.Combine(_root, "maps")), "Your story starts here.", out string problem);
            Assert.Null(problem);

            string file = Path.Combine(folder, ManifestReader.PackFileName);
            string text = File.ReadAllText(file);
            Manifest read = Parse(text, "round_trip");

            Assert.Equal(text, ManifestWriter.Write(read));
            Assert.Equal(Said(read), Said(Parse(ManifestWriter.Write(read), "round_trip")));
        }

        [Fact]
        public void ALocaleSheetKeepsEveryRowItIsntToldToChange()
        {
            string csv = File.ReadAllText(Path.Combine(SampleCampaignTests.Folder, "locale", "sample_millbrook.csv")).Replace("\r\n", "\n");

            LocaleSheet sheet = LocaleSheet.Of(csv);
            Assert.Equal(csv, sheet.Text());

            sheet.Set("campaign.sample_millbrook.name", "Millbrook, renamed");
            sheet.Set("actor.ahmed.name", "Ahmed");
            Assert.True(sheet.Remove("actor.oda.name"));

            LocaleSheet again = LocaleSheet.Of(sheet.Text());
            Assert.Equal("Millbrook, renamed", again.Get("campaign.sample_millbrook.name"));
            Assert.Equal("Ahmed", again.Get("actor.ahmed.name"));
            Assert.False(again.Has("actor.oda.name"));
            Assert.Equal(sheet.Keys.Count(), again.Keys.Count());

            // a new row goes in key order; a second language's cells stay where they were
            Assert.Equal("actor.ahmed.name", again.Keys.First());
            LocaleSheet two = LocaleSheet.Of("keys,en,fr\ncampaign.x.name,Fox,Renard\n");
            two.Set("campaign.x.name", "Wolf");
            Assert.Equal("keys,en,fr\ncampaign.x.name,Wolf,Renard\n", two.Text());
        }

        [Theory]
        [InlineData("The Old Mill", "the_old_mill")]
        [InlineData("  Café   Noir!! ", "cafe_noir")]
        [InlineData("2nd Chance: Part II", "2nd_chance_part_ii")]
        [InlineData("???", "campaign")]
        public void AnIdIsMadeFromAName(string name, string id) => Assert.Equal(id, ContentId.From(name, "campaign"));

        [Fact]
        public void AFreeIdTakesTheNextNumber()
        {
            var taken = new[] { "mill", "mill_2" };
            Assert.Equal("mill_3", ContentId.Free("mill", taken.Contains));
            Assert.Equal("road", ContentId.Free("road", taken.Contains));
        }
    }
}
