using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Content.Campaigns;
using Content.Schema;
using Core.Localization;
using Core.Tables;

namespace Content.Workshop.Tables
{
    // A CAMPAIGN'S TABLES, READ INTO THE WORKSHOP AND WRITTEN BACK (cc_task_n, "every editor here is a form over a reader
    // that already exists"): encounters/, loot/, merchants/ and consequences/, each file through its drafts and back
    // through JsonSplice, so a file nobody touched is never written and a touched one changes only where it was
    // touched. The words the tables speak are the locale's: written with them, the author never seeing a key
    public static class TableFiles
    {
        // one kind of table file: its folder, the list its file holds, and how one of its things is read and written
        sealed record Shelf<T>(string Folder, string List, Func<JsonObject, string, List<string>, T> Read,
                               Func<T, JsonObject> Json, Func<T, string> File);

        static readonly Shelf<EncounterTableDraft> EncounterShelf =
            new(Package.EncountersFolder, "tables", EncounterTableDraft.Read, t => t.ToJson(), t => t.File);

        static readonly Shelf<LootTableDraft> LootShelf =
            new(Package.LootFolder, "tables", LootTableDraft.Read, t => t.ToJson(), t => t.File);

        static readonly Shelf<ShopDraft> ShopShelf =
            new(Package.MerchantsFolder, "merchants", ShopDraft.Read, s => s.ToJson(), s => s.File);

        static readonly Shelf<ConsequenceDraft> LuckShelf =
            new(Package.ConsequencesFolder, "consequences", ConsequenceDraft.Read, c => c.ToJson(), c => c.File);

        // --- open -------------------------------------------------------------------------------------------------

        // the tables a campaign folder holds, their words from its locale. `told` hears every key kept unread
        public static TablesDraft Open(string folder, LocaleSheet sheet, List<string> told = null)
        {
            var tables = new TablesDraft
            {
                Encounters = Read(folder, EncounterShelf, told),
                Loot = Read(folder, LootShelf, told),
                Shops = Read(folder, ShopShelf, told),
                Luck = Read(folder, LuckShelf, told),
            };

            foreach (EncounterTableDraft table in tables.Encounters)
                foreach (EncounterDraft entry in table.Entries)
                    entry.Line = sheet.Get(LineKey(table.Id, entry.Id));

            foreach (LootTableDraft table in tables.Loot)
                foreach (LootDraft entry in table.Entries)
                    entry.Line = sheet.Get(LootLineKey(table.Id, entry.Id));

            foreach (ShopDraft shop in tables.Shops) shop.Name = sheet.Get(shop.NameKey);

            foreach (ConsequenceDraft moment in tables.Luck)
            {
                moment.Name = sheet.Get(MomentKey(moment.Id, "name"));
                moment.Line = sheet.Get(MomentKey(moment.Id, "line"));
            }

            return tables;
        }

        static List<T> Read<T>(string folder, Shelf<T> shelf, List<string> told)
        {
            var found = new List<T>();

            foreach ((string file, string _, string text) in PackFolder.Read(Path.Combine(folder, shelf.Folder), ".json", shelf.Folder, new List<ContentProblem>()))
                found.AddRange(ReadFile(text, file, shelf, told));

            return found;
        }

        static IEnumerable<T> ReadFile<T>(string text, string file, Shelf<T> shelf, List<string> told)
        {
            JsonObject root = DraftJson.Parse(text, out _);

            return DraftJson.Objects(root, shelf.List).Select(o => shelf.Read(o, file, told)).ToList();
        }

        // --- the locale's keys ----------------------------------------------------------------------------------------

        // encounter.<table>.line.<entry>: what the GM says when an entry comes up (GmTable.LineKey)
        public static string LineKey(string table, string entry) =>
            KeyConventions.Key(KeyConventions.EncounterNs, table, "line", entry);

        // encounter.<table>.loot.<entry> (LootTable's aspect)
        public static string LootLineKey(string table, string entry) =>
            KeyConventions.Key(KeyConventions.EncounterNs, table, "loot", entry);

        public static string MomentKey(string id, string aspect) => KeyConventions.Key(KeyConventions.ConsequenceNs, id, aspect);

        // --- save ---------------------------------------------------------------------------------------------------

        // EVERY TABLE FILE WRITTEN where it changed (a file whose last table went is taken away), and the words into the
        // campaign's sheet. Throws the disk's own IOException, for CampaignFiles.Save to say
        public static void Save(TablesDraft tables, string folder, LocaleSheet sheet)
        {
            TablesDraft was = Open(folder, LocaleSheet.Empty());

            Write(folder, EncounterShelf, tables.Encounters);
            Write(folder, LootShelf, tables.Loot);
            Write(folder, ShopShelf, tables.Shops);
            Write(folder, LuckShelf, tables.Luck);

            Words(tables, was, sheet);
        }

        static void Write<T>(string folder, Shelf<T> shelf, List<T> now)
        {
            var files = now.Select(shelf.File).ToHashSet(StringComparer.Ordinal);

            string shelfFolder = Path.Combine(folder, shelf.Folder);

            if (Directory.Exists(shelfFolder))
                foreach (string path in Directory.EnumerateFiles(shelfFolder, "*.json"))
                    files.Add(shelf.Folder + "/" + Path.GetFileName(path));

            foreach (string file in files.OrderBy(f => f, StringComparer.Ordinal))
            {
                string path = Path.Combine(folder, file);
                string original = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "";
                List<T> mine = now.Where(t => shelf.File(t) == file).ToList();

                string written = Text(original, file, shelf, mine, out bool emptied);

                if (emptied)
                {
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    continue;
                }

                if (written == original) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                System.IO.File.WriteAllText(path, written);
            }
        }

        // ONE FILE'S TEXT, with `now` in it: the file's own text where nothing changed. `emptied` when the file had
        // things and none are left (a hand-written file that doesn't read as JSON is never touched)
        static string Text<T>(string original, string file, Shelf<T> shelf, List<T> now, out bool emptied)
        {
            JsonObject root = original.Length == 0 ? new JsonObject() : DraftJson.Parse(original, out _);
            emptied = false;

            if (root == null) return original;

            List<T> read = original.Length == 0 ? new List<T>() : ReadFile(original, file, shelf, null).ToList();

            if (now.Count == 0)
            {
                emptied = read.Count > 0;
                return original;
            }

            JsonObject before = Root(root, shelf.List, read.Select(shelf.Json));
            JsonObject after = Root(root, shelf.List, now.Select(shelf.Json));

            if (JsonNode.DeepEquals(before, after)) return original;

            return JsonSplice.Write(original, before, after);
        }

        // the file's root with its list made of these: its other keys (a "_" comment) kept as they are
        static JsonObject Root(JsonObject file, string list, IEnumerable<JsonObject> items)
        {
            var root = new JsonObject();

            foreach (KeyValuePair<string, JsonNode> field in file)
                root[field.Key] = field.Key == list ? null : field.Value?.DeepClone();

            root[list] = new JsonArray(items.Select(i => (JsonNode)i).ToArray());
            return root;
        }

        // --- the words ------------------------------------------------------------------------------------------------

        // EVERY LINE A TABLE SPEAKS, set in the sheet; the rows of an entry or a table that went taken away with it. Only
        // a table's own rows are touched: `was` is what the files held before this save
        public static void Words(TablesDraft now, TablesDraft was, LocaleSheet sheet)
        {
            var keep = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (EncounterTableDraft table in now.Encounters)
                foreach (EncounterDraft entry in table.Entries.Where(e => e.Kind != EntryKind.Nothing))
                    keep[LineKey(table.Id, entry.Id)] = entry.Line;

            foreach (LootTableDraft table in now.Loot)
                foreach (LootDraft entry in table.Entries.Where(e => e.Speaks))
                    keep[LootLineKey(table.Id, entry.Id)] = entry.Line;

            foreach (ShopDraft shop in now.Shops) keep[shop.NameKey] = shop.Name;

            foreach (ConsequenceDraft moment in now.Luck)
            {
                keep[MomentKey(moment.Id, "name")] = moment.Name;
                keep[MomentKey(moment.Id, "line")] = moment.Line;
            }

            // a row goes with its entry, or when its entry stopped speaking; a row the files never asked for (a hand-written
            // spare) stays
            IEnumerable<string> gone = Keys(was).Except(Keys(now)).Concat(Spoken(was).Where(k => !keep.ContainsKey(k)));

            foreach (string key in gone.Distinct().ToList()) sheet.Remove(key);

            foreach (KeyValuePair<string, string> line in keep)
                if (line.Value.Length > 0 || sheet.Has(line.Key)) sheet.Set(line.Key, line.Value);
        }

        // every key a set of tables speaks
        static IEnumerable<string> Spoken(TablesDraft tables) =>
            tables.Encounters.SelectMany(t => t.Entries.Where(e => e.Kind != EntryKind.Nothing).Select(e => LineKey(t.Id, e.Id)))
                  .Concat(tables.Loot.SelectMany(t => t.Entries.Where(e => e.Speaks).Select(e => LootLineKey(t.Id, e.Id))))
                  .Concat(tables.Shops.Select(s => s.NameKey))
                  .Concat(tables.Luck.SelectMany(c => new[] { MomentKey(c.Id, "name"), MomentKey(c.Id, "line") }));

        // every key a set of tables could own, spoken or not
        static IEnumerable<string> Keys(TablesDraft tables) =>
            tables.Encounters.SelectMany(t => t.Entries.Select(e => LineKey(t.Id, e.Id)))
                  .Concat(tables.Loot.SelectMany(t => t.Entries.Select(e => LootLineKey(t.Id, e.Id))))
                  .Concat(tables.Shops.Select(s => s.NameKey))
                  .Concat(tables.Luck.SelectMany(c => new[] { MomentKey(c.Id, "name"), MomentKey(c.Id, "line") }));
    }
}
