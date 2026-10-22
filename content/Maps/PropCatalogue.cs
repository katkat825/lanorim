using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Localization;

namespace Content.Maps
{
    // one thing an author can put on a square
    public sealed class PropEntry
    {
        public string Id { get; init; } = "";

        public string Category { get; init; } = "";

        // the asset pack it comes from and the model inside it - what tools/pull-models.ps1 pulls
        public string Pack { get; init; } = "";

        public string Model { get; init; } = "";

        // the board treats it as filling its squares: nobody walks through it, and a building is one of these
        public bool Blocks { get; init; }

        // HOW MANY SQUARES IT COVERS, across and deep, unturned (cc_task_l Part 3.3): a building is 2 x 2 or more. a
        // map file stores a prop's anchor and turn only; its size is the palette's, so a map needn't change for it
        public int Width { get; init; } = 1;

        public int Depth { get; init; } = 1;

        // the squares it covers anchored at `at` (the footprint's top-left) and turned a quarter `turn` times: an odd
        // turn swaps across and deep
        public IReadOnlyList<Core.Space.Cell> Footprint(Core.Space.Cell at, int turn)
        {
            bool swapped = turn % 2 != 0;
            int across = swapped ? Depth : Width, deep = swapped ? Width : Depth;
            var cells = new List<Core.Space.Cell>(across * deep);

            for (int y = 0; y < deep; y++)
                for (int x = 0; x < across; x++)
                    cells.Add(new Core.Space.Cell(at.X + x, at.Y + y));

            return cells;
        }

        public string NameKey => KeyConventions.Key(PropCatalogue.PropNs, Id, "name");
    }

    // THE MAP BUILDER'S PALETTE (Tier 2.10): the props an author can place, by category, from data
    // (srd/props/props.json). The editor lists a category's props; the board draws a prop from its
    // model; a map naming a prop the palette doesn't have still loads, with a caution.
    public sealed class PropCatalogue
    {
        public const string PropNs = KeyConventions.PropNs;

        public const string File = "props/props.json";

        PropCatalogue(IReadOnlyList<string> categories, IReadOnlyList<PropEntry> props)
        {
            Categories = categories;
            Props = props;
        }

        public IReadOnlyList<string> Categories { get; }

        public IReadOnlyList<PropEntry> Props { get; }

        public IEnumerable<PropEntry> In(string category) => Props.Where(p => p.Category == category);

        public PropEntry Find(string id) => Props.FirstOrDefault(p => p.Id == id);

        public bool Has(string id) => Find(id) != null;

        // a prop's footprint by its palette size; one the palette doesn't know covers its own square
        public IReadOnlyList<Core.Space.Cell> Footprint(Prop prop) =>
            prop == null ? Array.Empty<Core.Space.Cell>()
            : Find(prop.Id)?.Footprint(prop.Cell, prop.Turn) ?? new[] { prop.Cell };

        // the palette's props whose name or id has these letters in it, for the builder's search box (cc_task_l Part
        // 4.1): the screen passes each prop's name in the player's language
        public IEnumerable<PropEntry> Matching(string words, Func<PropEntry, string> named, string category = null) =>
            (category == null ? Props : In(category))
                .Where(p => string.IsNullOrWhiteSpace(words) ||
                            p.Id.Contains(words.Trim().Replace(' ', '_'), StringComparison.OrdinalIgnoreCase) ||
                            (named?.Invoke(p) ?? "").Contains(words.Trim(), StringComparison.OrdinalIgnoreCase));

        // BUILDINGS APART FROM PROPS (cc_task_m2 Part 8): the builder's bottom bar has a picker for each. The buildings are
        // L's multi-square buildings, the one category of that name; the props are every other category
        public const string BuildingsCategory = "buildings";

        public IReadOnlyList<string> CategoriesOn(bool buildings) =>
            Categories.Where(c => (c == BuildingsCategory) == buildings).ToList();

        // the picker's matches: on one shelf, in one of its categories (null: the whole shelf)
        public IEnumerable<PropEntry> Matching(string words, Func<PropEntry, string> named, string category, bool buildings) =>
            Matching(words, named, category).Where(p => (p.Category == BuildingsCategory) == buildings);

        public static string CategoryKey(string category) =>
            KeyConventions.Key(KeyConventions.UiNs, "map", "category_" + category);

        public IEnumerable<string> Keys() =>
            Props.Select(p => p.NameKey).Concat(Categories.Select(CategoryKey));

        static PropCatalogue _srd;

        public static PropCatalogue Srd()
        {
            if (_srd != null) return _srd;

            if (!TryRead(Schema.Srd.Read(File), out PropCatalogue catalogue, out IReadOnlyList<string> problems))
                throw new InvalidOperationException("the SRD prop palette does not read: " + string.Join("; ", problems));

            return _srd = catalogue;
        }

        // the biggest a prop can be, a side: a town's biggest building, not a map's worth
        public const int MaxSize = 8;

        // every key a palette entry takes
        public static readonly IReadOnlyList<string> EntryKeys = new[] { "id", "category", "pack", "model", "blocks", "size" };

        public static bool TryRead(string text, out PropCatalogue catalogue, out IReadOnlyList<string> problems)
        {
            var found = new List<string>();
            problems = found;
            catalogue = null;

            if (!Json.TryParse(text, out JsonDocument doc, out string bad))
            {
                found.Add(bad);
                return false;
            }

            using (doc)
            {
                JsonElement root = doc.RootElement;

                IReadOnlyList<string> categories = root.Strings("categories");
                var props = new List<PropEntry>();
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (JsonElement raw in root.Items("props"))
                {
                    string id = raw.Text("id");
                    string category = raw.Text("category");

                    Keyed.OnlyKnown(raw, EntryKeys, id, found);

                    if (!Json.IsId(id)) found.Add($"'{id}' is not a prop id");
                    else if (!seen.Add(id)) found.Add($"'{id}' is in the palette twice");

                    if (!categories.Contains(category))
                        found.Add($"{id}: '{category}' is not one of the palette's categories");

                    IReadOnlyList<int> size = raw.TryGetProperty("size", out JsonElement sized) && sized.ValueKind == JsonValueKind.Array
                        ? sized.EnumerateArray().Select(n => n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 0).ToList()
                        : new[] { 1, 1 };

                    if (size.Count != 2 || size.Any(n => n < 1 || n > MaxSize))
                        found.Add($"{id}: 'size' is [across, deep], each 1 to {MaxSize}");

                    props.Add(new PropEntry
                    {
                        Id = id,
                        Category = category,
                        Pack = raw.Text("pack"),
                        Model = raw.Text("model"),
                        Blocks = raw.Flag("blocks"),
                        Width = size.Count == 2 ? Math.Clamp(size[0], 1, MaxSize) : 1,
                        Depth = size.Count == 2 ? Math.Clamp(size[1], 1, MaxSize) : 1,
                    });
                }

                catalogue = new PropCatalogue(categories, props);
            }

            return found.Count == 0;
        }
    }
}
