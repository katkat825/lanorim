using System.IO;
using Content.Campaigns;

namespace Content.Maps
{
    // WHERE A MAP LIVES (cc_task_l Part 1.1): the "My maps" library, which belongs to no campaign (user://maps, the
    // player's own space, in the editor and in an exported build), or one campaign's maps/ folder. The same files in
    // the same format either way; MapFiles reads and writes a place without caring which
    public sealed class MapPlace
    {
        MapPlace(string folder, string campaign, string id, string nameKey)
        {
            Folder = folder ?? "";
            Campaign = campaign;
            Id = id;
            NameKey = nameKey;
        }

        // the folder the .map files are in
        public string Folder { get; }

        // the campaign's own folder, or null for the library
        public string Campaign { get; }

        // the campaign's id, or null for the library
        public string Id { get; }

        public bool IsLibrary => Campaign == null;

        // what the title bar calls it: "My maps", or the campaign's name
        public string NameKey { get; }

        // THE CAMPAIGN'S NAME AS ITS PAGE HAS IT (Kathleen's UI notes, 2026-10-27): a campaign being made in the Workshop
        // may not have its words loaded, or may have been renamed and not saved, so its page says it; "" when nobody did
        public string Named { get; private init; } = "";

        public static readonly string LibraryKey = Screens.ScreenKeys.Key("map", "my_maps");

        // the library: its folder is the maps' own
        public static MapPlace Library(string folder) => new(folder, null, null, LibraryKey);

        // a campaign: its maps are in its maps/ folder, the one Package.ReadMaps reads
        public static MapPlace OfCampaign(string campaign, string nameKey = null, string named = "") =>
            new(Path.Combine(campaign ?? "", Package.MapsFolder), campaign,
                Path.GetFileName((campaign ?? "").TrimEnd('/', '\\')), nameKey) { Named = named ?? "" };

        public bool Same(MapPlace other) =>
            other != null && Path.GetFullPath(Folder).TrimEnd('/', '\\') == Path.GetFullPath(other.Folder).TrimEnd('/', '\\');

        // debug only, never localized
        public override string ToString() => IsLibrary ? $"my maps ({Folder})" : $"campaign {Id} ({Folder})";
    }
}
