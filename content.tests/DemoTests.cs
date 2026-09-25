using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Tests
{
    // Phase D: the demo is the full game with less on the shelf, decided by one small data file
    public class DemoTests : IDisposable
    {
        readonly string _root =
            Path.Combine(Path.GetTempPath(), "lanorim_demo_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        const string Room = @"{
  ""format"": 1,
  ""columns"": 3,
  ""rows"": 2,
  ""map"": ""\n+-+-+-+\n|@ . 1|\n+ + + +\n|. . .|\n+-+-+-+"",
  ""props"": []
}
";

        Package Campaign(string id)
        {
            string folder = Path.Combine(_root, id);

            Directory.CreateDirectory(Path.Combine(folder, Package.MapsFolder));

            File.WriteAllText(Path.Combine(folder, ManifestReader.PackFileName), $@"{{
                 ""id"": ""{id}"",
                 ""kind"": ""campaign"",
                 ""format"": {ContentFormat.Current},
                 ""engine"": ""{Core.EngineVersion.Current}"",
                 ""author"": ""Kathleen"",
                 ""chapters"": [ {{ ""id"": ""the_bridge"", ""maps"": [ ""yard"" ] }} ]
               }}");

            File.WriteAllText(Path.Combine(folder, Package.MapsFolder, "yard.map"), Room);

            Package package = Package.Read(folder);

            Assert.True(package.Sound, string.Join("; ", package.Problems));

            return package;
        }

        const string Cut = @"{
  ""campaigns"": [""first_steps"", ""the_goat""],
  ""ends"": [ { ""campaign"": ""the_goat"", ""chapter"": ""the_bridge"" } ]
}";

        [Fact]
        public void TheDemoCutReads()
        {
            Assert.True(DemoCut.TryRead(Cut, out DemoCut cut, out IReadOnlyList<string> problems),
                        string.Join("; ", problems));

            Assert.True(cut.Ships("the_goat"));
            Assert.False(cut.Ships("the_long_road"));
            Assert.True(cut.IsTheEnd("the_goat", "the_bridge"));
            Assert.False(cut.IsTheEnd("first_steps", "the_bridge"));
        }

        [Fact]
        public void ADemoWithNoEndOrEndingSomewhereElseIsRefused()
        {
            DemoCut.TryRead(@"{ ""campaigns"": [""the_goat""] }", out _,
                            out IReadOnlyList<string> endless);

            Assert.Contains(endless, p => p.Contains("no end"));

            DemoCut.TryRead(@"{ ""campaigns"": [""the_goat""],
                               ""ends"": [ { ""campaign"": ""elsewhere"", ""chapter"": ""x"" } ] }",
                            out _, out IReadOnlyList<string> elsewhere);

            Assert.Contains(elsewhere, p => p.Contains("elsewhere"));
        }

        [Fact]
        public void TheDemoShelfOffersTheCutAndNothingElse()
        {
            Shelf shelf = Shelf.Of(new[] { Campaign("first_steps"), Campaign("the_goat"),
                                           Campaign("the_long_road") });

            DemoCut.TryRead(Cut, out DemoCut cut, out _);

            Assert.Equal(3, shelf.Offered(Edition.Full, cut).Count());

            Assert.Equal(new[] { "first_steps", "the_goat" },
                         shelf.Offered(Edition.Demo, cut).Select(p => p.Id).OrderBy(id => id));
        }

        [Fact]
        public void OnlyTheDemoEndsAndOnlyTheFullGameCountsAchievements()
        {
            DemoCut.TryRead(Cut, out DemoCut cut, out _);

            Assert.True(Edition.Demo.EndsTheDemo(cut, "the_goat", "the_bridge"));
            Assert.False(Edition.Full.EndsTheDemo(cut, "the_goat", "the_bridge"));

            Assert.True(Edition.Full.Achievements());
            Assert.False(Edition.Demo.Achievements());

            Assert.Equal(Edition.Demo, Editions.From(demoFeature: true));
        }
    }
}
