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
    // (PackFolder.IsHidden), so a deleted campaign isn't on the book's shelf
    public static class RecentlyDeleted
    {
        public const string Folder = ".deleted";

        public const int KeptFor = 30;

        const string DateShape = "yyyy-MM-dd";

        static readonly Regex Named = new(@"^(?<id>[a-z0-9_]+?)_(?<date>\d{4}-\d{2}-\d{2})(_\d+)?$", RegexOptions.Compiled);

        public sealed record Item(string Id, DateTime Deleted, string Folder)
        {
            // the days it has left before it goes for good
            public int DaysLeft(DateTime today) => Math.Max(0, KeptFor - (int)(today.Date - Deleted.Date).TotalDays);

            public bool Expired(DateTime today) => (today.Date - Deleted.Date).TotalDays > KeptFor;
        }

        public static string In(string root) => Path.Combine(root, Folder);

        // the campaign moved into Recently deleted: where it went, or null and the disk's reason
        public static string Delete(string campaign, DateTime today, out string problem)
        {
            problem = null;
            string root = Path.GetDirectoryName(Path.GetFullPath(campaign).TrimEnd('/', '\\')) ?? "";
            string id = new DirectoryInfo(campaign).Name;
            string stamp = $"{id}_{today.ToString(DateShape, CultureInfo.InvariantCulture)}";
            string name = ContentId.Free(stamp, taken => Directory.Exists(Path.Combine(In(root), taken)));

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
        public static IReadOnlyList<Item> Items(string root)
        {
            var items = new List<Item>();

            if (!Directory.Exists(In(root))) return items;

            foreach (string folder in Directory.EnumerateDirectories(In(root)))
            {
                Match named = Named.Match(Path.GetFileName(folder));

                if (named.Success && DateTime.TryParseExact(named.Groups["date"].Value, DateShape, CultureInfo.InvariantCulture,
                                                            DateTimeStyles.None, out DateTime deleted))
                    items.Add(new Item(named.Groups["id"].Value, deleted, folder));
            }

            return items.OrderByDescending(i => i.Deleted).ThenBy(i => i.Folder, StringComparer.Ordinal).ToList();
        }

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
                Directory.Delete(item.Folder, true);
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
