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
                Core.Resolution.ConsequencePool consequences, FormShelf forms,
                IReadOnlyList<string> problems)
        {
            Spells = spells;
            Items = items;
            Classes = classes;
            Species = species;
            Backgrounds = backgrounds;
            Bestiary = bestiary;
            Consequences = consequences;
            Forms = forms;
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

        // the Druid's Wild Shape cards. SRD-only: a campaign adding a sixth would be the
        // beast-catalogue converter v1_class_roster.md rules out
        public FormShelf Forms { get; }

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
                  .Concat(Forms.Keys())
                  .Concat(Inventory.Merchant.Keys())
                  .Distinct();

        // A CAMPAIGN'S CONTENT, LAID ON TOP OF THE SRD'S. The SRD library is never edited: the game
        // holds one, and starting a campaign makes a SECOND that has both - so unloading a campaign
        // is dropping a reference, not undoing a merge, and two campaigns can never leak into each
        // other through a library somebody mutated.
        //
        // Ids in a pack are scoped to the campaign, so a pack's "goblin" cannot shadow the SRD's.
        // A pack that is not sound adds nothing: Shelf has already said why, and half its content
        // is worse than none of it.
        // IDS ARE NOT SCOPED HERE YET, AND A COLLISION IS REFUSED RATHER THAN RESOLVED. ContentId
        // knows how to spell "this campaign's goblin" and nothing calls it on the way in, because
        // rescoping content means rebuilding a Monster, an Item and a Spell with a new id, and what
        // a scoped id looks like at the table is part of finalizing the pack format - Phase 7.
        //
        // Until then a campaign may not redefine something the SRD already ships. That is a real
        // limit, and the point of doing it this way is that it is a SENTENCE rather than a silent
        // shadowing: whoever hits it is told which id, and Phase 7 is where it stops being true.
        public Library With(Campaigns.Package pack)
        {
            if (pack == null || !pack.Sound) return this;

            var problems = new List<string>(Problems);

            var monsters = Keep(pack.Monsters, m => m.Id, Bestiary.Has, "monster", pack.Id, problems);
            var items = Keep(pack.Items, i => i.Id, Items.Has, "item", pack.Id, problems);
            var spells = Keep(pack.Spells, s => s.Id, Spells.Has, "spell", pack.Id, problems);

            return new Library(Spells.With(spells), Items.With(items),
                               Classes, Species, Backgrounds,
                               Bestiary.With(monsters), Consequences, Forms, problems);
        }

        static List<T> Keep<T>(IEnumerable<T> offered, Func<T, string> idOf,
                               Func<string, bool> alreadyThere, string what, string campaign,
                               List<string> problems)
        {
            var kept = new List<T>();

            foreach (T one in offered)
            {
                string id = idOf(one);

                if (alreadyThere(id))
                {
                    problems.Add($"{campaign}: the {what} '{id}' is already one the SRD ships, and " +
                                 "a campaign cannot redefine it yet - rename it");
                    continue;
                }

                kept.Add(one);
            }

            return kept;
        }

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

            FormShelf forms = FormShelf.Srd();
            problems.AddRange(forms.Problems);

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

            // a Shape feature's count is how many cards it offers, and the cards are the data -
            // so the two are held to each other rather than trusted to agree
            foreach (CharacterClass one in classes)
                foreach (Feature shape in one.Features.Where(f => f.Trait == Trait.Shape))
                    if (shape.Count != forms.Count)
                        problems.Add($"{one.Id}/{shape.Id}: count {shape.Count} but " +
                                     $"{forms.Count} form cards ship");

            return new Library(spells, items, classes, species, backgrounds, bestiary,
                               new Core.Resolution.ConsequencePool(consequences), forms,
                               problems);
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
