using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Workshop;
using Game.Campaigns;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE BOOK'S WORKSHOP (cc_task_m Part 1), where "Map builder" was in the contents: the hub (Hub), the new-campaign
    // wizard (WizardScreen) and a campaign's own page (CampaignScreen), each on a panel over the open book. Opened with a
    // campaign named (`--show workshop --open <id>`), or with one in hand when the table comes back from the builder
    // (GameState.Editing), it opens that campaign's page instead of the hub
    public sealed class WorkshopPage : IBookPage
    {
        public const string PageId = "workshop";

        public string Id => PageId;

        public string NameKey => WorkshopWords.BookKey;

        // MY CAMPAIGNS: user://campaigns (CampaignFolders.Mine), a campaign root after the game's own
        public static string Mine => CampaignFolders.Mine();

        // the game's own campaigns are the Workshop's to change only in the editor build (WORKSHOP_DESIGN.md, "Where things
        // live"): Kathleen opens her campaigns here; a player's copy of the game has them on the shelf to play
        public static bool EditorBuild => OS.HasFeature("editor");

        // an id some campaign in some root already has: what a new one, a rename or a restore can't be
        public static bool Taken(string id) => Ids().Contains(id);

        static HashSet<string> Ids() => WorkshopShelf.Ids(CampaignFolders.Roots());

        public void Open(Launch launch, string open = null)
        {
            // back from the builder: the campaign's page as it was left, on its chapters, the problems read again (a map
            // was made or changed)
            if (GameState.Editing is { } editing && Directory.Exists(editing.Folder))
            {
                editing.Refresh();

                // back from a test (cc_task_q 3.1): on the card that was playing, its block highlighted; or a tried fight's
                if (GameState.EditingAt is { } at)
                {
                    GameState.EditingAt = null;
                    CampaignScreen page = CampaignScreen.Show(launch, editing, at.Tab, fresh: false);
                    if (at.Tab == Content.Workshop.WorkshopTab.Story) page.OpenStory(at.Card, at.Block);
                    else page.OpenCard("fight:" + at.Card);
                    return;
                }

                CampaignScreen.Show(launch, editing, Content.Workshop.WorkshopTab.Chapters, fresh: false);
                return;
            }

            GameState.Editing = null;

            if (FolderOf(open) is { } folder)
            {
                OpenCampaign(launch, folder);
                return;
            }

            Hub.Show(launch);
        }

        // a campaign's folder by its id: My campaigns' first, then (in the editor) the game's own
        public static string FolderOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (string root in EditorBuild ? new[] { Mine, CampaignFolders.Local() } : new[] { Mine })
                if (Directory.Exists(Path.Combine(root, id))) return Path.Combine(root, id);

            return null;
        }

        // THE ONE DOOR INTO A CAMPAIGN'S PAGE. The game's own campaign is never opened for editing in a release build
        // (cc_task_m2 Part 6): it gets the hub, where its card offers Make an editable copy
        public static void OpenCampaign(Launch launch, string folder)
        {
            if (!EditorBuild && IsTheGames(folder))
            {
                GD.PushWarning($"workshop: {folder} is one of the game's own campaigns - copy it to edit it");
                Hub.Show(launch);
                return;
            }

            CampaignScreen.Show(launch, CampaignEditor.Open(folder));
        }

        // whether a folder is one of the game's own campaigns (under the game's campaigns/, not My campaigns)
        public static bool IsTheGames(string folder) =>
            Path.GetFullPath(folder).StartsWith(Path.GetFullPath(CampaignFolders.Local()).TrimEnd('/', '\\') + Path.DirectorySeparatorChar,
                                                System.StringComparison.OrdinalIgnoreCase);
    }
}
