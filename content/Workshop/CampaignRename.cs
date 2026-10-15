using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;
using Core.Localization;

namespace Content.Workshop
{
    // A CAMPAIGN GIVEN ANOTHER ID (cc_task_m 2.2's Rename, and a duplicate's or a restore's new id): the folder, pack.json's
    // id and every key in its locale that's spelled with the id, together - they have to agree, or nothing in it is
    // found (ManifestReader's folder-matches-id rule; Shelf's one id a pack). The story's .yarn files don't spell it
    // (a line's tag is its own; the campaign is added in its key), so they're untouched.
    //
    // What follows a campaign's id and isn't changed: the saves of anyone who played it under the old one. A Workshop
    // campaign being renamed is its author's, so that's said in the run log rather than migrated
    public static class CampaignRename
    {
        // the folder it's in now, or null and why not (another folder has that name, or the disk said no)
        public static string To(string folder, string id, out string problem)
        {
            problem = null;
            string old = new DirectoryInfo(folder).Name;
            string target = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(folder).TrimEnd('/', '\\')) ?? "", id);

            if (!ContentId.IsCampaign(id))
            {
                problem = $"'{id}' is not a campaign id";
                return null;
            }

            if (id == old) return folder;

            if (Directory.Exists(target))
            {
                problem = $"there is already a campaign folder called '{id}'";
                return null;
            }

            try
            {
                Directory.Move(folder, target);
                Respell(target, old, id);
                return target;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                problem = could.Message;
                return null;
            }
        }

        // pack.json's id, and the locale's keys and file name, from one id to the other (the folder already moved)
        public static void Respell(string folder, string old, string id)
        {
            string pack = Path.Combine(folder, ManifestReader.PackFileName);

            if (File.Exists(pack))
            {
                Read<Manifest> read = ManifestReader.Parse(File.ReadAllText(pack), ManifestReader.PackFileName, old, lenient: true);

                if (read.Value != null)
                {
                    CampaignDraft draft = CampaignDraft.Of(read.Value);
                    draft.Id = id;
                    File.WriteAllText(pack, ManifestWriter.Write(draft.ToManifest()));
                }
                else
                {
                    // a pack.json that won't read is respelled where it says its id, and nothing else is touched
                    File.WriteAllText(pack, File.ReadAllText(pack).Replace($"\"{old}\"", $"\"{id}\""));
                }
            }

            string locale = Path.Combine(folder, Package.LocaleFolder);

            if (!Directory.Exists(locale)) return;

            foreach (string csv in Directory.EnumerateFiles(locale, "*.csv").ToList())
            {
                LocaleSheet sheet = LocaleSheet.Of(File.ReadAllText(csv));
                sheet.RenameKeys(key => Key(key, old, id));

                string named = Path.GetFileNameWithoutExtension(csv) == old ? Path.Combine(locale, id + ".csv") : csv;

                File.WriteAllText(named, sheet.Text());
                if (named != csv) File.Delete(csv);
            }
        }

        // A KEY SPELLED WITH THE CAMPAIGN'S ID, respelled: campaign.<id>.*, quest.<id>.*, a story line
        // (dialogue.<speaker>.line.<id>.<line>) and a campaign's bark (dialogue.<speaker>.bark.<situation>.<id>.<n>).
        // Only there: a chapter, a line or an item that happens to share the campaign's name keeps it
        public static string Key(string key, string old, string id)
        {
            string[] parts = key.Split('.');

            int at = parts.Length < 2 ? -1
                   : parts[0] is KeyConventions.CampaignNs or KeyConventions.QuestNs ? 1
                   : parts[0] == KeyConventions.DialogueNs && parts.Length > 4 && parts[2] == "line" ? 3
                   : parts[0] == KeyConventions.DialogueNs && parts.Length > 5 && parts[2] == KeyConventions.BarkAspect ? 4
                   : -1;

            if (at < 0 || parts[at] != old) return key;

            parts[at] = id;
            return string.Join(".", parts);
        }
    }
}
