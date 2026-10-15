using System.Collections.Generic;
using System.IO;
using Godot;

namespace Game.Campaigns
{
    public static class CampaignFolders
    {
        public const string FolderName = "campaigns";

        public static IReadOnlyList<string> Roots()
        {
            var roots = new List<string>();

            Add(roots, Local());

            // MY CAMPAIGNS (cc_task_m Part 2.1): the Workshop's, after the game's own, so a campaign made there is on the
            // book's shelf and plays like any other. A later one with an earlier one's id isn't loaded (Shelf.Entry.ShadowedBy)
            Add(roots, Mine());

            return roots;
        }

        // where My campaigns live: user://campaigns, the player's own space, in the editor and in an exported build alike.
        // the Workshop's probe keeps one of its own (check-workshop), never the player's
        public static string MineFolder { get; set; } = "user://campaigns";

        public static string Mine() => ProjectSettings.GlobalizePath(MineFolder);

        public static string Local() =>
            OS.HasFeature("editor")
                ? Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", FolderName))
                : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".", FolderName);

        static void Add(List<string> roots, string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return;

            // a root added twice would load every campaign in it twice and collide each id with itself
            foreach (string already in roots)
                if (string.Equals(already, root, System.StringComparison.OrdinalIgnoreCase)) return;

            roots.Add(root);
        }

        // sorted because filesystem enumeration order is otherwise unstable; a dot-folder (Recently deleted) is the
        // Workshop's and not a campaign
        public static IReadOnlyList<string> In(string root) => Content.Schema.PackFolder.Folders(root);
    }
}
