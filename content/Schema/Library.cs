using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Items;
using Content.Monsters;
using Content.Species;
using Content.Spells;

namespace Content.Schema
{
    // everything the SRD ships, read once. the game holds one of these; a campaign adds its own
    // content on top with With, and never edits this.
    public sealed class Library
    {
        Library(SpellBook spells, ItemShelf items,
                IReadOnlyList<CharacterClass> classes, IReadOnlyList<Kind> species,
                IReadOnlyList<Background> backgrounds, Bestiary bestiary,
                Core.Resolution.ConsequencePool consequences, IReadOnlyList<string> problems)
        {
            Spells = spells;
            Items = items;
            Classes = classes;
            Species = species;
            Backgrounds = backgrounds;
            Bestiary = bestiary;
            Consequences = consequences;
            Problems = problems;
        }

        public SpellBook Spells { get; }

        public ItemShelf Items { get; }

        public IReadOnlyList<CharacterClass> Classes { get; }

        public IReadOnlyList<Kind> Species { get; }

        public IReadOnlyList<Background> Backgrounds { get; }

        public Bestiary Bestiary { get; }

        // the nat-1 / nat-20 pool; a campaign adds its own on top with With
        public Core.Resolution.ConsequencePool Consequences { get; }

        public IReadOnlyList<string> Problems { get; }

        public bool Sound => Problems.Count == 0;

        public CharacterClass Class(string id) =>
            Classes.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));

        public Kind Kind(string id) =>
            Species.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

        public Background Background(string id) =>
            Backgrounds.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.Ordinal));

        // the seven the player picks from: a lineage is a second step inside its species, not a
        // thing on the first list
        public IEnumerable<Kind> Playable => Species.Where(s => !s.IsLineage);

        public IEnumerable<Kind> LineagesOf(string speciesId) =>
            Species.Where(s => s.LineageOf == speciesId);

        public IEnumerable<string> Keys() =>
            Spells.Keys()
                  .Concat(Items.Keys())
                  .Concat(Classes.SelectMany(c => c.Keys()))
                  .Concat(Species.SelectMany(s => s.Keys()))
                  .Concat(Backgrounds.SelectMany(b => b.Keys()))
                  .Concat(Bestiary.Keys())
                  .Concat(Consequences.All.SelectMany(c => c.Keys()))
                  .Concat(Inventory.Merchant.Keys())
                  .Distinct();

        static Library _srd;

        public static Library Srd() => _srd ??= Load();

        static Library Load()
        {
            var problems = new List<string>();

            SpellBook spells = SpellBook.Srd();
            problems.AddRange(spells.Problems);

            ItemShelf items = ItemShelf.Srd();
            problems.AddRange(items.Problems);

            Bestiary bestiary = Bestiary.Srd();
            problems.AddRange(bestiary.Problems);

            var consequences = new List<Core.Resolution.Consequence>();

            foreach ((string path, string text) in Schema.Srd.ReadFolder("consequences"))
            {
                ConsequenceReader.TryRead(text, out IReadOnlyList<Core.Resolution.Consequence> read,
                                          out IReadOnlyList<string> trouble);

                consequences.AddRange(read);
                problems.AddRange(trouble.Select(t => $"{path}: {t}"));
            }

            var classes = new List<CharacterClass>();

            foreach ((string path, string text) in Schema.Srd.ReadFolder("classes"))
            {
                ClassReader.TryRead(text, out IReadOnlyList<CharacterClass> read,
                                    out IReadOnlyList<string> trouble);

                classes.AddRange(read);
                problems.AddRange(trouble.Select(t => $"{path}: {t}"));
            }

            var species = new List<Kind>();

            foreach ((string path, string text) in Schema.Srd.ReadFolder("species"))
            {
                SpeciesReader.TryRead(text, out IReadOnlyList<Kind> read,
                                      out IReadOnlyList<string> trouble);

                species.AddRange(read);
                problems.AddRange(trouble.Select(t => $"{path}: {t}"));
            }

            var backgrounds = new List<Background>();

            foreach ((string path, string text) in Schema.Srd.ReadFolder("backgrounds"))
            {
                BackgroundReader.TryRead(text, out IReadOnlyList<Background> read,
                                         out IReadOnlyList<string> trouble);

                backgrounds.AddRange(read);
                problems.AddRange(trouble.Select(t => $"{path}: {t}"));
            }

            // the rosters are fixed by the design docs, so a missing one is a build error rather
            // than a quiet short list in the character creator
            problems.AddRange(Missing(classes.Select(c => c.Id), Roster.Classes, "class"));
            problems.AddRange(Missing(species.Where(s => !s.IsLineage).Select(s => s.Id),
                                      Roster.Species, "species"));

            // every item a class starts with has to exist, or a new character opens with a hole
            foreach (CharacterClass one in classes)
                foreach (string gear in one.StartingGear)
                    if (!items.Has(gear))
                        problems.Add($"{one.Id}: starting gear '{gear}' is not an item");

            // a background's gear has to exist too, for the same reason
            foreach (Background one in backgrounds)
                foreach (string gear in one.Gear)
                    if (!items.Has(gear))
                        problems.Add($"{one.Id}: gear '{gear}' is not an item");

            return new Library(spells, items, classes, species, backgrounds, bestiary,
                               new Core.Resolution.ConsequencePool(consequences), problems);
        }

        static IEnumerable<string> Missing(IEnumerable<string> got, IEnumerable<string> wanted,
                                           string what)
        {
            var have = new HashSet<string>(got, StringComparer.Ordinal);

            return wanted.Where(w => !have.Contains(w))
                         .Select(w => $"the {what} '{w}' is in the design docs but not in the data");
        }
    }

    // the v1 rosters, as the design docs settle them. listed here and nowhere else, so a test can
    // hold the data to them.
    public static class Roster
    {
        // v1_class_roster.md
        public static readonly IReadOnlyList<string> Classes = new[]
        {
            "barbarian", "fighter", "rogue", "mage", "cleric", "paladin", "druid",
        };

        // v1_species_roster.md
        public static readonly IReadOnlyList<string> Species = new[]
        {
            "human", "elf", "dragonborn", "tiefling", "dwarf", "halfling", "orc",
        };
    }
}
