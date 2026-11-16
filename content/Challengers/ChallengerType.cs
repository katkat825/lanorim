using System;
using System.Collections.Generic;
using System.Linq;
using Core.Tables;

namespace Content.Challengers
{
    // ONE KIND OF CHALLENGER (cc_task_cg Part 1): a `# heading` in one of Kathleen's `Challengers/*.md`, imported by
    // tools/import_challengers.py into a campaign's challengers/<file>.json. Its monsters are the encounter's bands
    // (Band); its names and descriptions are locale keys, numbered, and any name goes with any description
    public sealed class ChallengerType
    {
        public ChallengerType(string id, ChallengerShape shape, IReadOnlyList<Band> monsters, int names,
                              int descriptions, string map = null, ChallengerKind kind = ChallengerKind.Own)
        {
            Id = id ?? "";
            Shape = shape;
            Monsters = monsters ?? Array.Empty<Band>();
            Names = Math.Max(0, names);
            Descriptions = Math.Max(0, descriptions);
            Map = map ?? "";
            Kind = kind;
        }

        public string Id { get; }

        public ChallengerShape Shape { get; }

        // a single's one monster, a group's bands, or the monsters a team is drawn from (their counts unread)
        public IReadOnlyList<Band> Monsters { get; }

        // how many numbered names and descriptions its locale has: challenger.<id>.name.001 ...
        public int Names { get; }

        public int Descriptions { get; }

        // the map its bouts are fought on, or empty for the run's own (the first map of the campaign, as for a fight
        // entry that names none)
        public string Map { get; }

        // whether it claims its stat blocks (Own, the default) or is a champion of them
        public ChallengerKind Kind { get; }

        // A TYPE CLAIMS ITS STAT BLOCKS: they are offered under its own lists and no shared list's. A champion type
        // doesn't, so a plain gladiator, berserker or mage is still offered under the shared lists (Kathleen, 2026-11-09)
        public bool Claims(string monster) =>
            !Shared && Kind == ChallengerKind.Own && Monsters.Any(b => b.Monster == monster);

        // A SHARED LIST (cc_task_cg, Kathleen's "Generic"): names and descriptions with no monsters, for any stat block.
        // A single's shared list serves every single; a team's serves every group and team
        public bool Shared => Monsters.Count == 0;

        public bool Serves(ChallengerType other) =>
            Shared && other != null && !other.Shared &&
            Shape == (other.Shape == ChallengerShape.Single ? ChallengerShape.Single : ChallengerShape.Team);

        // EVERY NAME A CHALLENGER OF THIS TYPE CAN HAVE: its own list when it has one; a type with none takes the
        // shared list that serves its shape (Kathleen, 2026-11-09: no more mixing the two)
        public IReadOnlyList<string> NameKeys(IEnumerable<ChallengerType> all) =>
            Names > 0
                ? Enumerable.Range(1, Names).Select(NameKey).ToList()
                : Serving(all).SelectMany(t => Enumerable.Range(1, t.Names).Select(t.NameKey)).ToList();

        public IReadOnlyList<string> DescriptionKeys(IEnumerable<ChallengerType> all) =>
            Descriptions > 0
                ? Enumerable.Range(1, Descriptions).Select(DescriptionKey).ToList()
                : Serving(all).SelectMany(t => Enumerable.Range(1, t.Descriptions).Select(t.DescriptionKey)).ToList();

        IEnumerable<ChallengerType> Serving(IEnumerable<ChallengerType> all) =>
            Shared ? new[] { this } : (all ?? Enumerable.Empty<ChallengerType>()).Where(t => t.Serves(this));

        public string NameKey(int number) => Key(Id, "name", number);

        public string DescriptionKey(int number) => Key(Id, "description", number);

        // the key shape the importer writes
        public static string Key(string type, string what, int number) =>
            Core.Localization.KeyConventions.Key(Core.Localization.KeyConventions.ChallengerNs, type, what, number.ToString("000"));

        public IEnumerable<string> Keys() =>
            Enumerable.Range(1, Names).Select(NameKey).Concat(Enumerable.Range(1, Descriptions).Select(DescriptionKey));

        public override string ToString() =>
            $"{Id}: {Shape.ToString().ToLowerInvariant()} of {string.Join(", ", Monsters)}, {Names} names, {Descriptions} descriptions";
    }
}
