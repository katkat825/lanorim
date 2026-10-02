using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Maps
{
    // WHERE A CAMPAIGN'S MAPS LIVE ON DISK, for the map builder (cc_task_f, Part 2): <campaign>/maps/<id>.map, the
    // folder and the format Package.ReadMaps reads, so a map the builder saves loads in its campaign with no
    // conversion. A map is named by its file, so its id is the file name's rules (ContentId.IsLocal)
    public static class MapFiles
    {
        public static string Folder(string campaign) => Path.Combine(campaign ?? "", Package.MapsFolder);

        public static string PathOf(string campaign, string id) => Path.Combine(Folder(campaign), id + Package.MapExtension);

        // the maps in a campaign, by id, in name order
        public static IReadOnlyList<string> Ids(string campaign) =>
            PackFolder.Read(Folder(campaign), Package.MapExtension, Package.MapsFolder, new List<ContentProblem>())
                      .Select(f => Path.GetFileNameWithoutExtension(f.Path))
                      .Where(ContentId.IsLocal)
                      .ToList();

        public static bool Read(string campaign, string id, out MapDraft draft, out string problem)
        {
            draft = null;

            try
            {
                return MapDraft.TryRead(File.ReadAllText(PathOf(campaign, id)), out draft, out problem);
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                problem = could.Message;
                return false;
            }
        }

        // null when it was written, or why not. the folder is made if the campaign has none yet
        public static string Write(string campaign, string id, MapDraft draft)
        {
            if (!ContentId.IsLocal(id))
                return $"'{id}' is not a map id - lowercase a-z, 0-9 and underscore, as the file is named";

            try
            {
                Directory.CreateDirectory(Folder(campaign));
                File.WriteAllText(PathOf(campaign, id), draft.Save());
                return null;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                return could.Message;
            }
        }
    }
}
