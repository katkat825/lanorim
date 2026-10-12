using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Words;

namespace Core.Tables
{
    public enum LootKind
    {
        // the pockets were empty. an entry of its own, so an author sets the odds of it
        Nothing,

        // items, coins, or both
        Find,

        // roll another table - the "roll on the gem table" of every printed hoard
        Table,
    }

    // one kind of item in a find, and how many - "1d4 torches" is a count of 1d4. the item is an
    // id and nothing more; whether it exists is the package's question, the same as a Band's monster
    public sealed class Lot
    {
        public Lot(string item, DiceRoll count)
        {
            Item = item ?? "";
            Count = count.IsNothing ? DiceRoll.Flat(1) : count;
        }

        public string Item { get; }

        public DiceRoll Count { get; }

        public override string ToString() => $"{Count} {Item}";
    }

    // coins of one kind, as dice and a multiplier. DiceRoll has no "*10" and should not grow one for this -
    // "3d6 x 10" is the only place the SRD multiplies a roll, and it is always a purse.
    //
    // what it comes to is in copper (cc_task_k): Worth is what one of its coins is in copper, 100 for gold, 10 for
    // silver, 1 for copper. core does not know a gold piece from a copper one - the reader that wrote the purse says
    // (Content.Inventory.Coins) - so a hand-built purse with no worth given is copper
    public readonly struct Purse
    {
        public Purse(DiceRoll dice, int times = 1, int worth = 1)
        {
            Dice = dice;
            Times = times < 1 ? 1 : times;
            Worth = worth < 1 ? 1 : worth;
        }

        public static readonly Purse Empty = new Purse(DiceRoll.None);

        public DiceRoll Dice { get; }

        public int Times { get; }

        // one coin, in copper
        public int Worth { get; }

        public bool IsEmpty => Dice.IsNothing;

        // in copper, as Of is
        public int Minimum => Math.Max(0, Dice.Minimum) * Times * Worth;

        public int Maximum => Math.Max(0, Dice.Maximum) * Times * Worth;

        // what a rolled total comes to, in copper. never below nothing - a purse can be empty, not in debt
        public int Of(int rolled) => Math.Max(0, rolled) * Times * Worth;

        public override string ToString() =>
            IsEmpty ? "no coin" : (Times == 1 ? $"{Dice}" : $"{Dice}x{Times}") + $" of {Worth} cp";
    }

    public sealed class LootEntry : ITableEntry
    {
        public LootEntry(string id, LootKind kind, int weight = 1, IReadOnlyList<Lot> items = null,
                         IReadOnlyList<Purse> coins = null, string table = null, bool speaks = false)
        {
            Id = id ?? "";
            Kind = kind;
            Weight = weight < 1 ? 1 : weight;
            Items = items ?? Array.Empty<Lot>();
            Coins = (coins ?? Array.Empty<Purse>()).Where(p => !p.IsEmpty).ToList();
            Table = table ?? "";
            Speaks = speaks;
        }

        public string Id { get; }

        public LootKind Kind { get; }

        // relative chance of being picked against the rest of its table
        public int Weight { get; }

        public IReadOnlyList<Lot> Items { get; }

        // the gold, silver and copper in it, a purse each, as the author wrote them - each rolled on its own
        public IReadOnlyList<Purse> Coins { get; }

        // the table this entry rolls, or empty
        public string Table { get; }

        // whether the narrator has a line for it. unlike an encounter, a find mostly speaks for
        // itself - the item card is the news - so a line is the author's to ask for
        public bool Speaks { get; }

        public override string ToString() =>
            $"{Id} x{Weight} {EnumWords.Name(Kind)}" +
            (Items.Count > 0 ? ": " + string.Join(", ", Items) : "") +
            string.Concat(Coins.Select(c => $" + {c}")) +
            (Table.Length > 0 ? $" -> {Table}" : "");
    }

    // A LOOT TABLE: a weighted pick of what was found. It is data and nothing else, the same as an
    // EncounterTable - the GmScreen rolls it, and what the hero ends up carrying is the caller's.
    //
    // the narrator's line for a find sits beside the encounter lines rather than in a namespace of
    // its own: it is the same voice behind the same screen, and the "loot" aspect keeps a loot table
    // and an encounter table of one name from ever sharing a key
    public sealed class LootTable : GmTable<LootEntry>
    {
        public LootTable(string id, IEnumerable<LootEntry> entries,
                         Visibility visibility = Visibility.Hidden)
            : base(id, entries, visibility, "loot")
        {
        }

        // the tables this one rolls on, for the cycle check and the package's "is there such a table"
        public IEnumerable<string> Rolls =>
            Entries.Where(e => e.Kind == LootKind.Table).Select(e => e.Table).Distinct();

        public override string ToString() => $"{Id}: {Entries.Count} entries" + RolledInTheOpen;
    }

    // every loot table a campaign ships, by id, so a table entry can find the one it names
    public sealed class LootTables
    {
        readonly Dictionary<string, LootTable> _byId =
            new Dictionary<string, LootTable>(StringComparer.Ordinal);

        public LootTables(IEnumerable<LootTable> tables)
        {
            foreach (LootTable table in tables ?? Enumerable.Empty<LootTable>())
                if (table != null)
                    _byId[table.Id] = table;
        }

        public static readonly LootTables None = new LootTables(null);

        public int Count => _byId.Count;

        public IEnumerable<LootTable> All => _byId.Values.OrderBy(t => t.Id, StringComparer.Ordinal);

        public LootTable Find(string id) =>
            id != null && _byId.TryGetValue(id, out LootTable table) ? table : null;

        public bool Has(string id) => Find(id) != null;

        // A TABLE THAT ROLLS ITSELF, however far round, is a chest that never stops opening. The
        // readers refuse one; this finds it, as the ids in order with the first repeated at the
        // end, or empty. a table this set does not have is a dead end, not a loop.
        public IReadOnlyList<string> Cycle()
        {
            var done = new HashSet<string>(StringComparer.Ordinal);

            foreach (LootTable table in All)
            {
                var path = new List<string>();

                if (Walk(table.Id, path, done)) return path;
            }

            return Array.Empty<string>();
        }

        bool Walk(string id, List<string> path, HashSet<string> done)
        {
            int at = path.IndexOf(id);

            if (at >= 0)
            {
                path.Add(id);
                path.RemoveRange(0, at);
                return true;
            }

            LootTable table = Find(id);

            if (table == null || done.Contains(id)) return false;

            path.Add(id);

            foreach (string next in table.Rolls)
                if (Walk(next, path, done)) return true;

            path.RemoveAt(path.Count - 1);
            done.Add(id);

            return false;
        }

        public override string ToString() => $"{Count} loot tables";
    }
}
