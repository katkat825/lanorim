using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Inventory;
using Content.Items;
using Content.Loot;
using Content.Schema;
using Core.Dice;
using Core.Localization;
using Core.Tables;

namespace Content.Tests
{
    // loot tables as a campaign ships them, and as they meet one hero and one pack. the reader
    // holds a table to its shape, the package to the items that exist, and Spoils to who is
    // looking and how much room they have.
    public class LootTests
    {
        const string Pockets = @"{
  ""tables"": [
    {
      ""id"": ""goblin_pockets"",
      ""entries"": [
        { ""id"": ""lint"", ""kind"": ""nothing"", ""weight"": 2 },
        { ""id"": ""coins"", ""kind"": ""find"", ""weight"": 3,
          ""gold"": { ""roll"": ""3d6"", ""times"": 10 }, ""line"": true },
        { ""id"": ""torches"", ""kind"": ""find"",
          ""items"": [ { ""item"": ""torch"", ""count"": ""1d4"" } ], ""gold"": ""2"" },
        { ""id"": ""gem_pouch"", ""kind"": ""table"", ""table"": ""gems"" }
      ]
    },
    {
      ""id"": ""gems"",
      ""rolled"": ""shown"",
      ""entries"": [ { ""id"": ""gemstone"", ""kind"": ""find"",
                       ""items"": [ { ""item"": ""gemstone"" } ] } ]
    }
  ]
}";

        static IReadOnlyList<string> Problems(string text)
        {
            LootReader.TryRead(text, out _, out IReadOnlyList<string> problems);
            return problems;
        }

        // one table, one entry, with a piece swapped out
        static string One(string entry = @"{ ""id"": ""lint"", ""kind"": ""nothing"" }",
                          string extra = "") =>
            $@"{{ ""tables"": [ {{ ""id"": ""goblin_pockets"", {extra}
                                   ""entries"": [ {entry} ] }} ] }}";

        [Fact]
        public void AWellFormedTableReads()
        {
            Assert.True(LootReader.TryRead(Pockets, out IReadOnlyList<LootTable> tables,
                                           out IReadOnlyList<string> problems),
                        string.Join("; ", problems));

            LootTable pockets = tables.Single(t => t.Id == "goblin_pockets");

            Assert.Equal(Visibility.Hidden, pockets.Visibility);
            Assert.Equal(Visibility.Shown, tables.Single(t => t.Id == "gems").Visibility);
            Assert.Equal(7, pockets.TotalWeight);

            LootEntry coins = pockets.Entries.Single(e => e.Id == "coins");

            Assert.Equal("3d6", coins.Gold.Dice.ToString());
            Assert.Equal(10, coins.Gold.Times);
            Assert.Equal(30, coins.Gold.Minimum);
            Assert.Equal(180, coins.Gold.Maximum);

            LootEntry torches = pockets.Entries.Single(e => e.Id == "torches");

            Assert.Equal("1d4", torches.Items.Single().Count.ToString());
            Assert.Equal(2, torches.Gold.Minimum);

            Assert.Equal("gems", pockets.Entries.Single(e => e.Kind == LootKind.Table).Table);
            Assert.Equal("1", tables.Single(t => t.Id == "gems").Entries[0].Items[0].Count.ToString());
        }

        [Theory]
        [InlineData(@"{ ""id"": ""lint"", ""kind"": ""nothing"", ""weight"": 0 }", "weight")]
        [InlineData(@"{ ""id"": ""lint"", ""kind"": ""nothing"", ""weight"": ""lots"" }", "weight")]
        [InlineData(@"{ ""id"": ""lint"", ""kind"": ""chest"" }", "not a kind of entry")]
        [InlineData(@"{ ""id"": ""Lint"", ""kind"": ""nothing"" }", "not an entry id")]
        [InlineData(@"{ ""id"": ""box"", ""kind"": ""find"" }", "needs 'items' or 'gold'")]
        [InlineData(@"{ ""id"": ""box"", ""kind"": ""table"" }", "needs 'table'")]
        [InlineData(@"{ ""id"": ""lint"", ""kind"": ""nothing"", ""gold"": ""5"" }",
                    "'nothing' has nothing")]
        [InlineData(@"{ ""id"": ""box"", ""kind"": ""table"", ""table"": ""gems"", ""gold"": ""5"" }",
                    "gives nothing of its own")]
        [InlineData(@"{ ""id"": ""box"", ""kind"": ""find"", ""gold"": ""5"", ""table"": ""gems"" }",
                    "does not roll a table")]
        [InlineData(@"{ ""id"": ""box"", ""kind"": ""find"",
                        ""items"": [ { ""item"": ""torch"", ""count"": ""1d4-1"" } ] }",
                    "can come to none")]
        [InlineData(@"{ ""id"": ""box"", ""kind"": ""find"",
                        ""items"": [ { ""item"": ""Torch"" } ] }", "not an item id")]
        [InlineData(@"{ ""id"": ""box"", ""kind"": ""find"", ""gold"": ""heaps"" }", "not dice")]
        [InlineData(@"{ ""id"": ""box"", ""kind"": ""find"", ""gold"": ""1d4-6"" }", "less than none")]
        [InlineData(@"{ ""id"": ""box"", ""kind"": ""find"", ""gold"": { ""times"": 10 } }",
                    "needs 'roll'")]
        [InlineData(@"{ ""id"": ""box"", ""kind"": ""find"",
                        ""gold"": { ""roll"": ""3d6"", ""times"": 0 } }", "'times'")]
        [InlineData(@"{ ""id"": ""lint"", ""kind"": ""nothing"" },
                      { ""id"": ""lint"", ""kind"": ""nothing"" }", "twice")]
        public void ABadEntryIsRefused(string entry, string said)
        {
            Assert.Contains(Problems(One(entry: entry)), p => p.Contains(said));
        }

        [Fact]
        public void AGoldDiceTypoIsSaidOnce()
        {
            Assert.Single(Problems(One(@"{ ""id"": ""box"", ""kind"": ""find"", ""gold"": ""heaps"" }")));
        }

        [Fact]
        public void AnEmptyTableIsRefused()
        {
            Assert.Contains(Problems(@"{ ""tables"": [ { ""id"": ""chest"", ""entries"": [] } ] }"),
                            p => p.Contains("empty table"));
        }

        [Fact]
        public void HiddenOrShownAndNothingElse()
        {
            Assert.Contains(Problems(One(extra: @"""rolled"": ""secret"",")),
                            p => p.Contains("'hidden' or 'shown'"));
        }

        [Fact]
        public void ATableThatRollsItselfIsRefused()
        {
            Assert.Contains(Problems(One(@"{ ""id"": ""again"", ""kind"": ""table"",
                                            ""table"": ""goblin_pockets"" }")),
                            p => p.Contains("rolls itself"));
        }

        [Fact]
        public void ALoopThroughTwoTablesInOneFileIsRefused()
        {
            string loop = Pockets.Replace(@"""items"": [ { ""item"": ""gemstone"" } ]",
                                          @"""items"": [ { ""item"": ""gemstone"" } ] },
                       { ""id"": ""back"", ""kind"": ""table"", ""table"": ""goblin_pockets""");

            Assert.Contains(Problems(loop),
                            p => p.Contains("gems rolls goblin_pockets rolls gems"));
        }

        [Fact]
        public void EveryKeyATablePromisesIsWellFormed()
        {
            LootReader.TryRead(Pockets, out IReadOnlyList<LootTable> tables, out _);

            string[] keys = tables.SelectMany(t => t.Keys()).ToArray();

            Assert.Equal(new[] { "encounter.goblin_pockets.loot.coins" }, keys);
            Assert.All(keys, key => Assert.True(KeyConventions.IsWellFormed(key), key));
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
            ""format"": {ContentFormat.Current},
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

        static Package Pack(Folder folder, string loot)
        {
            folder.Write(ManifestReader.PackFileName, Manifest)
                  .Write("maps/yard.map", Yard)
                  .Write(Package.LootFolder + "/pockets.json", loot);

            return Package.Read(folder.Path);
        }

        static string Said(Package package) =>
            string.Join("; ", package.Problems.Select(p => p.ToString()));

        [Fact]
        public void ACampaignCarriesItsLoot()
        {
            using var folder = new Folder("ash_yard");

            Package package = Pack(folder, Pockets);

            Assert.True(package.Sound, Said(package));
            Assert.Equal(2, package.Loot.Count);
            Assert.Equal("goblin_pockets", package.LootTable("goblin_pockets").Id);

            // the narrator's lines are the campaign's to write, so its locale owes them
            Assert.Contains("encounter.goblin_pockets.loot.coins", package.Keys());
            Assert.DoesNotContain("encounter.goblin_pockets.loot.lint", package.Keys());
        }

        [Fact]
        public void AFindOfAnItemNobodyShippedIsRefused()
        {
            using var folder = new Folder("ash_yard");

            Package package = Pack(folder, Pockets.Replace(@"""torch""", @"""torchh"""));

            Assert.False(package.Sound);
            Assert.Contains(package.Faults, p => p.What.Contains("'torchh'") &&
                                                 p.Where == "tables.goblin_pockets.entries.torches");
        }

        [Fact]
        public void AnItemTheCampaignShipsMayBeFound()
        {
            using var folder = new Folder("ash_yard");

            folder.Write(Package.ItemsFolder + "/relics.json",
                         @"{ ""items"": [ { ""id"": ""yard_key"", ""kind"": ""quest"" } ] }");

            Package package = Pack(folder, Pockets.Replace(@"""torch""", @"""yard_key"""));

            Assert.True(package.Sound, Said(package));
        }

        [Fact]
        public void RollingATableNobodyShippedIsRefused()
        {
            using var folder = new Folder("ash_yard");

            Package package = Pack(folder, Pockets.Replace(@"""table"": ""gems""",
                                                           @"""table"": ""jewels"""));

            Assert.False(package.Sound);
            Assert.Contains(package.Faults, p => p.What.Contains("'jewels'"));
        }

        [Fact]
        public void ALoopAcrossTwoFilesIsRefused()
        {
            using var folder = new Folder("ash_yard");

            folder.Write(Package.LootFolder + "/gems.json", @"{ ""tables"": [ { ""id"": ""gems"",
                ""entries"": [ { ""id"": ""back"", ""kind"": ""table"", ""table"": ""goblin_pockets"" } ]
            } ] }");

            string without = @"{ ""tables"": [ { ""id"": ""goblin_pockets"", ""entries"": [
                { ""id"": ""gem_pouch"", ""kind"": ""table"", ""table"": ""gems"" } ] } ] }";

            Package package = Pack(folder, without);

            Assert.False(package.Sound);
            Assert.Contains(package.Faults, p => p.What.Contains("loop") &&
                                                 p.What.Contains("loot/gems.json") &&
                                                 p.What.Contains("loot/pockets.json"));
        }

        const string Road = @"{ ""tables"": [ { ""id"": ""north_road"", ""entries"": [
            { ""id"": ""goblins"", ""kind"": ""fight"", ""loot"": ""goblin_pockets"",
              ""monsters"": [ { ""monster"": ""goblin"" } ] } ] } ] }";

        [Fact]
        public void AFightMayNameTheLootItLeaves()
        {
            using var folder = new Folder("ash_yard");

            folder.Write(Package.EncountersFolder + "/road.json", Road);

            Package package = Pack(folder, Pockets);

            Assert.True(package.Sound, Said(package));

            TableRoll fight = new GmScreen(new ScriptedRng(1)).Consult(package.Encounter("north_road"));

            Assert.Equal("goblin_pockets", fight.Loot);
            Assert.NotNull(package.LootTable(fight.Loot));
        }

        [Fact]
        public void AFightLeavingLootNobodyShippedIsRefused()
        {
            using var folder = new Folder("ash_yard");

            folder.Write(Package.EncountersFolder + "/road.json",
                         Road.Replace("goblin_pockets", "goblin_purse"));

            Package package = Pack(folder, Pockets);

            Assert.False(package.Sound);
            Assert.Contains(package.Faults, p => p.What.Contains("'goblin_purse'") &&
                                                 p.Where == "tables.north_road.entries.goblins");
        }


        // --- for one hero ------------------------------------------------------------------------

        static readonly ItemShelf Shelf = Library.Srd().Items;

        // four entries of one weight each: a cleric's symbol, a rogue's tools, a potion from level 5,
        // and torches anyone can carry
        static readonly LootTable Shrine = new LootTable("shrine_box", new[]
        {
            new LootEntry("symbol", LootKind.Find, items: new[] { new Lot("holy_symbol", DiceRoll.Flat(1)) }),
            new LootEntry("picks", LootKind.Find, items: new[] { new Lot("thieves_tools", DiceRoll.Flat(1)) }),
            new LootEntry("potion", LootKind.Find,
                          items: new[] { new Lot("greater_potion_of_healing", DiceRoll.Flat(1)) }),
            new LootEntry("torches", LootKind.Find, items: new[] { new Lot("torch", DiceRoll.Flat(3)) }),
        });

        [Fact]
        public void AFighterIsNeverDrawnWhatTheyCannotUse()
        {
            LootRoll roll = new GmScreen(new ScriptedRng(1)).Open(Shrine, null, Shelf, "fighter", 1);

            Assert.Equal("d1", roll.PickRoll.Dice);
            Assert.Equal("torches", roll.Entry.Id);
            Assert.Equal(new[] { "symbol", "picks", "potion" }, roll.WeightedOut);
        }

        [Fact]
        public void AClericAtFiveHasTheSymbolAndThePotionInTheDraw()
        {
            LootRoll roll = new GmScreen(new ScriptedRng(2)).Open(Shrine, null, Shelf, "cleric", 5);

            Assert.Equal("d3", roll.PickRoll.Dice);
            Assert.Equal("potion", roll.Entry.Id);
            Assert.Equal(new[] { "picks" }, roll.WeightedOut);
        }

        // the merchant's promise, held for loot too: whatever the dice do, nothing unusable surfaces
        [Theory]
        [InlineData("fighter", 1)]
        [InlineData("rogue", 3)]
        [InlineData("cleric", 1)]
        [InlineData("mage", 5)]
        public void NoSeedEverSurfacesAnUnusableItem(string className, int level)
        {
            for (int seed = 0; seed < 200; seed++)
            {
                LootRoll roll = new GmScreen(new SeededRng(seed))
                    .Open(Shrine, null, Shelf, className, level);

                Assert.All(roll.Found, f => Assert.True(Shelf.Find(f.Item).UsableBy(className, level),
                                                        $"{f.Item} for a level {level} {className}"));
            }
        }

        [Fact]
        public void AHaulGoesIntoThePackAndTheGoldIntoThePurse()
        {
            var pack = new Pack();
            var table = new LootTable("purse", new[]
            {
                new LootEntry("purse", LootKind.Find, items: new[] { new Lot("torch", DiceRoll.Flat(3)) },
                              gold: new Purse(DiceRoll.Parse("2d6"), 10)),
            });

            LootRoll roll = new GmScreen(new ScriptedRng(1, 3, 4)).Open(table, null, Shelf, "fighter", 1);
            Haul haul = Spoils.Hand(pack, roll, Shelf);

            Assert.Equal(70, haul.Gold);
            Assert.Equal(70, pack.Gold);
            Assert.Equal(3, pack.CountOf("torch"));
            Assert.False(haul.MustMakeRoom);
        }

        [Fact]
        public void LootIntoAFullPackWaitsOnTheOverflowAndIsNotDropped()
        {
            var pack = new Pack(1);
            pack.Take(Shelf.Find("shield"));

            var table = new LootTable("rack", new[]
            {
                new LootEntry("rack", LootKind.Find, gold: new Purse(DiceRoll.Flat(5)), items: new[]
                {
                    new Lot("longsword", DiceRoll.Flat(1)),
                    new Lot("dagger", DiceRoll.Flat(1)),
                }),
            });

            LootRoll roll = new GmScreen(new ScriptedRng(1)).Open(table, null, Shelf, "fighter", 1);
            Haul haul = Spoils.Hand(pack, roll, Shelf);

            // the gold costs no slot, so it is in; the two blades wait, in the order they were found
            Assert.Equal(5, pack.Gold);
            Assert.True(haul.MustMakeRoom);
            Assert.Equal(new[] { "longsword", "dagger" }, haul.Waiting.Select(o => o.Arriving.Id));
            Assert.True(pack.Has("shield"));
            Assert.False(pack.Has("longsword"));

            // the existing flow: drop the shield for the sword, and refuse the dagger
            Overflow sword = haul.Waiting[0];

            Assert.True(sword.Discard("shield"));
            Assert.True(sword.Finish());

            Overflow dagger = haul.Waiting[1];

            Assert.Equal(1, dagger.SlotsShort);
            dagger.Refuse();
            Assert.True(dagger.Finish());

            Assert.True(pack.Has("longsword"));
            Assert.False(pack.Has("dagger"));
            Assert.False(pack.Has("shield"));
        }

        [Fact]
        public void MoreOnAStackAlreadyCarriedNeedsNoRoom()
        {
            var pack = new Pack(1);
            pack.Take(Shelf.Find("torch"));

            var table = new LootTable("torches", new[]
            {
                new LootEntry("torches", LootKind.Find, items: new[] { new Lot("torch", DiceRoll.Flat(4)) }),
            });

            Haul haul = Spoils.Hand(pack, new GmScreen(new ScriptedRng(1)).Open(table), Shelf);

            Assert.False(haul.MustMakeRoom);
            Assert.Equal(5, pack.CountOf("torch"));
        }

        [Fact]
        public void TheLootRollIsMadeBehindTheScreen()
        {
            var log = new ScreenLog();
            var screen = new GmScreen(new ScriptedRng(1), log);

            LootReader.TryRead(Pockets, out IReadOnlyList<LootTable> tables, out _);

            // ticket 1 of 7: the lint - one pick, hidden, and said to the log
            LootRoll roll = screen.Open(tables[0], new LootTables(tables), Shelf, "fighter", 1);

            Assert.True(roll.PickRoll.IsHidden);
            Assert.Equal("behind the screen: pick d7 = 1 (goblin_pockets)", log.Lines[0]);
            Assert.Equal("== goblin_pockets: lint, and nothing in it", log.Lines[1]);
        }
    }
}
