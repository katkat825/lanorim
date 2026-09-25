using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Encounters;
using Core.Localization;
using Core.Tables;
using Xunit;

namespace Content.Tests
{
    // random-encounter tables as a campaign ships them: the reader holds a table to its shape, and
    // the package holds it to the monsters and maps that actually exist.
    public class EncounterTests
    {
        const string Road = @"{
  ""tables"": [
    {
      ""id"": ""north_road"",
      ""trigger"": { ""roll"": ""1d20"", ""at_least"": 15 },
      ""entries"": [
        { ""id"": ""quiet"", ""kind"": ""nothing"", ""weight"": 3 },
        { ""id"": ""crows"", ""kind"": ""line"", ""weight"": 2 },
        { ""id"": ""goblins"", ""kind"": ""fight"", ""weight"": 2, ""map"": ""yard"",
          ""monsters"": [ { ""monster"": ""goblin"", ""count"": ""1d4"" },
                          { ""monster"": ""goblin_boss"" } ] }
      ]
    }
  ]
}";

        static IReadOnlyList<string> Problems(string text)
        {
            EncounterReader.TryRead(text, out _, out IReadOnlyList<string> problems);
            return problems;
        }

        // one table, one entry, with a piece swapped out
        static string One(string trigger = @"""trigger"": { ""roll"": ""1d20"", ""at_least"": 15 },",
                          string entry = @"{ ""id"": ""crows"", ""kind"": ""line"" }",
                          string extra = "") =>
            $@"{{ ""tables"": [ {{ ""id"": ""north_road"", {trigger} {extra}
                                   ""entries"": [ {entry} ] }} ] }}";

        [Fact]
        public void AWellFormedTableReads()
        {
            Assert.True(EncounterReader.TryRead(Road, out IReadOnlyList<EncounterTable> tables,
                                                out IReadOnlyList<string> problems),
                        string.Join("; ", problems));

            EncounterTable table = tables.Single();

            Assert.Equal("north_road", table.Id);
            Assert.Equal(15, table.Trigger.AtLeast);
            Assert.Equal(Visibility.Hidden, table.Visibility);
            Assert.Equal(7, table.TotalWeight);

            EncounterEntry goblins = table.Entries.Single(e => e.Kind == EntryKind.Fight);

            Assert.Equal("yard", goblins.Map);
            Assert.Equal("1d4", goblins.Monsters[0].Count.ToString());
            Assert.Equal("1", goblins.Monsters[1].Count.ToString());
        }

        [Fact]
        public void NoTriggerIsATableThatAlwaysFires()
        {
            EncounterReader.TryRead(One(trigger: ""), out IReadOnlyList<EncounterTable> tables,
                                    out IReadOnlyList<string> problems);

            Assert.Empty(problems);
            Assert.True(tables.Single().Trigger.IsAlways);
        }

        [Fact]
        public void ATableCanBeRolledInTheOpen()
        {
            EncounterReader.TryRead(One(extra: @"""rolled"": ""shown"","),
                                    out IReadOnlyList<EncounterTable> tables, out _);

            Assert.Equal(Visibility.Shown, tables.Single().Visibility);
        }

        [Theory]
        [InlineData(@"""trigger"": { ""roll"": ""1d20"", ""at_least"": 21 },", "never")]
        [InlineData(@"""trigger"": { ""roll"": ""1d20"", ""at_least"": 1 },", "always")]
        [InlineData(@"""trigger"": { ""roll"": ""1d20"" },", "at_least")]
        [InlineData(@"""trigger"": { ""roll"": ""5"", ""at_least"": 3 },", "rolls dice")]
        [InlineData(@"""trigger"": { ""roll"": ""1d7"", ""at_least"": 3 },", "not dice")]
        public void ABadTriggerIsRefused(string trigger, string said)
        {
            Assert.Contains(Problems(One(trigger: trigger)), p => p.Contains(said));
        }

        [Theory]
        [InlineData(@"{ ""id"": ""crows"", ""kind"": ""line"", ""weight"": 0 }", "weight")]
        [InlineData(@"{ ""id"": ""crows"", ""kind"": ""line"", ""weight"": ""heavy"" }", "weight")]
        [InlineData(@"{ ""id"": ""crows"", ""kind"": ""ambush"" }", "not a kind of entry")]
        [InlineData(@"{ ""id"": ""Crows"", ""kind"": ""line"" }", "not an entry id")]
        [InlineData(@"{ ""id"": ""wolves"", ""kind"": ""fight"" }", "needs 'monsters'")]
        [InlineData(@"{ ""id"": ""crows"", ""kind"": ""line"", ""map"": ""yard"" }", "only a fight")]
        [InlineData(@"{ ""id"": ""wolves"", ""kind"": ""fight"",
                        ""monsters"": [ { ""monster"": ""wolf"", ""count"": ""1d4-1"" } ] }",
                    "can come to none")]
        [InlineData(@"{ ""id"": ""wolves"", ""kind"": ""fight"",
                        ""monsters"": [ { ""monster"": ""wolf"", ""count"": ""lots"" } ] }",
                    "not dice")]
        [InlineData(@"{ ""id"": ""crows"", ""kind"": ""line"" },
                      { ""id"": ""crows"", ""kind"": ""line"" }", "twice")]
        public void ABadEntryIsRefused(string entry, string said)
        {
            Assert.Contains(Problems(One(entry: entry)), p => p.Contains(said));
        }

        [Fact]
        public void AnEmptyTableIsRefused()
        {
            Assert.Contains(Problems(@"{ ""tables"": [ { ""id"": ""north_road"", ""entries"": [] } ] }"),
                            p => p.Contains("empty table"));
        }

        [Fact]
        public void HiddenOrShownAndNothingElse()
        {
            Assert.Contains(Problems(One(extra: @"""rolled"": ""secret"",")),
                            p => p.Contains("'hidden' or 'shown'"));
        }

        [Fact]
        public void AFileWithNoTablesOrNoJsonSaysSo()
        {
            Assert.Contains(Problems(@"{ ""table"": [] }"), p => p.Contains("'tables'"));
            Assert.Contains(Problems("{ not json"), p => p.Contains("not valid json"));
        }

        [Fact]
        public void EveryKeyATablePromisesIsWellFormed()
        {
            EncounterReader.TryRead(Road, out IReadOnlyList<EncounterTable> tables, out _);

            Assert.Equal(2, tables.Single().Keys().Count());
            Assert.All(tables.Single().Keys(),
                       key => Assert.True(KeyConventions.IsWellFormed(key), key));
        }


        // --- in a campaign folder ------------------------------------------------------------------

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

        static readonly string Manifest = $@"{{
            ""id"": ""ash_yard"",
            ""kind"": ""campaign"",
            ""format"": {Content.Schema.ContentFormat.Current},
            ""engine"": ""{Core.EngineVersion.Current}"",
            ""chapters"": [ {{ ""id"": ""the_yard"", ""maps"": [ ""yard"" ] }} ]
        }}";

        const string Yard = @"{
  ""format"": 1,
  ""columns"": 3,
  ""rows"": 2,
  ""map"": ""\n+-+-+-+\n|@ . 1|\n+ + + +\n|. . .|\n+-+-+-+"",
  ""props"": []
}
";

        static Package Pack(Folder folder, string tables)
        {
            folder.Write(ManifestReader.PackFileName, Manifest)
                  .Write("maps/yard.map", Yard)
                  .Write(Package.EncountersFolder + "/road.json", tables);

            return Package.Read(folder.Path);
        }

        [Fact]
        public void ACampaignCarriesItsTables()
        {
            using var folder = new Folder("ash_yard");

            Package package = Pack(folder, Road);

            Assert.True(package.Sound, string.Join("; ", package.Problems.Select(p => p.ToString())));
            Assert.Equal("north_road", package.Encounter("north_road").Id);

            // the narrator's lines are the campaign's to write, so its locale owes them
            Assert.Contains("encounter.north_road.line.crows", package.Keys());
            Assert.DoesNotContain("encounter.north_road.line.quiet", package.Keys());
        }

        [Fact]
        public void ATableCallingForAMonsterNobodyShippedIsRefused()
        {
            using var folder = new Folder("ash_yard");

            Package package = Pack(folder, Road.Replace("goblin_boss", "goblin_king"));

            Assert.False(package.Sound);
            Assert.Contains(package.Faults, p => p.What.Contains("goblin_king") &&
                                                 p.Where == "tables.north_road.entries.goblins");
        }

        [Fact]
        public void AFightOnAMapNobodyShippedIsRefused()
        {
            using var folder = new Folder("ash_yard");

            Package package = Pack(folder, Road.Replace(@"""map"": ""yard""", @"""map"": ""cave"""));

            Assert.False(package.Sound);
            Assert.Contains(package.Faults, p => p.What.Contains("maps/cave.map"));
        }

        [Fact]
        public void TwoFilesCannotBothDefineATable()
        {
            using var folder = new Folder("ash_yard");

            folder.Write(Package.EncountersFolder + "/again.json", Road);

            Package package = Pack(folder, Road);

            Assert.False(package.Sound);
            Assert.Contains(package.Faults, p => p.What.Contains("defined twice"));
        }

        [Fact]
        public void TheTableReadOffDiskRollsOnTheScreen()
        {
            using var folder = new Folder("ash_yard");

            EncounterTable table = Pack(folder, Road).Encounter("north_road");

            // 18 fires it, ticket 7 is the last of seven - the goblins - and the 1d4 comes to 2
            TableRoll roll = new GmScreen(new Core.Dice.ScriptedRng(18, 7, 2)).Consult(table);

            Assert.True(roll.Fights);
            Assert.Equal(2, roll.Group.Single(g => g.Monster == "goblin").Count);
            Assert.Equal("encounter.north_road.line.goblins", roll.LineKey);
        }
    }
}
