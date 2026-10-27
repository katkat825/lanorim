using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Encounters;
using Content.Loot;
using Content.Schema;
using Content.Workshop;
using Content.Workshop.Tables;
using Core.Tables;

namespace Content.Tests
{
    // cc_task_n, "writer round trips for encounters, loot, merchants and consequences over the corpus": every table file
    // in the repo opens into the editor's drafts and saves back byte for byte when nothing changed; a change rewrites only
    // the entry it touched, in that entry's own shape; a key the editor doesn't know rides along; the words go into the
    // locale with the keys the game reads
    public class WorkshopTablesTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_tables_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        // THE CORPUS: sample_millbrook whole, the crucible fixture's tables, and the game's own pool and starting shop (one
        // line per entry, by hand), each in a campaign folder of its own
        public static IEnumerable<object[]> Corpus()
        {
            yield return new object[] { "sample" };
            yield return new object[] { "crucible" };
            yield return new object[] { "srd" };
        }

        string Copy(string which)
        {
            // a campaign's folder is named for its id
            string folder = Path.Combine(_root, which, which switch { "sample" => "sample_millbrook", "crucible" => "crucible_fixture", _ => "srd_tables" });

            switch (which)
            {
                case "sample": CopyTree(SampleCampaignTests.Folder, folder); break;
                case "crucible": CopyTree(CrucibleTests.Folder, folder); break;
                default:
                    Directory.CreateDirectory(Path.Combine(folder, "consequences"));
                    Directory.CreateDirectory(Path.Combine(folder, "merchants"));
                    File.WriteAllText(Path.Combine(folder, "consequences", "pool.json"), Srd.Read("consequences/pool.json"));
                    File.WriteAllText(Path.Combine(folder, "merchants", "starting_shop.json"), Srd.Read("merchants/starting_shop.json"));
                    break;
            }

            return folder;
        }

        static void CopyTree(string from, string to)
        {
            foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                string there = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(there));
                File.Copy(file, there);
            }
        }

        static Dictionary<string, string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(folder, "*.csv", SearchOption.AllDirectories))
                     .ToDictionary(f => Path.GetRelativePath(folder, f), File.ReadAllText);

        static LocaleSheet Sheet(string folder)
        {
            string csv = Directory.Exists(Path.Combine(folder, "locale"))
                ? Directory.EnumerateFiles(Path.Combine(folder, "locale"), "*.csv").FirstOrDefault()
                : null;

            return csv == null ? LocaleSheet.Empty() : LocaleSheet.Of(File.ReadAllText(csv));
        }

        [Theory]
        [MemberData(nameof(Corpus))]
        public void EveryTableFileSavesBackByteForByteWhenNothingChanged(string which)
        {
            string folder = Copy(which);
            Dictionary<string, string> before = Files(folder);
            LocaleSheet sheet = Sheet(folder);

            TablesDraft tables = TableFiles.Open(folder, sheet);
            Assert.True(tables.Encounters.Count + tables.Loot.Count + tables.Shops.Count + tables.Luck.Count > 0, "the corpus has tables");

            TableFiles.Save(tables, folder, sheet);

            Dictionary<string, string> after = Files(folder);
            Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
            foreach (string file in before.Keys) Assert.True(before[file] == after[file], $"{file} came back changed");
        }

        [Theory]
        [MemberData(nameof(Corpus))]
        public void ReadWriteReadIsTheSameThroughTheGamesOwnReaders(string which)
        {
            string folder = Copy(which);
            string again = Path.Combine(_root, which + "_again", "again");
            Directory.CreateDirectory(again);

            TablesDraft tables = TableFiles.Open(folder, Sheet(folder));
            TableFiles.Save(tables, again, LocaleSheet.Empty());

            foreach (string file in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith("pack.json")))
            {
                string rel = Path.GetRelativePath(folder, file);
                if (rel.StartsWith("maps") || rel.StartsWith("barks")) continue;

                Assert.True(File.Exists(Path.Combine(again, rel)), $"{rel} was written");
                Assert.Equal(Readers(rel, File.ReadAllText(file)), Readers(rel, File.ReadAllText(Path.Combine(again, rel))));
            }
        }

        // what the game's own reader makes of a file, as text
        static string Readers(string file, string text)
        {
            string folder = file.Split('/', '\\')[0];

            return folder switch
            {
                Package.EncountersFolder => EncounterReader.TryRead(text, out var e, out _) ? string.Join("|", e.Select(t => t + ": " + string.Join(";", t.Entries))) : "unread",
                Package.LootFolder => LootReader.TryRead(text, out var l, out _) ? string.Join("|", l.Select(t => t + ": " + string.Join(";", t.Entries))) : "unread",
                Package.MerchantsFolder => MerchantReader.TryRead(text, out var m, out _) ? string.Join("|", m.Select(s => s + string.Join(",", s.Stock) + s.SellPercent + s.Armory)) : "unread",
                _ => ConsequenceReader.CampaignEntries.TryRead(text, out var c, out _) ? string.Join("|", c.Select(x => x + " w" + x.Weight)) : "unread",
            };
        }

        [Fact]
        public void AChangedEntryIsRewrittenAloneInItsOwnShape()
        {
            string folder = Copy("srd");
            string pool = Path.Combine(folder, "consequences", "pool.json");
            string[] was = File.ReadAllLines(pool);

            TablesDraft tables = TableFiles.Open(folder, LocaleSheet.Empty());
            tables.Moment("a_bad_feeling").Weight = 5;
            TableFiles.Save(tables, folder, LocaleSheet.Empty());

            string[] now = File.ReadAllLines(pool);
            Assert.Equal(was.Length, now.Length);

            var changed = Enumerable.Range(0, was.Length).Where(i => was[i] != now[i]).ToList();
            Assert.Single(changed);
            Assert.Equal("""    { "id": "a_bad_feeling", "polarity": "bane", "kind": "flavour", "weight": 5 },""", now[changed[0]]);

            Assert.True(ConsequenceReader.TryRead(File.ReadAllText(pool), out var read, out var problems), string.Join("; ", problems));
            Assert.Equal(5, read.Single(c => c.Id == "a_bad_feeling").Weight);
        }

        [Fact]
        public void AnIndentedFileGainsAnIndentedEntryAndKeepsTheRest()
        {
            string folder = Copy("sample");
            string file = Path.Combine(folder, "encounters", "encounters.json");
            string was = File.ReadAllText(file);

            TablesDraft tables = TableFiles.Open(folder, Sheet(folder));
            EncounterTableDraft road = tables.Table("mill_road");
            road.Entries.Add(new EncounterDraft { Id = "owls", Kind = EntryKind.Line, Weight = 2, Line = "An owl calls." });
            road.Entry("wolves").Monsters[0].Count = "2";
            TableFiles.Save(tables, folder, Sheet(folder));

            string now = File.ReadAllText(file);
            Assert.Contains("""
                    {
                      "id": "owls",
                      "kind": "line",
                      "weight": 2
                    }
            """.Replace("\r\n", "\n"), now.Replace("\r\n", "\n"));
            // the set pieces table, untouched, is the file's own text
            int setPieces = was.IndexOf("\"set_pieces\"", StringComparison.Ordinal);
            int road0 = was.IndexOf("\"mill_road\"", StringComparison.Ordinal);
            Assert.Contains(was.Substring(setPieces, road0 - setPieces - 20), now);

            Assert.True(EncounterReader.TryRead(now, out var read, out var problems), string.Join("; ", problems));
            Assert.Equal("2", read.Single(t => t.Id == "mill_road").Entries.Single(e => e.Id == "wolves").Monsters[0].Count.ToString());
        }

        [Fact]
        public void AKeyTheEditorDoesNotKnowIsKeptAndTold()
        {
            string folder = Copy("srd");
            string pool = Path.Combine(folder, "consequences", "pool.json");
            File.WriteAllText(pool, File.ReadAllText(pool).Replace("""{ "id": "rattled", """, """{ "id": "rattled", "note": "for the bridge", """));

            var told = new List<string>();
            TablesDraft tables = TableFiles.Open(folder, LocaleSheet.Empty(), told);
            Assert.Contains(told, t => t.Contains("rattled") && t.Contains("'note'"));

            tables.Moment("rattled").Weight = 4;
            TableFiles.Save(tables, folder, LocaleSheet.Empty());

            // the entry keeps its own wrap onto a second line, and the key it doesn't know
            Assert.Contains("""
                { "id": "rattled", "note": "for the bridge", "polarity": "bane", "kind": "ability_shift", "ability": "cha",
                  "amount": "-1", "weight": 4 },
            """.Replace("\r\n", "\n"), File.ReadAllText(pool).Replace("\r\n", "\n"));
        }

        [Fact]
        public void ANewFightGoesIntoTheWorkshopsFightsTableWithItsLine()
        {
            string folder = Copy("sample");
            LocaleSheet sheet = Sheet(folder);

            TablesDraft tables = TableFiles.Open(folder, sheet);
            var fights = new EncounterTableDraft { File = TablesDraft.FightsFile, Id = TablesDraft.FightsTable };
            EncounterDraft fight = NewEntries.Fight("bridge_trolls");
            fight.Map = "road_clearing";
            fight.Line = "Something big under the bridge.";
            fights.Entries.Add(fight);
            tables.Encounters.Add(fights);

            TableFiles.Save(tables, folder, sheet);

            Assert.True(EncounterReader.TryRead(File.ReadAllText(Path.Combine(folder, "encounters", "fights.json")), out var read, out var problems),
                        string.Join("; ", problems));
            Assert.Equal("bridge_trolls", read.Single().Entries.Single().Id);
            Assert.Equal("Something big under the bridge.", sheet.Get("encounter.fights.line.bridge_trolls"));

            // and the pack loads with no new problems
            File.WriteAllText(Directory.EnumerateFiles(Path.Combine(folder, "locale")).First(), sheet.Text());
            Package pack = Package.Read(folder);
            Assert.True(pack.Sound, string.Join("; ", pack.Problems));
        }

        [Fact]
        public void ADeletedEntryTakesItsLocaleRowAndAnEmptiedFileGoes()
        {
            string folder = Copy("sample");
            LocaleSheet sheet = Sheet(folder);

            TablesDraft tables = TableFiles.Open(folder, sheet);
            Assert.True(sheet.Has("encounter.mill_road.line.crows"));

            tables.Table("mill_road").Entries.RemoveAll(e => e.Id == "crows");
            tables.Shops.Clear();
            TableFiles.Save(tables, folder, sheet);

            Assert.False(sheet.Has("encounter.mill_road.line.crows"));
            Assert.False(sheet.Has("merchant.millbrook_store.name"));
            Assert.False(File.Exists(Path.Combine(folder, "merchants", "merchants.json")));
            Assert.True(sheet.Has("encounter.mill_road.line.wolves"), "the rest of the table's rows stay");
        }

        [Fact]
        public void TheTablesRideInTheDraftsSnapshot()
        {
            string folder = Copy("sample");
            CampaignEditor editor = CampaignEditor.Open(folder);

            Assert.Contains(editor.Draft.Tables.Fights, f => f.Fight.Id == "goblin_camp");

            editor.Edit("fight", d => d.Tables.Fight("goblin_camp").Fight.Level = 4);
            Assert.Equal(4, editor.Draft.Tables.Fight("goblin_camp").Fight.Level);
            editor.Undo.Undo();
            Assert.Equal(3, editor.Draft.Tables.Fight("goblin_camp").Fight.Level);

            Assert.Null(editor.Save());
            Assert.True(Package.Read(folder).Sound);
        }
    }
}
