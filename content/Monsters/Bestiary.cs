using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Monsters
{
    public sealed class Bestiary
    {
        readonly Dictionary<string, Monster> _byId;

        public Bestiary(IEnumerable<Monster> monsters, IEnumerable<string> problems = null)
        {
            _byId = new Dictionary<string, Monster>(StringComparer.Ordinal);

            foreach (Monster monster in monsters ?? Enumerable.Empty<Monster>())
                if (monster != null)
                    _byId[monster.Id] = monster;

            Problems = (problems ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> Problems { get; }

        public bool Sound => Problems.Count == 0;

        public int Count => _byId.Count;

        public IEnumerable<Monster> All =>
            _byId.Values.OrderBy(m => m.Challenge).ThenBy(m => m.Id, StringComparer.Ordinal);

        public Monster Find(string id) =>
            id != null && _byId.TryGetValue(id, out Monster monster) ? monster : null;

        public bool Has(string id) => Find(id) != null;

        public IEnumerable<Monster> Around(double challenge, double spread = 1) =>
            All.Where(m => Math.Abs(m.Challenge - challenge) <= spread);

        public IEnumerable<Monster> Tagged(string tag) =>
            All.Where(m => m.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

        public IEnumerable<string> Keys() => All.SelectMany(m => m.Keys());

        public Bestiary With(IEnumerable<Monster> more) =>
            new Bestiary(_byId.Values.Concat(more ?? Enumerable.Empty<Monster>()), Problems);

        public static Bestiary Srd()
        {
            var monsters = new List<Monster>();
            var problems = new List<string>();

            foreach ((string path, string text) in Schema.Srd.ReadFolder("monsters"))
            {
                MonsterReader.TryRead(text, out IReadOnlyList<Monster> read,
                                      out IReadOnlyList<string> trouble);

                monsters.AddRange(read);
                problems.AddRange(trouble.Select(t => $"{path}: {t}"));
            }

            return new Bestiary(monsters, problems);
        }

        public override string ToString() =>
            $"{Count} monsters" + (Problems.Count > 0 ? $", {Problems.Count} problems" : "");
    }
}
