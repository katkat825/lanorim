using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Core.Magic;

namespace Content.Spells
{
    // every spell the game can cast, by id. built once from the SRD files and handed round
    // read-only; a campaign's own spells are added on top with With.
    public sealed class SpellBook
    {
        readonly Dictionary<string, Spell> _byId;

        public SpellBook(IEnumerable<Spell> spells, IEnumerable<string> problems = null)
        {
            _byId = new Dictionary<string, Spell>(StringComparer.Ordinal);

            foreach (Spell spell in spells ?? Enumerable.Empty<Spell>())
                if (spell != null)
                    _byId[spell.Id] = spell;

            Problems = (problems ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> Problems { get; }

        public bool Sound => Problems.Count == 0;

        public int Count => _byId.Count;

        public IEnumerable<Spell> All => _byId.Values.OrderBy(s => s.Level)
                                                     .ThenBy(s => s.Id, StringComparer.Ordinal);

        public Spell Find(string id) =>
            id != null && _byId.TryGetValue(id, out Spell spell) ? spell : null;

        public bool Has(string id) => Find(id) != null;

        public IEnumerable<Spell> AtLevel(int level) => All.Where(s => s.Level == level);

        public IEnumerable<Spell> For(string className) =>
            All.Where(s => s.Classes.Contains(className, StringComparer.OrdinalIgnoreCase));

        public IEnumerable<Spell> Approximations => All.Where(s => s.Approximated);

        public SpellBook With(IEnumerable<Spell> more) =>
            new SpellBook(_byId.Values.Concat(more ?? Enumerable.Empty<Spell>()), Problems);

        public IEnumerable<string> Keys() => All.SelectMany(s => s.Keys());

        // the whole SRD spell set, read out of the assembly
        public static SpellBook Srd()
        {
            var spells = new List<Spell>();
            var problems = new List<string>();

            foreach ((string path, string text) in Schema.Srd.ReadFolder("spells"))
            {
                SpellReader.TryRead(text, out IReadOnlyList<Spell> read,
                                    out IReadOnlyList<string> trouble);

                spells.AddRange(read);
                problems.AddRange(trouble.Select(t => $"{path}: {t}"));
            }

            return new SpellBook(spells, problems);
        }

        public override string ToString() =>
            $"{Count} spells" +
            (Problems.Count > 0 ? $", {Problems.Count} problems" : "") +
            $", {All.Count(s => s.IsCantrip)} cantrips, " +
            $"{All.Count(s => s.Approximated)} approximations";
    }
}
