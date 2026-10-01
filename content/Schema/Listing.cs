using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Content.Schema
{
    // A CATALOGUE IN ITS FILE'S ORDER: the classes, species and backgrounds, found by id like every other
    // catalogue (they were lists searched one by one, cc_task_d-seams-and-duplication.md §7) and listed in the
    // order the SRD file has them, which is the order the creator offers them in. still a list, so whatever
    // counted or walked Library.Classes does as it did
    public sealed class Listing<T> : Catalogue<T>, IReadOnlyList<T> where T : class
    {
        readonly List<T> _order;

        public Listing(IEnumerable<T> things, Func<T, string> id, Func<T, IEnumerable<string>> keys,
                       IEnumerable<string> problems = null)
            : base(things, id, keys, problems)
        {
            // what the catalogue kept, in the order it came: a second of one id is a problem, not a copy
            var kept = new HashSet<T>(Stock, ReferenceEqualityComparer.Instance);

            _order = (things ?? Enumerable.Empty<T>()).Where(t => t != null && kept.Remove(t)).ToList();
        }

        public override IEnumerable<T> All => _order;

        public T this[int index] => _order[index];

        public IEnumerator<T> GetEnumerator() => _order.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => Counted(typeof(T).Name);
    }
}
