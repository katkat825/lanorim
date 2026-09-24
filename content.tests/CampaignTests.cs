using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;
using Xunit;

namespace Content.Tests
{
    // The campaign package format. Everything here is about the LOADER's contract rather than any
    // particular campaign: a pack says what it is, a bad pack says why, and a pack that is refused
    // is refused whole.
    public class CampaignTests
    {
        static string Pack(string id = "ash_yard", string extra = "") =>
            $@"{{
                 ""id"": ""{id}"",
                 ""kind"": ""campaign"",
                 ""format"": {ContentFormat.Current},
                 ""engine"": ""{Core.EngineVersion.Current}"",
                 ""author"": ""Kathleen"",
                 ""chapters"": [ {{ ""id"": ""the_yard"", ""maps"": [ ""yard"" ] }} ]
                 {extra}
               }}";

        [Fact]
        public void AWellFormedManifestReads()
        {
            Read<Manifest> read = ManifestReader.Parse(Pack(), ManifestReader.PackFileName);

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));

            Manifest manifest = read.Value;

            Assert.Equal("ash_yard", manifest.Id);
            Assert.Equal(PackKind.Campaign, manifest.Kind);
            Assert.True(manifest.IsPlayable);
            Assert.Equal("the_yard", manifest.Start);
            Assert.Equal(new[] { "yard" }, manifest.Chapters.Single().Maps);
        }

        [Fact]
        public void TheFolderAndTheIdHaveToMatch()
        {
            Read<Manifest> read = ManifestReader.Parse(Pack(), ManifestReader.PackFileName,
                                                       folder: "somewhere_else");

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("folder"));
        }

        [Fact]
        public void ANameInTheManifestIsToldWhereNamesLive()
        {
            Read<Manifest> read = ManifestReader.Parse(
                Pack(extra: @", ""name"": ""The Ash Yard"""), ManifestReader.PackFileName);

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("locale"));
        }

        [Fact]
        public void AFormatThisBuildCannotReadIsRefusedWithADirection()
        {
            string json = Pack().Replace($"\"format\": {ContentFormat.Current}",
                                         "\"format\": 99");

            Read<Manifest> read = ManifestReader.Parse(json, ManifestReader.PackFileName);

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("update the game"));
        }

        [Fact]
        public void APackCannotDependOnItself()
        {
            Read<Manifest> read = ManifestReader.Parse(
                Pack(extra: @", ""dependencies"": [ ""ash_yard"" ]"), ManifestReader.PackFileName);

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("cannot depend on itself"));
        }

        [Fact]
        public void StartingAtAChapterThatIsNotThereIsRefused()
        {
            Read<Manifest> read = ManifestReader.Parse(
                Pack(extra: @", ""start"": ""nowhere"""), ManifestReader.PackFileName);

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("no such chapter"));
        }

        [Fact]
        public void NotJsonIsSaidPlainlyRatherThanThrown()
        {
            Read<Manifest> read = ManifestReader.Parse("{ this is not json",
                                                       ManifestReader.PackFileName);

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("not JSON"));
        }

        // A CAMPAIGN ID BECOMES A SEGMENT OF EVERY KEY THE CAMPAIGN EMITS, so an id the key grammar
        // would refuse has to be caught here rather than in a later locale audit, where it would
        // read as a hundred missing keys instead of one bad name.
        [Theory]
        [InlineData("Ash_Yard")]
        [InlineData("ash yard")]
        [InlineData("ash.yard")]
        [InlineData("ash-yard")]
        public void AnIdTheKeyGrammarWouldRefuseIsRefusedHere(string id)
        {
            Assert.False(ContentId.IsCampaign(id));
        }

        [Fact]
        public void EveryKeyAManifestPromisesIsWellFormed()
        {
            Manifest manifest = ManifestReader.Parse(Pack(), ManifestReader.PackFileName).Value;

            Assert.All(manifest.Keys(),
                       key => Assert.True(Core.Localization.KeyConventions.IsWellFormed(key), key));
        }


        // --- a folder on disk -------------------------------------------------------------------

        sealed class Folder : IDisposable
        {
            public Folder(string name)
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                              "lanorim_" + Guid.NewGuid().ToString("N"), name);
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public Folder Write(string relative, string text)
            {
                string full = System.IO.Path.Combine(Path, relative);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
                File.WriteAllText(full, text);
                return this;
            }

            public void Dispose()
            {
                try { Directory.Delete(System.IO.Path.GetDirectoryName(Path), true); }
                catch (IOException) { }
            }
        }

        // A .map IS JSON WITH THE GRID AS A STRING INSIDE IT. The grid half stays
        // hand-editable on purpose - that is what makes a text editor a usable tool for a
        // Workshop author, which is the whole reason campaigns stay loose on disk.
        const string OneRoom = @"{
  ""format"": 1,
  ""columns"": 3,
  ""rows"": 2,
  ""map"": ""\n+-+-+-+\n|@ . 1|\n+ + + +\n|. . .|\n+-+-+-+"",
  ""props"": []
}
";

        [Fact]
        public void AFolderWithAManifestAndAMapReads()
        {
            using var folder = new Folder("ash_yard");

            folder.Write(ManifestReader.PackFileName, Pack())
                  .Write("maps/yard.map", OneRoom);

            Package package = Package.Read(folder.Path);

            Assert.True(package.Sound, string.Join("; ", package.Problems.Select(p => p.ToString())));
            Assert.Equal("ash_yard", package.Id);
            Assert.True(package.Maps.ContainsKey("yard"));
        }

        // A chapter that names a map nobody shipped is a campaign that stops mid-play, and the
        // whole point of reading it at load is that it stops on the shelf instead.
        [Fact]
        public void AChapterNamingAMapThatIsNotThereIsRefused()
        {
            using var folder = new Folder("ash_yard");

            folder.Write(ManifestReader.PackFileName, Pack());

            Package package = Package.Read(folder.Path);

            Assert.False(package.Sound);
            Assert.Contains(package.Problems, p => p.What.Contains("yard"));
        }

        [Fact]
        public void AFolderWithNoManifestSaysWhichFileIsMissing()
        {
            using var folder = new Folder("ash_yard");

            Package package = Package.Read(folder.Path);

            Assert.False(package.Sound);
            Assert.Contains(package.Problems,
                            p => p.What.Contains(ManifestReader.PackFileName));
        }

        [Fact]
        public void AFolderNameNobodyRecognisesIsListedBack()
        {
            using var folder = new Folder("ash_yard");

            folder.Write(ManifestReader.PackFileName, Pack())
                  .Write("maps/yard.map", OneRoom)
                  .Write("monster/goblin.json", "[]");

            Package package = Package.Read(folder.Path);

            Assert.Contains(package.Problems, p => p.What.Contains(Package.MonstersFolder + "/"));
        }


        // --- the shelf ---------------------------------------------------------------------------

        [Fact]
        public void APackWaitingOnSomethingUninstalledIsNotInPlay()
        {
            using var needs = new Folder("ash_yard");

            needs.Write(ManifestReader.PackFileName,
                        Pack(extra: @", ""dependencies"": [ ""grimdark_minis"" ]"))
                 .Write("maps/yard.map", OneRoom);

            Shelf shelf = Shelf.Of(new[] { Package.Read(needs.Path) });

            Assert.Empty(shelf.Loaded);
            Assert.Contains(shelf.Problems, p => p.What.Contains("grimdark_minis"));
        }

        [Fact]
        public void ADependencyResolvesWhicheverOrderTheFoldersWereWalkedIn()
        {
            using var minis = new Folder("grimdark_minis");
            using var campaign = new Folder("ash_yard");

            minis.Write(ManifestReader.PackFileName, $@"{{
                ""id"": ""grimdark_minis"",
                ""kind"": ""minis"",
                ""format"": {ContentFormat.Current},
                ""engine"": ""{Core.EngineVersion.Current}""
            }}");

            campaign.Write(ManifestReader.PackFileName,
                           Pack(extra: @", ""dependencies"": [ ""grimdark_minis"" ]"))
                    .Write("maps/yard.map", OneRoom);

            Package a = Package.Read(campaign.Path);
            Package b = Package.Read(minis.Path);

            // the campaign first, so its dependency has not been seen yet when it is added
            Assert.Equal(2, Shelf.Of(new[] { a, b }).Loaded.Count());
            Assert.Equal(2, Shelf.Of(new[] { b, a }).Loaded.Count());
        }

        [Fact]
        public void TwoPacksCannotShareAnId()
        {
            using var one = new Folder("ash_yard");
            using var two = new Folder("ash_yard");

            one.Write(ManifestReader.PackFileName, Pack()).Write("maps/yard.map", OneRoom);
            two.Write(ManifestReader.PackFileName, Pack()).Write("maps/yard.map", OneRoom);

            Shelf shelf = Shelf.Of(new[] { Package.Read(one.Path), Package.Read(two.Path) });

            Assert.Contains(shelf.Problems, p => p.What.Contains("cannot share an id"));
        }

        // A campaign's content is laid ON TOP of the SRD's and the SRD library is never edited -
        // so two campaigns can never reach each other through a library somebody mutated.
        [Fact]
        public void LayingACampaignOnTheSrdLeavesTheSrdAlone()
        {
            using var folder = new Folder("ash_yard");

            folder.Write(ManifestReader.PackFileName, Pack())
                  .Write("maps/yard.map", OneRoom);

            Content.Schema.Library srd = Content.Schema.Library.Srd();
            int monsters = srd.Bestiary.Count;

            Content.Schema.Library with = srd.With(Package.Read(folder.Path));

            Assert.Equal(monsters, srd.Bestiary.Count);
            Assert.NotSame(srd, with);
        }
    }
}
