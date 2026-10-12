using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Tables
{
    // what one opening of a loot table came to, the nested tables' finds included. like a
    // TableRoll it says what happened and decides nothing - putting it in the pack, and making room
    // when the pack is full, is the caller's (content/Inventory/Loot.cs)
    public sealed class LootRoll
    {
        public LootRoll(LootTable table, GmRoll pick, LootEntry entry, IReadOnlyList<Found> found,
                        int copper, LootRoll inner, IReadOnlyList<string> weightedOut,
                        IReadOnlyList<GmRoll> rolls)
        {
            Table = table;
            PickRoll = pick;
            Entry = entry;
            Found = found ?? Array.Empty<Found>();
            Copper = Math.Max(0, copper);
            Inner = inner;
            WeightedOut = weightedOut ?? Array.Empty<string>();
            Rolls = rolls ?? Array.Empty<GmRoll>();
        }

        public LootTable Table { get; }

        // null when there was nothing left to pick from
        public GmRoll PickRoll { get; }

        public LootEntry Entry { get; }

        // every item found, this table's and the nested ones', in the order they were rolled
        public IReadOnlyList<Found> Found { get; }

        // all the coin, nested tables included, in copper: gold, silver and copper together (cc_task_k)
        public int Copper { get; }

        // the table the entry rolled, or null
        public LootRoll Inner { get; }

        // the entries this hero could not have been given, so they were never in the draw
        public IReadOnlyList<string> WeightedOut { get; }

        // every roll made, in order - the pick, the counts, the gold, then the nested table's
        public IReadOnlyList<GmRoll> Rolls { get; }

        public bool IsEmpty => Found.Count == 0 && Copper == 0;

        // the narrator's lines, outer table first; empty when nobody has anything to say
        public IEnumerable<string> LineKeys
        {
            get
            {
                string mine = Table?.LineKey(Entry) ?? "";

                if (mine.Length > 0) yield return mine;

                if (Inner == null) yield break;

                foreach (string key in Inner.LineKeys) yield return key;
            }
        }

        public override string ToString() =>
            $"{Table?.Id}: " +
            (Entry == null ? "nothing to find"
             : IsEmpty ? $"{Entry.Id}, and nothing in it"
             : $"{Entry.Id} - " + string.Join(", ", Found.Select(f => f.ToString())
                                                        .Concat(Copper > 0 ? new[] { $"{Copper} cp" }
                                                                         : Array.Empty<string>())));
    }
}
