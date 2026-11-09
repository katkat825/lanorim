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
                              int descriptions, string map = null)
        {
            Id = id ?? "";
            Shape = shape;
            Monsters = monsters ?? Array.Empty<Band>();
            Names = Math.Max(0, names);
            Descriptions = Math.Max(0, descriptions);
            Map = map ?? "";
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

        // A SHARED LIST (cc_task_cg, Kathleen's "Generic"): names and descriptions with no monsters, for any stat block.
        // A single's shared list serves every single; a team's serves every group and team
        public bool Shared => Monsters.Count == 0;

        public bool Serves(ChallengerType other) =>
            Shared && other != null && !other.Shared &&
            Shape == (other.Shape == ChallengerShape.Single ? ChallengerShape.Single : ChallengerShape.Team);

        // every name a challenger of this type can have: its own, then each shared list's that serves it
        public IReadOnlyList<string> NameKeys(IEnumerable<ChallengerType> all) =>
            Enumerable.Range(1, Names).Select(NameKey)
                      .Concat((all ?? Enumerable.Empty<ChallengerType>()).Where(t => t.Serves(this))
                                  .SelectMany(t => Enumerable.Range(1, t.Names).Select(t.NameKey)))
                      .ToList();

        public IReadOnlyList<string> DescriptionKeys(IEnumerable<ChallengerType> all) =>
            Enumerable.Range(1, Descriptions).Select(DescriptionKey)
                      .Concat((all ?? Enumerable.Empty<ChallengerType>()).Where(t => t.Serves(this))
                                  .SelectMany(t => Enumerable.Range(1, t.Descriptions).Select(t.DescriptionKey)))
                      .ToList();

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
