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

        // the board treats it as filling its square
        public bool Blocks { get; init; }

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

                    if (!Json.IsId(id)) found.Add($"'{id}' is not a prop id");
                    else if (!seen.Add(id)) found.Add($"'{id}' is in the palette twice");

                    if (!categories.Contains(category))
                        found.Add($"{id}: '{category}' is not one of the palette's categories");

                    props.Add(new PropEntry
                    {
                        Id = id,
                        Category = category,
                        Pack = raw.Text("pack"),
                        Model = raw.Text("model"),
                        Blocks = raw.Flag("blocks"),
                    });
                }

                catalogue = new PropCatalogue(categories, props);
            }

            return found.Count == 0;
        }
    }
}
