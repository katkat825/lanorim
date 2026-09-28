using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Items;
using Content.Maps;
using Content.Monsters;
using Content.Schema;
using Core.Magic;
using Core.Space;
using Core.Tables;

namespace Content.Campaigns
{
    public sealed partial class Package
    {
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
            IReadOnlyDictionary<string, MapLayout> maps = ReadMaps(folder, problems, props);

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

                        var unknown = new List<string>();
                        Keyed.OnlyKnown(one, new[] { "id", "stock", "sell_percent" }, id, unknown);

                        foreach (string why in unknown)
                            problems.Add(new ContentProblem(file, $"merchants.{id}", why));

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
    }
}
