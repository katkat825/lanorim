using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Campaigns;

namespace Content.Workshop
{
    // RECENTLY DELETED (cc_task_m 2.3; WORKSHOP_DESIGN.md principle 4): a deleted campaign isn't gone, it's moved to
    // My campaigns' .deleted/<id>_<date>/ for thirty days. Restore puts it back (under the next free id if its own has
    // been taken since); Delete forever and the thirty days are the only ways one goes. The loader skips the dot-folder
    // (PackFolder.IsHidden), so a deleted campaign isn't on the book's shelf.
    // A MAP THE SAME WAY (Kathleen's UI notes, 2026-10-27): its file moved to its own folder's .deleted/<id>_<date>.map,
    // My maps' or a campaign's maps/, and back again; the readers skip a dot-folder, and a campaign's copy leaves it out
    public static class RecentlyDeleted
    {
        public const string Folder = ".deleted";

        public const int KeptFor = 30;

        const string DateShape = "yyyy-MM-dd";

        static readonly Regex Named = new(@"^(?<id>[a-z0-9_]+?)_(?<date>\d{4}-\d{2}-\d{2})(_\d+)?$", RegexOptions.Compiled);

        // `Folder`: where it is now, a campaign's folder or a map's file
        public sealed record Item(string Id, DateTime Deleted, string Folder)
        {
            // the days it has left before it goes for good
            public int DaysLeft(DateTime today) => Math.Max(0, KeptFor - (int)(today.Date - Deleted.Date).TotalDays);

            public bool Expired(DateTime today) => (today.Date - Deleted.Date).TotalDays > KeptFor;
        }

        public static string In(string root) => Path.Combine(root, Folder);

        static string Stamp(string id, DateTime today) => $"{id}_{today.ToString(DateShape, CultureInfo.InvariantCulture)}";

        static Item Parse(string path, string name) =>
            Named.Match(name) is { Success: true } named &&
            DateTime.TryParseExact(named.Groups["date"].Value, DateShape, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime deleted)
                ? new Item(named.Groups["id"].Value, deleted, path)
                : null;

        static IReadOnlyList<Item> Newest(IEnumerable<Item> items) =>
            items.Where(i => i != null).OrderByDescending(i => i.Deleted).ThenBy(i => i.Folder, StringComparer.Ordinal).ToList();

        // the campaign moved into Recently deleted: where it went, or null and the disk's reason
        public static string Delete(string campaign, DateTime today, out string problem)
        {
            problem = null;
            string root = Path.GetDirectoryName(Path.GetFullPath(campaign).TrimEnd('/', '\\')) ?? "";
            string id = new DirectoryInfo(campaign).Name;
            string name = ContentId.Free(Stamp(id, today), taken => Directory.Exists(Path.Combine(In(root), taken)));

            try
            {
                Directory.CreateDirectory(In(root));
                string target = Path.Combine(In(root), name);
                Directory.Move(campaign, target);
                return target;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                problem = could.Message;
                return null;
            }
        }

        // what's in it, the newest deleted first
        public static IReadOnlyList<Item> Items(string root) =>
            Directory.Exists(In(root))
                ? Newest(Directory.EnumerateDirectories(In(root)).Select(folder => Parse(folder, Path.GetFileName(folder))))
                : Array.Empty<Item>();

        // --- one file: a map ------------------------------------------------------------------------------------------

        // the file moved into its folder's Recently deleted: where it went, or null and the disk's reason
        public static string DeleteFile(string file, DateTime today, out string problem)
        {
            problem = null;
            string root = Path.GetDirectoryName(Path.GetFullPath(file)) ?? "";
            string extension = Path.GetExtension(file);
            string name = ContentId.Free(Stamp(Path.GetFileNameWithoutExtension(file), today),
                                         taken => File.Exists(Path.Combine(In(root), taken + extension)));

            try
            {
                Directory.CreateDirectory(In(root));
                string target = Path.Combine(In(root), name + extension);
                File.Move(file, target);
                return target;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                problem = could.Message;
                return null;
            }
        }

        // the files of one kind in a folder's Recently deleted, the newest deleted first
        public static IReadOnlyList<Item> Files(string root, string extension) =>
            Directory.Exists(In(root))
                ? Newest(Directory.EnumerateFiles(In(root), "*" + extension).Select(file => Parse(file, Path.GetFileNameWithoutExtension(file))))
                : Array.Empty<Item>();

        // back in its folder under its own id, or the next free one when a file of that id is there now: the id it has,
        // or null and why not
        public static string RestoreFile(Item item, string root, out string problem)
        {
            problem = null;
            string extension = Path.GetExtension(item.Folder);
            string id = ContentId.Free(item.Id, one => File.Exists(Path.Combine(root, one + extension)));

            try
            {
                File.Move(item.Folder, Path.Combine(root, id + extension));
                return id;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                problem = could.Message;
                return null;
            }
        }

        public static int ExpireFiles(string root, string extension, DateTime today) =>
            Files(root, extension).Where(i => i.Expired(today)).Count(i => Forget(i) == null);

        // RESTORE: back among My campaigns under its own id, or the next free one (respelled, CampaignRename) when a
        // campaign of that id is there now or in any root before it (`taken`). Where it is now, or null and why not
        public static string Restore(Item item, string root, Func<string, bool> taken, out string problem)
        {
            problem = null;
            string id = ContentId.Free(item.Id, one => taken(one) || Directory.Exists(Path.Combine(root, one)));
            string target = Path.Combine(root, id);

            try
            {
                Directory.Move(item.Folder, target);

                if (id != item.Id) CampaignRename.Respell(target, item.Id, id);

                return target;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                problem = could.Message;
                return null;
            }
        }

        // DELETE FOREVER: asked first, on the hub (and the only other way one goes is the thirty days)
        public static string Forget(Item item)
        {
            try
            {
                if (File.Exists(item.Folder)) File.Delete(item.Folder);
                else Directory.Delete(item.Folder, true);

                return null;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                return could.Message;
            }
        }

        // ANYTHING OLDER THAN THIRTY DAYS goes, when the hub opens: how many went
        public static int Expire(string root, DateTime today) =>
            Items(root).Where(i => i.Expired(today)).Count(i => Forget(i) == null);
    }
}
