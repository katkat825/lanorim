using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Classes;
using Content.Schema;
using Core.Characters;
using Core.Localization;

namespace Content.Species
{
    // one of the seven (v1_species_roster.md). a species is a speed, a couple of ability bumps
    // and a handful of features - the same Feature type the classes use, because a Dwarf's poison
    // resistance and a Mage's fire resistance are the same shape and deserve one implementation.
    public sealed class Kind
    {
        public Kind(string id, int speed = 30,
                    IReadOnlyDictionary<Ability, int> bumps = null,
                    IReadOnlyList<Feature> features = null,
                    IReadOnlyList<string> lineages = null,
                    string lineageOf = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Speed = Math.Max(0, speed);
            Bumps = bumps ?? new Dictionary<Ability, int>();
            Features = features ?? Array.Empty<Feature>();
            Lineages = lineages ?? Array.Empty<string>();
            LineageOf = lineageOf ?? "";
        }

        public string Id { get; }

        public int Speed { get; }

        // SRD 5.2.1 puts ability increases on the background rather than the species; the seven
        // here carry none by default, and the field is kept because a campaign's own species may
        public IReadOnlyDictionary<Ability, int> Bumps { get; }

        public IReadOnlyList<Feature> Features { get; }

        // Elf's Drow / High / Wood, Tiefling's legacies. each is its own Kind with LineageOf set
        public IReadOnlyList<string> Lineages { get; }

        public string LineageOf { get; }

        public bool IsLineage => LineageOf.Length > 0;

        public string NameKey => KeyConventions.SpeciesName(Id);

        public string DescriptionKey => KeyConventions.SpeciesDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;

            foreach (string key in Features.SelectMany(f => f.Keys())) yield return key;
        }

        public void Outfit(Actor actor, int level)
        {
            if (actor == null) return;

            actor.Speed = Speed;

            foreach (KeyValuePair<Ability, int> bump in Bumps)
                actor.Scores.Raise(bump.Key, bump.Value);

            foreach (Feature feature in Features.Where(f => f.Level <= level))
                feature.Grant(actor, level);
        }

        public override string ToString() =>
            $"{Id}, speed {Speed}, {Features.Count} traits" +
            (Lineages.Count > 0 ? $", {Lineages.Count} lineages" : "") +
            (IsLineage ? $" (a {LineageOf})" : "");
    }

    public static class SpeciesReader
    {
        public static bool TryRead(string text, out IReadOnlyList<Kind> species,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<Kind>();
            var trouble = new List<string>();

            species = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                foreach (JsonElement entry in document.RootElement.Items("species"))
                {
                    Kind kind = ReadOne(entry, trouble);

                    if (kind != null) found.Add(kind);
                }

                if (found.Count == 0 && trouble.Count == 0)
                    trouble.Add("no species in it - the file is an object with a 'species' array");
            }

            // a lineage that names a species nobody shipped is a dangling choice in the creator
            var ids = new HashSet<string>(found.Select(k => k.Id), StringComparer.Ordinal);

            foreach (Kind kind in found)
                foreach (string lineage in kind.Lineages)
                    if (!ids.Contains(lineage))
                        trouble.Add($"{kind.Id}: lineage '{lineage}' is not a species in this file");

            return trouble.Count == 0;
        }

        static Kind ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a species id");
                return null;
            }

            var bumps = new Dictionary<Ability, int>();

            if (entry.Has("bumps"))
                foreach (JsonProperty bump in entry.GetProperty("bumps").EnumerateObject())
                {
                    if (Abilities.TryParse(bump.Name, out Ability ability) &&
                        bump.Value.TryGetInt32(out int by))
                        bumps[ability] = by;
                    else
                        problems.Add($"{id}: '{bump.Name}' is not an ability, or its bump is not " +
                                     "a number");
                }

            var features = new List<Feature>();

            foreach (JsonElement raw in entry.Items("features"))
            {
                Feature feature = FeatureReader.ReadOne(raw, id, problems);

                if (feature != null) features.Add(feature);
            }

            return new Kind(id, entry.Number("speed", 30), bumps, features,
                            entry.Strings("lineages"), entry.Text("lineage_of"));
        }
    }
}
