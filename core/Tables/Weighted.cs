using System.Collections.Generic;
using System.Linq;

namespace Core.Tables
{
    // something drawn by weight beside others: an encounter table's entry, a loot table's, a
    // consequence in the nat-1 / nat-20 pool
    public interface IWeighted
    {
        int Weight { get; }
    }

    // THE ONE WEIGHTED DRAW. the encounter table, the loot table and the consequence pool each
    // walked their own copy of it (cc_task_dedupe-leftovers.md #13)
    public static class Weighted
    {
        public static int TotalWeight<T>(this IEnumerable<T> entries) where T : IWeighted =>
            entries.Sum(e => e.Weight);

        // the entry a ticket from 1 to the total lands on
        public static T Walk<T>(this IReadOnlyList<T> entries, int ticket) where T : IWeighted
        {
            foreach (T entry in entries)
            {
                ticket -= entry.Weight;

                if (ticket <= 0) return entry;
            }

            return entries[entries.Count - 1];
        }
    }
}
