using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;

namespace Content.Monsters
{
    public sealed class Bestiary : Catalogue<Monster>
    {
        public Bestiary(IEnumerable<Monster> monsters, IEnumerable<string> problems = null)
            : base(monsters, m => m.Id, m => m.Keys(), problems)
        {
        }

        public override IEnumerable<Monster> All =>
            Stock.OrderBy(m => m.Challenge).ThenBy(m => m.Id, StringComparer.Ordinal);

        public IEnumerable<Monster> Around(double challenge, double spread = 1) =>
            All.Where(m => Math.Abs(m.Challenge - challenge) <= spread);

        public Bestiary With(IEnumerable<Monster> more) => new Bestiary(Plus(more), Problems);

        // read once and shared: a catalogue is read-only (Catalogue)
        static readonly Lazy<Bestiary> TheSrd =
            new(() => new Bestiary(ReadSrd("monsters", MonsterReader.TryRead, out List<string> problems), problems));

        public static Bestiary Srd() => TheSrd.Value;

        public override string ToString() => Counted("monsters");
    }
}
