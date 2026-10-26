using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;

namespace Content.Maps
{
    // WHAT A NEW MAP STARTS AS (cc_task_l Part 1.3): data, not code. srd/maps/templates.json lists them in the order the
    // builder offers them, and each is an ordinary map file beside it (srd/maps/<id>.map), so Kathleen can swap one by
    // editing a map. A new map is a template fitted to the size asked for (MapDraft.Fitted)
    public static class MapTemplates
    {
        public const string Folder = "maps";

        public const string ListFile = Folder + "/templates.json";

        static IReadOnlyList<string> _ids;

        public static IReadOnlyList<string> Ids => _ids ??= ReadIds();

        static IReadOnlyList<string> ReadIds()
        {
            if (!Json.TryParse(Srd.Read(ListFile), out JsonDocument doc, out string bad))
                throw new InvalidOperationException("the map templates list does not read: " + bad);

            using (doc)
            {
                var problems = new List<string>();
                Keyed.OnlyKnown(doc.RootElement, new[] { "templates" }, ListFile, problems);

                if (problems.Count > 0)
                    throw new InvalidOperationException("the map templates list: " + string.Join("; ", problems));

                return doc.RootElement.Strings("templates").ToList();
            }
        }

        // the one a new map starts as when nobody chose: the room every map used to be
        public const string Standard = "dungeon_room";

        // BLANK TAKES THE SIZE ASKED FOR (cc_task_m3 Part 3): it has no size of its own to offer, so picking it leaves the
        // size boxes alone, and its start is the middle of whatever size that is. Every other template offers its own size
        public const string Blank = "blank";

        public static bool TakesTheSize(string id) => id == Blank;

        public static string NameKey(string id) => Screens.ScreenKeys.Key("map", "template_" + id);

        // the template as drawn, at its own size; null for one that isn't there
        public static MapDraft Read(string id)
        {
            string path = $"{Folder}/{id}.map";

            if (id == null || !Ids.Contains(id) || !Srd.Has(path)) return null;

            return MapDraft.TryRead(Srd.Read(path), out MapDraft draft, out _) ? draft : null;
        }

        // a new map from the template, at this size (3 to MapDraft.MaxSide a side)
        public static MapDraft Make(string id, int columns, int rows) =>
            Read(id)?.Fitted(Math.Clamp(columns, Screens.MapBuilderView.MinSide, MapDraft.MaxSide),
                             Math.Clamp(rows, Screens.MapBuilderView.MinSide, MapDraft.MaxSide), middle: TakesTheSize(id));

        public static IEnumerable<string> Keys() => Ids.Select(NameKey);
    }
}
