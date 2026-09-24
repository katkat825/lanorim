using System;
using System.Collections.Generic;
using Content.Campaigns;
using Content.Schema;

namespace Game.Campaigns
{
    // ONE CAMPAIGN, AS THE GAME HOLDS IT: the package that was read, and the two things only the
    // Godot side knows - how many strings its locale contributed, and which dependencies it is
    // still waiting on.
    //
    // ADAPTED from the old build's Loaded, which was forty lines of passthrough properties onto
    // Package - Monsters, Classes, Kit, Items, Dialogue, Barks, Beats, Hints, Companions, Sheet,
    // Places, Entities, Quests, Roads. Every one of those was a second name for something Package
    // already exposed, and keeping them in step by hand is exactly the duplication this project
    // refuses everywhere else. `Package` is public; ask it.
    public sealed class Loaded
    {
        public Loaded(Package package, int strings, IReadOnlyList<string> missing = null)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            Strings = strings;
            Missing = missing ?? Array.Empty<string>();
        }

        public Package Package { get; }

        public string Id => Package.Id;

        public string Folder => Package.Folder;

        public Manifest Manifest => Package.Manifest;

        public IReadOnlyList<ContentProblem> Problems => Package.Problems;

        // it could not be read at all
        public bool Failed => !Package.Sound;

        // it read fine and is not in play, because something it needs is not installed
        public IReadOnlyList<string> Missing { get; }

        public bool Waiting => !Failed && Missing.Count > 0;

        public bool InPlay => !Failed && !Waiting;

        // how many localized strings its locale/ folder put on the TranslationServer
        public int Strings { get; }

        // every key this campaign promises its own CSV answers. The audit walks this rather than a
        // list kept in step by hand - and a campaign not in play promises nothing, because its
        // strings were never registered.
        public IEnumerable<string> Keys()
        {
            if (!InPlay) return Array.Empty<string>();

            return Package.Keys();
        }

        public override string ToString() =>
            Failed ? Package.ToString()
          : Waiting ? $"{Package} - NOT LOADED, it needs {string.Join(", ", Missing)}"
          : $"{Package}, {Strings} strings";
    }
}
