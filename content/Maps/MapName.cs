using System.Linq;
using Content.Campaigns;

namespace Content.Maps
{
    // A MAP'S NAME AND ITS ID ARE TWO THINGS (cc_task_m2 Part 7). The name is what the author types and sees: letters of
    // either case, numbers, spaces, dashes and underscores. The id is made from it the way every id is (ContentId.From:
    // lowercase, a space or a dash an underscore), so a dash never reaches an id or a key, and it is the file's name; it
    // must be free in the place the map lives (My maps, or that campaign's maps/), a number added when it isn't. The
    // name is kept in the map file ("name", format 2); a map with none shows its id. The same rule is the campaign
    // name's in the wizard (CampaignWizard)
    public static class MapName
    {
        // "Only letters, numbers, dashes, spaces and underscores are allowed": said in red under the name box (the key the
        // id's rule always had)
        public static string RuleKey => Screens.MapBuilderView.IdRuleKey;

        public static bool Allowed(string name) =>
            !string.IsNullOrWhiteSpace(name) &&
            name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' or '-' or '_');

        // the id a name makes: "Mill Yard - North" is mill_yard_north
        public static string IdOf(string name) => ContentId.From(name, "map");

        // the id it gets where it will live: its own, or with a number when that one is taken there
        public static string IdIn(MapPlace place, string name) => MapFiles.FreeId(place, IdOf(name));

        // what a map is called on a screen: its name, or its id when it has none (a format 1 map, or one made by hand)
        public static string Shown(string name, string id) => string.IsNullOrWhiteSpace(name) ? id : name;

        // a map's name as its file says it (read by the map's own reader); its id when it has none or doesn't read
        public static string Of(MapPlace place, string id) =>
            MapFiles.Read(place, id, out MapDraft draft, out _) ? Shown(draft.Name, id) : id;

        // a deleted map's name, from its file in Recently deleted
        public static string Of(Workshop.RecentlyDeleted.Item deleted)
        {
            try
            {
                return MapDraft.TryRead(System.IO.File.ReadAllText(deleted.Folder), out MapDraft draft, out _) ? Shown(draft.Name, deleted.Id) : deleted.Id;
            }
            catch (System.Exception could) when (could is System.IO.IOException || could is System.UnauthorizedAccessException)
            {
                return deleted.Id;
            }
        }
    }
}
