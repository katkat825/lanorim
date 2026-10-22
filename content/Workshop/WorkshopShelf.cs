using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Workshop
{
    // ONE CAMPAIGN ON THE HUB'S SHELF (cc_task_m 1.1): its name, its cover, its chapters, when it was last changed,
    // and the two things its card warns of - another campaign already has its id (so it isn't loaded), and the
    // problems that stop it loading ("Won't load yet: 2 problems")
    public sealed class CampaignCard
    {
        public string Id { get; init; } = "";

        public string Folder { get; init; } = "";

        // its name's key, and the name its own locale gives it in English: the hub shows the key's words when the game
        // has registered them, else this (a campaign that isn't loaded has nothing registered)
        public string NameKey { get; init; } = "";

        public string Name { get; init; } = "";

        public int Chapters { get; init; }

        public DateTime Edited { get; init; }

        // the cover: pack.json's "preview" (preview.png, as the Workshop copies one in), or null for the GM screen's picture
        public string Cover { get; init; }

        public string GmScreen { get; init; } = Manifest.GmScreens[0];

        // an earlier root's campaign has this id, so this one isn't loaded (Shelf.Entry.ShadowedBy)
        public bool Shadowed { get; init; }

        public IReadOnlyList<ContentProblem> Problems { get; init; } = Array.Empty<ContentProblem>();

        public int Errors => WorkshopProblems.Errors(Problems);

        public override string ToString() =>
            $"{Id}: {Chapters} chapters" + (Shadowed ? ", its id is taken" : "") + (Errors > 0 ? $", {Errors} errors" : "");
    }

    // THE HUB'S SHELVES, AS LOGIC (cc_task_m Parts 1 and 2): the campaigns in a root as cards, and what the hub does to
    // one - duplicate it, give it a free id, and (RecentlyDeleted) delete and restore it
    public static class WorkshopShelf
    {
        public const string CoverFile = "preview.png";

        // the campaigns in a root, in folder order. `earlier` is every id the roots before it hold: one of those here
        // isn't loaded (the first root's wins), and its card says so
        public static IReadOnlyList<CampaignCard> Cards(string root, IReadOnlyCollection<string> earlier)
        {
            var cards = new List<CampaignCard>();

            foreach (string folder in PackFolder.Folders(root))
            {
                string id = new DirectoryInfo(folder).Name;
                var read = new List<ContentProblem>();
                CampaignDraft draft = CampaignFiles.Open(folder, read);
                string cover = draft.Preview.Length > 0 ? Path.Combine(folder, draft.Preview) : null;

                cards.Add(new CampaignCard
                {
                    Id = id,
                    Folder = folder,
                    NameKey = CampaignFiles.NameKey(id),
                    Name = draft.Name,
                    Chapters = draft.Chapters.Count,
                    Edited = CampaignFiles.Edited(folder),
                    Cover = cover != null && File.Exists(cover) ? cover : null,
                    GmScreen = draft.GmScreen,
                    Shadowed = earlier?.Contains(id) ?? false,
                    Problems = WorkshopProblems.Of(folder),
                });
            }

            return cards;
        }

        // every id in the roots, in the order they're read: what a new id, a rename or a restore must not be
        public static HashSet<string> Ids(IEnumerable<string> roots) =>
            new(roots.SelectMany(PackFolder.Folders).Select(f => new DirectoryInfo(f).Name), StringComparer.Ordinal);

        // DUPLICATE: a copy beside it under the next free id (Recently deleted and the autosave left behind), named
        // `name` (the screen's "Copy of ..."). Where the copy is, or null and why not
        public static string Duplicate(string folder, Func<string, bool> taken, string name, out string problem) =>
            Duplicate(folder, taken, name, null, null, out problem);

        // MAKE AN EDITABLE COPY of one of the game's own campaigns (cc_task_m2 Part 6): Duplicate, into My campaigns
        // (`mine`), under a free id, with based_on its own id. The game's campaign itself is never written to
        public static string EditableCopy(string folder, string mine, Func<string, bool> taken, string name, out string problem) =>
            Duplicate(folder, taken, name, mine, new DirectoryInfo(folder).Name, out problem);

        // a copy into `into` (null: beside the original), based on `basedOn` (null: whatever the original was based on)
        static string Duplicate(string folder, Func<string, bool> taken, string name, string into, string basedOn,
                                out string problem)
        {
            problem = null;
            string old = new DirectoryInfo(folder).Name;
            string root = into ?? Path.GetDirectoryName(Path.GetFullPath(folder).TrimEnd('/', '\\')) ?? "";
            string id = ContentId.Free(old, one => taken(one) || Directory.Exists(Path.Combine(root, one)));
            string target = Path.Combine(root, id);

            try
            {
                Copy(folder, target);
                CampaignRename.Respell(target, old, id);

                List<ContentProblem> read = new();
                CampaignDraft copy = CampaignFiles.Open(target, read);
                copy.Name = name ?? copy.Name;
                if (basedOn != null) copy.BasedOn = basedOn;
                problem = CampaignFiles.Save(copy, target);

                return problem == null ? target : null;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                problem = could.Message;
                return null;
            }
        }

        // RENAME, for a card whose id another campaign has: the next free id, folder and keys together
        public static string Rename(string folder, Func<string, bool> taken, out string problem)
        {
            string old = new DirectoryInfo(folder).Name;
            string root = Path.GetDirectoryName(Path.GetFullPath(folder).TrimEnd('/', '\\')) ?? "";

            return CampaignRename.To(folder, ContentId.Free(old, one => taken(one) || Directory.Exists(Path.Combine(root, one))), out problem);
        }

        static void Copy(string from, string to)
        {
            Directory.CreateDirectory(to);

            foreach (string file in Directory.EnumerateFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));

            foreach (string folder in Directory.EnumerateDirectories(from).Where(f => !PackFolder.IsHidden(f)))
                Copy(folder, Path.Combine(to, Path.GetFileName(folder)));
        }
    }
}
