using System;
using System.Collections.Generic;
using System.Linq;
using Content.Items;
using Core.Tables;

namespace Content.Inventory
{
    // what a loot roll came to once it met the pack: the gold banked, what went in, and what is
    // waiting on the player to make room. it holds the overflows and does not resolve them - that
    // is the player's choice, one at a time, through the same Overflow a full pack always uses.
    public sealed class Haul
    {
        public Haul(int gold, IReadOnlyList<Found> taken, IReadOnlyList<Overflow> waiting,
                    IReadOnlyList<string> unknown)
        {
            Gold = gold;
            Taken = taken ?? Array.Empty<Found>();
            Waiting = waiting ?? Array.Empty<Overflow>();
            Unknown = unknown ?? Array.Empty<string>();
        }

        public int Gold { get; }

        public IReadOnlyList<Found> Taken { get; }

        // one per item that did not fit, in the order it was found. the player cannot continue
        // until each is finished (inventory_decisions.md)
        public IReadOnlyList<Overflow> Waiting { get; }

        public bool MustMakeRoom => Waiting.Count > 0;

        // ids the shelf has never heard of. a package refuses those at load, so this is only ever
        // a hand-built table - and it is said here rather than dropped without a word
        public IReadOnlyList<string> Unknown { get; }

        public override string ToString() =>
            $"{Gold} gold, {Taken.Count} taken" +
            (MustMakeRoom ? $", {Waiting.Count} waiting for room" : "") +
            (Unknown.Count > 0 ? $", {Unknown.Count} unknown" : "");
    }

    // LOOT, FOR ONE HERO. The table is rolled on the GmScreen like any other; this is the part that
    // knows about items and packs, which core does not.
    //
    // THE PER-CLASS RULE: an entry that would give the hero an item they cannot use - wrong class,
    // or under the item's minimum level, the same Item.UsableBy the merchant's Showing asks - is
    // WEIGHTED OUT: it is taken off the table before the pick, and the other entries keep their
    // weights against each other. A find with several items goes if any one of them is unusable;
    // a table entry goes if every entry of the table it rolls would. "nothing" never goes.
    //
    // Why out and not swapped: the merchant does not offer a substitute for what it won't show, it
    // just doesn't show it, and a swap would mean inventing an item the author never put on the
    // table. Why the whole find: a find is one thing the author wrote - "a holy symbol and 20 gold"
    // is a cleric's cache, and the gold alone is not what they meant a fighter to find.
    public static class Spoils
    {
        public static Func<string, bool> UsableBy(ItemShelf shelf, string className, int level)
        {
            if (shelf == null) throw new ArgumentNullException(nameof(shelf));

            return id => shelf.Find(id)?.UsableBy(className, level) ?? false;
        }

        public static LootRoll Open(this GmScreen screen, LootTable table, LootTables tables,
                                    ItemShelf shelf, string className, int level)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));

            return screen.Open(table, tables, UsableBy(shelf, className, level));
        }

        // gold goes straight in - it costs no slot, and currency is gold-only. each item goes in
        // whole if it fits and waits whole if it does not: a stack half-taken would leave the
        // player refusing "the new item" when some of it is already in the pack
        public static Haul Hand(Pack pack, LootRoll roll, ItemShelf shelf)
        {
            if (pack == null) throw new ArgumentNullException(nameof(pack));
            if (shelf == null) throw new ArgumentNullException(nameof(shelf));

            if (roll == null) return new Haul(0, null, null, null);

            pack.Earn(roll.Gold);

            var taken = new List<Found>();
            var waiting = new List<Overflow>();
            var unknown = new List<string>();

            foreach (Found found in roll.Found)
            {
                Item item = shelf.Find(found.Item);

                if (item == null)
                {
                    unknown.Add(found.Item);
                    continue;
                }

                // anything already waiting is ahead in the queue, so a later find does not
                // squeeze into room the player has not yet decided how to make - unless it needs
                // no room at all: a quest item, or more on a stack already carried
                bool free = pack.SlotsNeededFor(item, found.Count) == 0;

                if (free || (waiting.Count == 0 && pack.Fits(item, found.Count)))
                {
                    pack.Take(item, found.Count);
                    taken.Add(found);
                }
                else
                {
                    waiting.Add(new Overflow(pack, item, found.Count));
                }
            }

            return new Haul(roll.Gold, taken, waiting, unknown);
        }
    }
}
