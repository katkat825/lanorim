using System;
using System.IO;

namespace Content.Workshop
{
    // THE AUTOSAVE (cc_task_m Part 6; WORKSHOP_DESIGN.md principle 4, nothing is lost): the draft as it stands, kept in
    // the campaign's own .draft/ folder a few seconds after the author stops changing it. Save writes the real files
    // and throws the draft away; a draft newer than the real files when the campaign is opened is changes that were
    // never saved, and the page asks whether to keep them. The loader skips a dot-folder (PackFolder.IsHidden), so a
    // draft never plays
    public static class Autosave
    {
        public const string Folder = ".draft";

        public const string FileName = "draft.json";

        public static string PathOf(string campaign) => Path.Combine(campaign, Folder, FileName);

        // null when it was kept, or the disk's own reason it wasn't
        public static string Write(string campaign, CampaignDraft draft)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(campaign, Folder));
                File.WriteAllText(PathOf(campaign), draft.Snapshot());
                return null;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                return could.Message;
            }
        }

        public static bool Has(string campaign) => File.Exists(PathOf(campaign));

        // changes from last time: a draft written after the campaign's files last were
        public static bool Newer(string campaign) =>
            Has(campaign) && File.GetLastWriteTimeUtc(PathOf(campaign)) > CampaignFiles.Edited(campaign);

        // the draft kept, or null when there's none (or it can't be read)
        public static CampaignDraft Read(string campaign)
        {
            try
            {
                return Has(campaign) ? CampaignDraft.FromSnapshot(File.ReadAllText(PathOf(campaign))) : null;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static void Forget(string campaign)
        {
            string folder = Path.Combine(campaign, Folder);

            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                // a draft that won't go is asked about again next time, which is the safe way round
            }
        }
    }
}
