using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;

namespace Content.Classes
{
    // FEATURES MORE THAN ONE CLASS OR SPECIES HAS, written once (srd/features/shared.json,
    // cc_task_godfiles-dupes-efficiency.md #9): Extra Attack, Weapon Mastery, Expertise, Darkvision.
    // a class or species names one by id with the level it arrives at, and FeatureReader reads the
    // shared entry at that level - one id, one locale name.
    public sealed class SharedFeatures
    {
        public const string File = "features/shared.json";

        readonly Dictionary<string, JsonElement> _raw;

        SharedFeatures(Dictionary<string, JsonElement> raw, IReadOnlyList<string> problems)
        {
            _raw = raw;
            Problems = problems;
        }

        static readonly Lazy<SharedFeatures> TheSrd = new(() => Read(Schema.Srd.Read(File)));

        public static SharedFeatures Srd => TheSrd.Value;

        public static readonly SharedFeatures None = new(new Dictionary<string, JsonElement>(), Array.Empty<string>());

        public IReadOnlyList<string> Problems { get; }

        public IEnumerable<string> Ids => _raw.Keys;

        public bool TryFind(string id, out JsonElement raw) => _raw.TryGetValue(id ?? "", out raw);

        public static SharedFeatures Read(string text)
        {
            var raw = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var problems = new List<string>();

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
                return new SharedFeatures(raw, new[] { $"{File}: {bad}" });

            using (document)
            {
                Keyed.OnlyKnown(document.RootElement, new[] { "features" }, File, problems);

                foreach (JsonElement entry in document.RootElement.Items("features"))
                {
                    string id = entry.Text("id");

                    if (!Json.IsId(id)) problems.Add($"{File}: '{id}' is not a feature id");
                    else if (!entry.Has("trait")) problems.Add($"{File}: {id} has no trait");
                    else if (entry.Has("level"))
                        problems.Add($"{File}: {id} has a level - the class or species that names it says when");
                    else if (!raw.TryAdd(id, entry.Clone())) problems.Add($"{File}: {id} is defined twice");
                }
            }

            return new SharedFeatures(raw, problems);
        }

        // what a class or species writes to name one: its id and the level, nothing else
        public static readonly IReadOnlyList<string> NamingKeys = new[] { "id", "level" };

        public static bool Names(JsonElement raw) => !raw.Has("trait");

        public IEnumerable<string> Unused(IEnumerable<Feature> named) =>
            Ids.Except(named.Select(f => f.Id), StringComparer.Ordinal).OrderBy(i => i, StringComparer.Ordinal);
    }
}
