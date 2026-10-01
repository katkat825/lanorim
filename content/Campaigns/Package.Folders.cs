using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Encounters;
using Content.Items;
using Content.Loot;
using Content.Monsters;
using Content.Schema;
using Content.Spells;
using Core.Magic;
using Core.Space;
using Core.Tables;

namespace Content.Campaigns
{
    public sealed partial class Package
    {
        // --- the content folders ----------------------------------------------------------------

        // every json file in one of the pack's folders, named from the pack: "monsters/goblin.json"
        static IEnumerable<(string File, string Text)> Jsons(string folder, string sub,
                                                             List<ContentProblem> problems) =>
            PackFolder.Read(Path.Combine(folder, sub), ".json", sub, problems).Select(f => (f.File, f.Text));

        // one folder of a list file type (monsters/, items/, spells/), each file read by the type's
        // EntryList, each problem said with its file and the entry it is in
        static IReadOnlyList<T> ReadFolder<T>(string folder, string sub, EntryList<T> reader, Func<T, string> id,
                                              List<ContentProblem> problems)
            where T : class
        {
            var all = new List<T>();

            foreach ((string file, string text) in Jsons(folder, sub, problems))
            {
                var trouble = new List<ContentProblem>();

                all.AddRange(reader.Read(text, trouble));

                problems.AddRange(trouble.Select(p => new ContentProblem(file, p.Where, p.What)));
            }

            return Scoped(all, id, sub, problems);
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

            return Scoped(all, t => t.Id, EncountersFolder, problems);
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

            IReadOnlyList<LootTable> kept = Scoped(all, t => t.Id, LootFolder, problems);

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
                                          List<ContentProblem> problems)
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
    }
}
