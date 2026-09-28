using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Core.Magic;

namespace Content.Spells
{
    // every spell the game can cast, by id. built once from the SRD files and handed round
    // read-only; a campaign's own spells are added on top with With.
    public sealed class SpellBook : Catalogue<Spell>
    {
        public SpellBook(IEnumerable<Spell> spells, IEnumerable<string> problems = null)
            : base(spells, s => s.Id, s => s.Keys(), problems)
        {
        }

        public override IEnumerable<Spell> All =>
            Stock.OrderBy(s => s.Level).ThenBy(s => s.Id, StringComparer.Ordinal);

        public IEnumerable<Spell> For(string className) =>
            All.Where(s => s.Classes.Contains(className, StringComparer.OrdinalIgnoreCase));

        public IEnumerable<Spell> Approximations => All.Where(s => s.Approximated);

        // everything that must not wear an SRD name: the approximations, and the two v1 spells
        // that are not in SRD 5.2.1 at all
        public IEnumerable<Spell> Renamed => All.Where(s => s.Renamed);

        public SpellBook With(IEnumerable<Spell> more) => new SpellBook(Plus(more), Problems);

        // the whole SRD spell set, read out of the assembly
        // read once and shared: a catalogue is read-only (Catalogue)
        static readonly Lazy<SpellBook> TheSrd =
            new(() => new SpellBook(ReadSrd("spells", SpellReader.TryRead, out List<string> problems), problems));

        public static SpellBook Srd() => TheSrd.Value;

        public override string ToString() =>
            Counted("spells") +
            $", {All.Count(s => s.IsCantrip)} cantrips, " +
            $"{All.Count(s => s.Approximated)} approximations";
    }
}
