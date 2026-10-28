using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Maps
{
    // WHERE MAPS LIVE ON DISK, for the map builder (cc_task_f Part 2; cc_task_l Part 1): a place's folder of <id>.map
    // files, a campaign's maps/ (the folder and the format Package.ReadMaps reads, so a map the builder saves loads in
    // its campaign with no conversion) or the "My maps" library (MapPlace). A map is named by its file, so its id is
    // the file name's rules (ContentId.IsLocal). Nothing here writes over a map unless it is told to
    public static class MapFiles
    {
        public static string PathOf(MapPlace place, string id) => Path.Combine(place.Folder, id + Package.MapExtension);

        // the maps in a place, by id, in name order
        public static IReadOnlyList<string> Ids(MapPlace place) =>
            PackFolder.Read(place.Folder, Package.MapExtension, Package.MapsFolder, new List<ContentProblem>())
                      .Select(f => Path.GetFileNameWithoutExtension(f.Path))
                      .Where(ContentId.IsLocal)
                      .ToList();

        public static bool Has(MapPlace place, string id) => ContentId.IsLocal(id) && File.Exists(PathOf(place, id));

        public static bool Read(MapPlace place, string id, out MapDraft draft, out string problem)
        {
            draft = null;

            try
            {
                return MapDraft.TryRead(File.ReadAllText(PathOf(place, id)), out draft, out problem);
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                problem = could.Message;
                return false;
            }
        }

        // null when it was written, or why not. the folder is made if the place has none yet. a map already there is
        // written over only when `replace` says so: the screen asks first
        public static string Write(MapPlace place, string id, MapDraft draft, bool replace = true)
        {
            if (!ContentId.IsLocal(id))
                return $"'{id}' is not a map id - lowercase a-z, 0-9 and underscore, as the file is named";

            if (!replace && Has(place, id)) return $"there is already a map called '{id}' there";

            try
            {
                Directory.CreateDirectory(place.Folder);
                File.WriteAllText(PathOf(place, id), draft.Save());
                return null;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                return could.Message;
            }
        }

        // A MAP DELETED goes to its place's Recently deleted for thirty days (Kathleen, 2026-10-27; RecentlyDeleted), as a
        // campaign does: null when it went, or why not
        public static string Delete(MapPlace place, string id, DateTime today)
        {
            if (!Has(place, id)) return $"there is no map called '{id}' there";

            return Workshop.RecentlyDeleted.DeleteFile(PathOf(place, id), today, out string problem) == null ? problem : null;
        }

        // the place's deleted maps, the newest first; one restored (the id it has now, its own or the next free one);
        // and those past their thirty days, gone
        public static IReadOnlyList<Workshop.RecentlyDeleted.Item> Deleted(MapPlace place) =>
            Workshop.RecentlyDeleted.Files(place.Folder, Package.MapExtension);

        public static string Restore(MapPlace place, Workshop.RecentlyDeleted.Item item, out string problem) =>
            Workshop.RecentlyDeleted.RestoreFile(item, place.Folder, out problem);

        public static int Expire(MapPlace place, DateTime today) =>
            Workshop.RecentlyDeleted.ExpireFiles(place.Folder, Package.MapExtension, today);

        // the first id like this one that no map in the place has: mill_yard_2, mill_yard_3. what "rename" offers when
        // a copy's id is taken
        public static string FreeId(MapPlace place, string id) => ContentId.Free(id, taken => Has(place, taken));
    }
}
