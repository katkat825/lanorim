using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Schema;
using Godot;

// this class is called Library and so is the content one, and it has a property called
// Content which shadows the root namespace - so the content library is aliased once here
// rather than written out and qualified at every use
using ContentLibrary = Content.Schema.Library;

namespace Game.Campaigns
{
    // CAMPAIGN DISCOVERY: walk the roots, read every folder in them, resolve what depends on what,
    // register the locales, and hand back one content library with the SRD and everything in play
    // laid on top of it.
    //
    // ADAPTED from the old build's Game.Campaigns.Library. That one WAS the archetype source - the
    // game asked it to create actors and look up loot - because the old content model had no single
    // library of its own. lanorim has `Content.Schema.Library`, which already knows how to be the
    // SRD and how to have a campaign laid on top of it, so this does discovery and nothing else and
    // hands that back. It is the loader, not a second content model.
    //
    // NAMED Game.Campaigns.Library beside Content.Schema.Library on purpose, and they are different
    // things: this one finds campaigns, that one holds content.
    public sealed class Library
    {
        Library(IReadOnlyList<Loaded> campaigns, Shelf shelf, ContentLibrary content)
        {
            Campaigns = campaigns;
            Shelf = shelf;
            Content = content;
        }

        public IReadOnlyList<Loaded> Campaigns { get; }

        public Shelf Shelf { get; }

        // the SRD, with every campaign in play laid over it
        public ContentLibrary Content { get; }

        public IEnumerable<Loaded> InPlay => Campaigns.Where(c => c.InPlay);

        public Loaded Campaign(string id) =>
            id == null ? null : Campaigns.FirstOrDefault(c => c.Id == id);

        public bool Has(string id) => Campaign(id) != null;

        // the campaigns a player could actually start, in the order they were found
        public IEnumerable<Loaded> Playable =>
            InPlay.Where(c => c.Manifest != null && c.Manifest.IsPlayable);

        public IReadOnlyList<ContentProblem> Problems => Shelf.Problems;

        public static Library Load() => Load(CampaignFolders.Roots());

        public static Library Load(IReadOnlyList<string> roots)
        {
            var packages = new List<Package>();

            foreach (string root in roots ?? System.Array.Empty<string>())
                foreach (string folder in CampaignFolders.In(root))
                    packages.Add(Package.Read(folder));

            // DEPENDENCIES FIRST, LOCALES SECOND. A pack that is waiting on something uninstalled
            // is not in play, and registering its strings would put words on the TranslationServer
            // for content nothing can reach - which reads as a locale full of orphans rather than
            // as a missing dependency.
            Shelf shelf = Shelf.Of(packages);

            var loaded = new List<Loaded>();

            ContentLibrary content = ContentLibrary.Srd();

            foreach (Shelf.Entry entry in shelf.Entries)
            {
                int strings = entry.InPlay ? CampaignLocale.Register(entry.Package.Folder) : 0;

                loaded.Add(new Loaded(entry.Package, strings, entry.Missing));

                if (entry.InPlay) content = content.With(entry.Package);
            }

            var library = new Library(loaded, shelf, content);

            // developer diagnostic, exempt from localization
            GD.Print($"campaigns  {library}");

            foreach (ContentProblem problem in shelf.Problems)
                GD.PushWarning("campaigns: " + problem);

            return library;
        }

        // takes a campaign's words back off the TranslationServer. The content library is not
        // unloaded because it was never mutated - dropping the reference is the whole of it.
        public void Unload(string id)
        {
            Loaded one = Campaign(id);

            if (one == null) return;

            CampaignLocale.Unregister(one.Folder);
        }

        public override string ToString() =>
            $"{InPlay.Count()} of {Campaigns.Count} campaigns in play" +
            (Shelf.Problems.Count > 0 ? $", {Shelf.Problems.Count} problems" : "");
    }
}
