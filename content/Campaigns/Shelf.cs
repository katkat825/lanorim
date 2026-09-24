using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;

namespace Content.Campaigns
{
    // WHAT IS INSTALLED, AND WHICH OF IT IS ACTUALLY IN PLAY.
    //
    // A pack that names a dependency nobody installed is not half-loaded: it is read, listed, and
    // left out, with a sentence saying which pack to subscribe to. Half-loading it would give a
    // campaign whose monsters exist and whose items do not, which fails in the middle of a fight
    // rather than on the shelf.
    //
    // ADAPTED from the old build's Shelf, which also cross-checked minis and dialogue across packs
    // - "this conversation is keyed under a creature nothing installed can be". Those checks want a
    // mini registry and a dialogue book, and both are Phase 7. What is here is the half that does
    // not: read the packs, resolve the dependencies, and say what is playable.
    public sealed class Shelf
    {
        readonly List<Entry> _entries = new List<Entry>();

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        Shelf() { }

        public sealed class Entry
        {
            public Entry(Package package, IReadOnlyList<string> missing)
            {
                Package = package;
                Missing = missing ?? Array.Empty<string>();
            }

            public Package Package { get; }

            public string Id => Package.Id;

            // ids this pack asked for that nothing on the shelf provides
            public IReadOnlyList<string> Missing { get; }

            public bool InPlay => Package.Sound && Missing.Count == 0;

            public override string ToString() =>
                Missing.Count > 0
                    ? $"{Package} - NOT LOADED, it needs {string.Join(", ", Missing)}"
                    : Package.ToString();
        }

        public IReadOnlyList<Entry> Entries => _entries;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public IEnumerable<Package> Loaded => _entries.Where(e => e.InPlay).Select(e => e.Package);

        public Entry Of(string id) =>
            id == null ? null : _entries.FirstOrDefault(e => e.Id == id);

        public bool Has(string id) => Of(id) != null;

        // the campaigns a player can actually start
        public IEnumerable<Package> Playable =>
            Loaded.Where(p => p.Manifest != null && p.Manifest.IsPlayable);

        public static Shelf Of(IEnumerable<Package> packages)
        {
            var shelf = new Shelf();

            var read = new List<Package>(packages?.Where(p => p != null) ?? Array.Empty<Package>());

            // ALL PRESENT IDS FIRST, so a dependency resolves whichever order the folders were
            // walked in - otherwise a pack would load or not depending on how the disk listed it
            var present = new HashSet<string>(
                read.Where(p => p.Sound).Select(p => p.Id), StringComparer.Ordinal);

            foreach (Package package in read)
            {
                var missing = new List<string>();

                if (package.Sound)
                    foreach (string needed in package.Manifest.Dependencies)
                    {
                        if (present.Contains(needed)) continue;

                        missing.Add(needed);

                        shelf._problems.Add(new ContentProblem(
                            package.Id + "/" + ManifestReader.PackFileName, "dependencies",
                            $"'{package.Id}' needs the pack '{needed}', and it is not installed - " +
                            "nothing of this pack is loaded until it is. Subscribe to it, or " +
                            "take the line out if it is no longer needed"));
                    }

                shelf._entries.Add(new Entry(package, missing));
            }

            // a pack that could not be read carries its own problems onto the shelf, so one list
            // answers "what is wrong with my install" rather than one list per folder
            foreach (Entry entry in shelf._entries)
                shelf._problems.AddRange(entry.Package.Problems);

            shelf.CrossCheck();

            return shelf;
        }

        // TWO PACKS MAY NOT DEFINE THE SAME THING. Within a pack, Package catches a duplicate id;
        // across packs nothing does, and the loser would be whichever the shelf happened to add
        // second. Ids are scoped by campaign, so this can only happen when two folders claim the
        // same campaign id - which is worth saying plainly.
        void CrossCheck()
        {
            var seen = new Dictionary<string, Entry>(StringComparer.Ordinal);

            foreach (Entry entry in _entries)
            {
                if (string.IsNullOrEmpty(entry.Id)) continue;

                if (seen.TryGetValue(entry.Id, out Entry already))
                {
                    _problems.Add(new ContentProblem(
                        entry.Package.Folder, "id",
                        $"'{entry.Id}' is also the id of the pack in '{already.Package.Folder}' - " +
                        "two packs cannot share an id, because the id is how every key in them is " +
                        "spelled. Rename one, folder and id together"));
                    continue;
                }

                seen[entry.Id] = entry;
            }
        }

        // every key everything in play promises the locale answers
        public IEnumerable<string> Keys() =>
            Loaded.SelectMany(p => p.Keys()).Distinct(StringComparer.Ordinal);

        public override string ToString() =>
            $"{_entries.Count(e => e.InPlay)} of {_entries.Count} packs in play" +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
