using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Classes;
using Content.Encounters;
using Content.Items;
using Content.Loot;
using Content.Maps;
using Content.Monsters;
using Content.Schema;
using Content.Spells;
using Core.Magic;
using Core.Space;
using Core.Tables;

namespace Content.Campaigns
{
    // A CAMPAIGN FOLDER, READ. One manifest and a folder per kind of content; everything in it is
    // scoped to the campaign's id, so two campaigns may both have a "goblin" and neither wins.
    //
    // ADAPTED from the old build's Package. The vessel is the same - name the folders as constants,
    // read each with the reader that owns that kind, collect problems rather than throwing, and
    // refuse the pack rather than half-load it. What went is the World layer: places/, entities/,
    // quests/, roads/ and companions/ were the abandoned direction, and kits/ was the homebrew
    // ability system that core/Magic replaced. What a chapter walks through now is maps.
    //
    // A FOLDER THIS DOES NOT KNOW IS A PROBLEM, NOT A SHRUG. A Workshop author who writes
    // "monster/" instead of "monsters/" has a campaign with no monsters in it and nothing to say
    // why, so the names it does know are listed back at them.
    public sealed class Package
    {
        public const string MonstersFolder = "monsters";

        public const string ItemsFolder = "items";

        public const string SpellsFolder = "spells";

        public const string MapsFolder = "maps";

        public const string EncountersFolder = "encounters";

        public const string LootFolder = "loot";

        public const string LocaleFolder = "locale";

        public const string AssetsFolder = "assets";

        // named though not read yet, so a pack that ships them is told "not loaded yet" rather than
        // "there is no such thing" - the difference between a feature that is coming and a typo.
        // Dialogue is Phase 7; minis, models and audio are the campaign mini-pack, Phase 7 as well.
        public const string DialogueFolder = "dialogue";

        public const string MinisFolder = "minis";

        public const string ModelsFolder = "models";

        public const string AudioFolder = "audio";

        public const string MapExtension = ".map";

        // shops: an id, what it stocks, and optionally what it pays (inventory_decisions.md)
        public const string MerchantsFolder = "merchants";

        public static readonly string[] Folders =
        {
            MonstersFolder, ItemsFolder, SpellsFolder, MapsFolder, EncountersFolder, LootFolder,
            LocaleFolder, AssetsFolder, DialogueFolder, MinisFolder, ModelsFolder, AudioFolder,
            MerchantsFolder,
        };

        // read, but nothing is done with them yet. (dialogue/ is read by DialogueBook when a campaign
        // is played, so it is not in here)
        public static readonly string[] NotLoadedYet =
        {
            MinisFolder, ModelsFolder, AudioFolder,
        };

        Package(string folder, string id, Manifest manifest,
                IReadOnlyList<Monster> monsters, IReadOnlyList<Item> items,
                IReadOnlyList<Spell> spells, IReadOnlyDictionary<string, MapLayout> maps,
                IReadOnlyList<EncounterTable> encounters, IReadOnlyList<LootTable> loot,
                List<ContentProblem> problems)
        {
            Folder = folder ?? "";
            Id = id ?? "";
            Manifest = manifest;
            Monsters = monsters ?? Array.Empty<Monster>();
            Items = items ?? Array.Empty<Item>();
            Spells = spells ?? Array.Empty<Spell>();
            Maps = maps ?? new Dictionary<string, MapLayout>();
            Encounters = encounters ?? Array.Empty<EncounterTable>();
            Loot = new LootTables(loot);
            Problems = problems ?? new List<ContentProblem>();
        }

        public string Folder { get; }

        public string Id { get; }

        public Manifest Manifest { get; }

        public IReadOnlyList<Monster> Monsters { get; }

        public IReadOnlyList<Item> Items { get; }

        public IReadOnlyList<Spell> Spells { get; }

        public IReadOnlyDictionary<string, MapLayout> Maps { get; }

        // what stands on each map's squares, by map id: the builder's props, which a MapLayout (the
        // rules' map) has no use for and the board draws
        public IReadOnlyDictionary<string, IReadOnlyList<Prop>> Props { get; private set; } =
            new Dictionary<string, IReadOnlyList<Prop>>();

        public IReadOnlyList<Prop> PropsOn(string map) =>
            map != null && Props.TryGetValue(map, out IReadOnlyList<Prop> props) ? props : Array.Empty<Prop>();

        public IReadOnlyList<EncounterTable> Encounters { get; }

        public EncounterTable Encounter(string id) =>
            Encounters.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

        // every loot table, together, because one may roll another from a different file
        public LootTables Loot { get; }

        public LootTable LootTable(string id) => Loot.Find(id);

        public IReadOnlyList<ContentProblem> Problems { get; }

        public IEnumerable<ContentProblem> Faults => Problems.Where(p => p.IsAFault);

        public IEnumerable<ContentProblem> Cautions => Problems.Where(p => p.IsACaution);

        // a caution is a sentence about a pack that loaded, so it cannot be what makes it refused
        public bool Sound => Manifest != null && !Problems.Any(p => p.IsAFault);

        // every key this pack promises its locale answers, so the audit walks the pack rather than
        // a list somebody keeps in step by hand
        public IEnumerable<string> Keys() =>
            (Manifest?.Keys() ?? Enumerable.Empty<string>())
                .Concat(Monsters.SelectMany(m => m.Keys()))
                .Concat(Items.SelectMany(i => i.Keys()))
                .Concat(Spells.SelectMany(s => s.Keys()))
                .Concat(Encounters.SelectMany(t => t.Keys()))
                .Concat(Loot.All.SelectMany(t => t.Keys()))
                .Concat(Merchants.Select(m => m.NameKey))
                .Distinct();

        // the campaign's shops, by id
        public IReadOnlyList<MerchantDef> Merchants { get; private set; } = Array.Empty<MerchantDef>();

        public MerchantDef Merchant(string id) =>
            Merchants.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.Ordinal));

        public override string ToString() =>
            Manifest == null
                ? $"{Id}: unreadable, {Problems.Count} problems"
                : $"{Manifest}, {Monsters.Count} monsters, {Items.Count} items, " +
                  $"{Spells.Count} spells, {Maps.Count} maps" +
                  (Encounters.Count > 0 ? $", {Encounters.Count} encounter tables" : "") +
                  (Loot.Count > 0 ? $", {Loot.Count} loot tables" : "") +
                  (Sound ? "" : $", {Faults.Count()} faults");


        // --- reading one off disk ---------------------------------------------------------------

        public static Package Read(string folder)
        {
            var problems = new List<ContentProblem>();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                problems.Add(new ContentProblem(folder ?? "", "",
                    "there is no folder there to read a campaign out of"));

                return new Package(folder, "", null, null, null, null, null, null, null, problems);
            }

            string name = new DirectoryInfo(folder).Name;

            Manifest manifest = ReadManifest(folder, name, problems);

            // without a manifest nothing below can be scoped, so the pack stops here rather than
            // registering a folder full of content under no id at all
            if (manifest == null)
                return new Package(folder, name, null, null, null, null, null, null, null, problems);

            UnknownFolders(folder, problems);

            IReadOnlyList<Monster> monsters = ReadMonsters(folder, manifest.Id, problems);
            IReadOnlyList<Item> items = ReadItems(folder, manifest.Id, problems);
            IReadOnlyList<Spell> spells = ReadSpells(folder, manifest.Id, problems);
            var props = new Dictionary<string, IReadOnlyList<Prop>>(StringComparer.Ordinal);
            IReadOnlyDictionary<string, MapLayout> maps = ReadMaps(folder, manifest.Id, problems, props);

            ChaptersNameRealMaps(manifest, maps, problems);

            IReadOnlyList<LootTable> loot = ReadLoot(folder, manifest.Id, items, problems);

            IReadOnlyList<EncounterTable> encounters =
                ReadEncounters(folder, manifest.Id, monsters, maps, loot, problems);

            IReadOnlyList<MerchantDef> merchants = ReadMerchants(folder, items, problems);

            return new Package(folder, manifest.Id, manifest, monsters, items, spells, maps,
                               encounters, loot, problems)
            {
                Merchants = merchants,
                Props = props,
            };
        }

        static IReadOnlyList<MerchantDef> ReadMerchants(string folder, IReadOnlyList<Item> items,
                                                        List<ContentProblem> problems)
        {
            var own = new HashSet<string>(items.Select(i => i.Id), StringComparer.Ordinal);
            ItemShelf srd = Library.Srd().Items;
            var all = new List<MerchantDef>();

            foreach ((string file, string text) in Jsons(folder, MerchantsFolder, problems))
            {
                if (!Json.TryParse(text, out System.Text.Json.JsonDocument doc, out string bad))
                {
                    problems.Add(new ContentProblem(file, "", bad));
                    continue;
                }

                using (doc)
                    foreach (System.Text.Json.JsonElement one in doc.RootElement.Items("merchants"))
                    {
                        string id = one.Text("id");

                        if (!Json.IsId(id))
                        {
                            problems.Add(new ContentProblem(file, "", $"'{id}' is not a merchant id"));
                            continue;
                        }

                        IReadOnlyList<string> stock = one.Strings("stock");

                        foreach (string item in stock.Where(i => !own.Contains(i) && !srd.Has(i)))
                            problems.Add(new ContentProblem(file, $"merchants.{id}",
                                $"'{item}' is not an item in this campaign or the SRD"));

                        all.Add(new MerchantDef(id, stock,
                                                one.Has("sell_percent") ? one.Number("sell_percent") : -1));
                    }
            }

            return all;
        }

        static Manifest ReadManifest(string folder, string name, List<ContentProblem> problems)
        {
            var found = ManifestReader.FileNames
                .Select(f => Path.Combine(folder, f))
                .Where(File.Exists)
                .ToList();

            if (found.Count == 0)
            {
                problems.Add(new ContentProblem(name, "",
                    $"a campaign folder needs a {ManifestReader.PackFileName} in it - that is the " +
                    "file that says what this is"));
                return null;
            }

            // both is ambiguous, and picking one would make the other silently dead
            if (found.Count > 1)
                problems.Add(ContentProblem.Caution(name, "",
                    $"this folder has both {ManifestReader.PackFileName} and " +
                    $"{ManifestReader.FileName} - {ManifestReader.PackFileName} is the one being " +
                    "read, and the other is doing nothing"));

            string file = Path.GetFileName(found[0]);

            string text;

            try
            {
                text = File.ReadAllText(found[0]);
            }
            catch (IOException bad)
            {
                problems.Add(new ContentProblem(file, "", "could not be read - " + bad.Message));
                return null;
            }

            Read<Manifest> read = ManifestReader.Parse(text, file, name);

            problems.AddRange(read.Problems);

            return read.Value;
        }

        static void UnknownFolders(string folder, List<ContentProblem> problems)
        {
            foreach (string each in Directory.EnumerateDirectories(folder))
            {
                string name = new DirectoryInfo(each).Name;

                if (Folders.Contains(name, StringComparer.Ordinal))
                {
                    if (NotLoadedYet.Contains(name, StringComparer.Ordinal) &&
                        Directory.EnumerateFileSystemEntries(each).Any())
                        problems.Add(ContentProblem.Caution(name, "",
                            $"'{name}/' is a folder this build knows about but does not load yet - " +
                            "what is in it is being ignored, and will not be once it does"));

                    continue;
                }

                problems.Add(new ContentProblem(name, "",
                    $"'{name}/' is not something a campaign holds - it holds " +
                    $"{Vocabulary.Offer(Folders.Select(f => f + "/"))}"));
            }
        }

        // --- the content folders ----------------------------------------------------------------

        // every json file in one folder, in name order, so a load is the same every run
        static IEnumerable<(string File, string Text)> Jsons(string folder, string sub,
                                                             List<ContentProblem> problems)
        {
            string path = Path.Combine(folder, sub);

            if (!Directory.Exists(path)) yield break;

            foreach (string each in Directory.EnumerateFiles(path, "*.json")
                                             .OrderBy(f => f, StringComparer.Ordinal))
            {
                string name = sub + "/" + Path.GetFileName(each);
                string text;

                try
                {
                    text = File.ReadAllText(each);
                }
                catch (IOException bad)
                {
                    problems.Add(new ContentProblem(name, "", "could not be read - " + bad.Message));
                    continue;
                }

                yield return (name, text);
            }
        }

        static IReadOnlyList<Monster> ReadMonsters(string folder, string campaign,
                                                   List<ContentProblem> problems)
        {
            var all = new List<Monster>();

            foreach ((string file, string text) in Jsons(folder, MonstersFolder, problems))
            {
                MonsterReader.TryRead(text, out IReadOnlyList<Monster> read,
                                      out IReadOnlyList<string> trouble);

                foreach (string one in trouble)
                    problems.Add(new ContentProblem(file, "", one));

                all.AddRange(read);
            }

            return Scoped(all, m => m.Id, MonstersFolder, campaign, problems);
        }

        static IReadOnlyList<Item> ReadItems(string folder, string campaign,
                                             List<ContentProblem> problems)
        {
            var all = new List<Item>();

            foreach ((string file, string text) in Jsons(folder, ItemsFolder, problems))
            {
                ItemReader.TryRead(text, out IReadOnlyList<Item> read,
                                   out IReadOnlyList<string> trouble);

                foreach (string one in trouble)
                    problems.Add(new ContentProblem(file, "", one));

                all.AddRange(read);
            }

            return Scoped(all, i => i.Id, ItemsFolder, campaign, problems);
        }

        static IReadOnlyList<Spell> ReadSpells(string folder, string campaign,
                                               List<ContentProblem> problems)
        {
            var all = new List<Spell>();

            foreach ((string file, string text) in Jsons(folder, SpellsFolder, problems))
            {
                SpellReader.TryRead(text, out IReadOnlyList<Spell> read,
                                    out IReadOnlyList<string> trouble);

                foreach (string one in trouble)
                    problems.Add(new ContentProblem(file, "", one));

                all.AddRange(read);
            }

            return Scoped(all, s => s.Id, SpellsFolder, campaign, problems);
        }

        // A TABLE IS READ LAST because it names the things read before it. A monster it calls for
        // is the campaign's own or the SRD's; one from a dependency is not reachable yet, for the
        // same reason Library.With does not scope ids yet - that is finalizing the pack format.
        static IReadOnlyList<EncounterTable> ReadEncounters(
            string folder, string campaign, IReadOnlyList<Monster> monsters,
            IReadOnlyDictionary<string, MapLayout> maps, IReadOnlyList<LootTable> loot,
            List<ContentProblem> problems)
        {
            var own = new HashSet<string>(monsters.Select(m => m.Id), StringComparer.Ordinal);
            var chests = new HashSet<string>(loot.Select(t => t.Id), StringComparer.Ordinal);
            Bestiary srd = Library.Srd().Bestiary;

            var all = new List<EncounterTable>();

            foreach ((string file, string text) in Jsons(folder, EncountersFolder, problems))
            {
                EncounterReader.TryRead(text, out IReadOnlyList<EncounterTable> read,
                                        out IReadOnlyList<string> trouble);

                foreach (string one in trouble)
                    problems.Add(new ContentProblem(file, "", one));

                // an encounter that calls for a monster nobody shipped is a fight that stops the
                // campaign the night it comes up, so it is caught on the shelf instead
                foreach (EncounterTable table in read)
                    foreach (EncounterEntry entry in table.Entries)
                    {
                        string where = $"tables.{table.Id}.entries.{entry.Id}";

                        foreach (Band band in entry.Monsters)
                            if (!own.Contains(band.Monster) && !srd.Has(band.Monster))
                                problems.Add(new ContentProblem(file, where,
                                    $"'{band.Monster}' is not a monster in this campaign's " +
                                    $"{MonstersFolder}/ or the SRD's"));

                        if (entry.Map.Length > 0 && !maps.ContainsKey(entry.Map))
                            problems.Add(new ContentProblem(file, where,
                                $"'{entry.Id}' is fought on '{entry.Map}' and there is no " +
                                $"{MapsFolder}/{entry.Map}{MapExtension} in this campaign"));

                        if (entry.Loot.Length > 0 && !chests.Contains(entry.Loot))
                            problems.Add(new ContentProblem(file, where,
                                $"'{entry.Id}' leaves '{entry.Loot}' and there is no loot table " +
                                $"by that name in this campaign's {LootFolder}/"));
                    }

                all.AddRange(read);
            }

            return Scoped(all, t => t.Id, EncountersFolder, campaign, problems);
        }

        // LOOT IS READ AFTER ITEMS because it names them, and before encounters because a fight
        // names it. an item is the campaign's own or the SRD's, like an encounter's monster; a
        // table one entry rolls may be in any file of loot/, so that link and the loop check are
        // asked of every file's tables together once they are all read.
        static IReadOnlyList<LootTable> ReadLoot(string folder, string campaign,
                                                 IReadOnlyList<Item> items,
                                                 List<ContentProblem> problems)
        {
            var own = new HashSet<string>(items.Select(i => i.Id), StringComparer.Ordinal);
            ItemShelf srd = Library.Srd().Items;

            var all = new List<LootTable>();
            var from = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach ((string file, string text) in Jsons(folder, LootFolder, problems))
            {
                LootReader.TryRead(text, out IReadOnlyList<LootTable> read,
                                   out IReadOnlyList<string> trouble);

                foreach (string one in trouble)
                    problems.Add(new ContentProblem(file, "", one));

                // a find of an item nobody shipped is a chest that cannot be opened, so it is
                // caught on the shelf, not the night the dice land on it
                foreach (LootTable table in read)
                {
                    foreach (LootEntry entry in table.Entries)
                        foreach (Lot lot in entry.Items)
                            if (!own.Contains(lot.Item) && !srd.Has(lot.Item))
                                problems.Add(new ContentProblem(file,
                                    $"tables.{table.Id}.entries.{entry.Id}",
                                    $"'{lot.Item}' is not an item in this campaign's " +
                                    $"{ItemsFolder}/ or the SRD's"));

                    from.TryAdd(table.Id, file);
                }

                all.AddRange(read);
            }

            IReadOnlyList<LootTable> kept = Scoped(all, t => t.Id, LootFolder, campaign, problems);

            var every = new LootTables(kept);

            foreach (LootTable table in kept)
                foreach (LootEntry entry in table.Entries.Where(e => e.Kind == LootKind.Table))
                    if (!every.Has(entry.Table))
                        problems.Add(new ContentProblem(from[table.Id],
                            $"tables.{table.Id}.entries.{entry.Id}",
                            $"'{entry.Id}' rolls '{entry.Table}' and there is no loot table by " +
                            $"that name in this campaign's {LootFolder}/"));

            // a loop inside one file was already said by that file's reader; this is the one
            // that runs through two files, which no single reader could see
            IReadOnlyList<string> cycle = every.Cycle();

            if (cycle.Select(id => from[id]).Distinct().Count() > 1)
                problems.Add(new ContentProblem(from[cycle[0]], $"tables.{cycle[0]}",
                    LootReader.Loop(kept) + " - the loop runs through " +
                    string.Join(" and ", cycle.Select(id => from[id]).Distinct())));

            return kept;
        }

        // EVERY ID A CAMPAIGN DEFINES IS ITS OWN. A pack that ships a "goblin" must not shadow the
        // SRD's, so ids are read locally and checked here rather than being written scoped in the
        // file - an author should not have to spell their own campaign's name on every line.
        static IReadOnlyList<T> Scoped<T>(List<T> all, Func<T, string> idOf, string where,
                                          string campaign, List<ContentProblem> problems)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var kept = new List<T>();

            foreach (T one in all)
            {
                string id = idOf(one) ?? "";

                if (!ContentId.IsLocal(id))
                {
                    problems.Add(new ContentProblem(where, id,
                        $"'{id}' is not an id a campaign may define - lowercase a-z, 0-9 and " +
                        "underscore, and no dots. The campaign's own name is added for you"));
                    continue;
                }

                if (!seen.Add(id))
                {
                    problems.Add(new ContentProblem(where, id,
                        $"'{id}' is defined twice in {where}/"));
                    continue;
                }

                kept.Add(one);
            }

            return kept;
        }

        static IReadOnlyDictionary<string, MapLayout> ReadMaps(string folder, string campaign,
                                                               List<ContentProblem> problems,
                                                               Dictionary<string, IReadOnlyList<Prop>> props = null)
        {
            var maps = new Dictionary<string, MapLayout>(StringComparer.Ordinal);

            string path = Path.Combine(folder, MapsFolder);

            if (!Directory.Exists(path)) return maps;

            foreach (string each in Directory.EnumerateFiles(path, "*" + MapExtension)
                                             .OrderBy(f => f, StringComparer.Ordinal))
            {
                string file = MapsFolder + "/" + Path.GetFileName(each);
                string id = Path.GetFileNameWithoutExtension(each);

                if (!ContentId.IsLocal(id))
                {
                    problems.Add(new ContentProblem(file, "",
                        $"'{id}' is not a map id - a map is named by its file, so the file name is " +
                        "lowercase a-z, 0-9 and underscore"));
                    continue;
                }

                string text;

                try
                {
                    text = File.ReadAllText(each);
                }
                catch (IOException bad)
                {
                    problems.Add(new ContentProblem(file, "", "could not be read - " + bad.Message));
                    continue;
                }

                if (!MapDraft.TryRead(text, out MapDraft draft, out string problem))
                {
                    problems.Add(new ContentProblem(file, "", problem));
                    continue;
                }

                if (!draft.Sound)
                {
                    foreach (string one in draft.Problems())
                        problems.Add(new ContentProblem(file, "", one));

                    continue;
                }

                if (maps.ContainsKey(id))
                {
                    problems.Add(new ContentProblem(file, "", $"there are two maps called '{id}'"));
                    continue;
                }

                // a prop the palette doesn't have is drawn as a placeholder - worth a word, not a refusal
                foreach (string unknown in new MapEditor(draft).UnknownProps)
                    problems.Add(ContentProblem.Caution(file, "props",
                        $"'{unknown}' is not in the map builder's palette, so the table draws a placeholder"));

                maps[id] = draft.Layout();

                if (props != null) props[id] = draft.Props.ToList();
            }

            return maps;
        }

        // a chapter that names a map nobody shipped is a campaign that stops mid-play, so it is
        // caught at load rather than at the moment the player walks into it
        static void ChaptersNameRealMaps(Manifest manifest,
                                         IReadOnlyDictionary<string, MapLayout> maps,
                                         List<ContentProblem> problems)
        {
            foreach (Chapter chapter in manifest.Chapters)
                foreach (string map in chapter.Maps)
                    if (!maps.ContainsKey(map))
                        problems.Add(new ContentProblem(
                            ManifestReader.PackFileName, $"chapters.{chapter.Id}",
                            $"chapter '{chapter.Id}' is played on '{map}' and there is no " +
                            $"{MapsFolder}/{map}{MapExtension} in this campaign"));
        }
    }

    // a shop, as the campaign writes it: the id, the stock, and what it pays for what it buys
    public sealed class MerchantDef
    {
        public MerchantDef(string id, IReadOnlyList<string> stock, int sellPercent = -1)
        {
            Id = id ?? "";
            Stock = stock ?? Array.Empty<string>();
            SellPercent = sellPercent;
        }

        public string Id { get; }

        public IReadOnlyList<string> Stock { get; }

        public int SellPercent { get; }

        public string NameKey => Core.Localization.KeyConventions.Key("merchant", Id, "name");

        public Content.Inventory.Merchant Open(ItemShelf shelf) =>
            new Content.Inventory.Merchant(Id, shelf, Stock, SellPercent);
    }
}
