using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Core.Tables
{
    public enum RollPurpose
    {
        // does anything happen at all
        Trigger,

        // which entry of the table
        Pick,

        // how many of one monster, or of one item
        Count,

        // how much gold was in it
        Gold,

        // anything else a campaign rolls behind the screen
        Aside,
    }

    // ONE ROLL THE GM MADE, and whether the player may see it. The table scene plays the dice
    // either way; a hidden one is a sound behind the screen and never a number, so the numbers are
    // here for the log and the sim and it is the presentation's job not to show them.
    public sealed class GmRoll
    {
        public GmRoll(RollPurpose purpose, string dice, IReadOnlyList<int> faces, int total,
                      Visibility visibility, string table = "")
        {
            Purpose = purpose;
            Dice = dice ?? "";
            Faces = faces ?? Array.Empty<int>();
            Total = total;
            Visibility = visibility;
            Table = table ?? "";
        }

        public RollPurpose Purpose { get; }

        // "1d20", or "d7" for a pick across seven weights - engineer's notation, never shown
        public string Dice { get; }

        public IReadOnlyList<int> Faces { get; }

        public int Total { get; }

        public Visibility Visibility { get; }

        public bool IsHidden => Visibility == Visibility.Hidden;

        // the table it was rolled for, or empty for an aside
        public string Table { get; }

        public override string ToString() =>
            $"{(IsHidden ? "behind the screen" : "in the open")}: " +
            $"{Purpose.ToString().ToLowerInvariant()} {Dice} = {Total}" +
            (Table.Length > 0 ? $" ({Table})" : "");
    }

    // how many of one monster turned up, after the count was rolled
    public sealed class Mustered
    {
        public Mustered(string monster, int count)
        {
            Monster = monster ?? "";
            Count = count;
        }

        public string Monster { get; }

        public int Count { get; }

        public override string ToString() => $"{Count} {Monster}";
    }

    // what one consultation of a table came to. it says what happened and decides nothing - whether
    // a fight starts, and where, is the caller's, the same as a Casting leaves the table to the UI.
    public sealed class TableRoll
    {
        public TableRoll(EncounterTable table, bool triggered, GmRoll trigger, GmRoll pick,
                         EncounterEntry entry, IReadOnlyList<Mustered> group,
                         IReadOnlyList<GmRoll> rolls)
        {
            Table = table;
            Triggered = triggered;
            TriggerRoll = trigger;
            PickRoll = pick;
            Entry = entry;
            Group = group ?? Array.Empty<Mustered>();
            Rolls = rolls ?? Array.Empty<GmRoll>();
        }

        public EncounterTable Table { get; }

        public bool Triggered { get; }

        // null when the trigger always fires, because then nothing was rolled for it
        public GmRoll TriggerRoll { get; }

        // null when the trigger did not fire
        public GmRoll PickRoll { get; }

        // null when the trigger did not fire, or the table had nothing in it
        public EncounterEntry Entry { get; }

        public IReadOnlyList<Mustered> Group { get; }

        // every roll made, in order - the trigger, the pick, then one per rolled count
        public IReadOnlyList<GmRoll> Rolls { get; }

        public bool Fights => Entry?.Kind == EntryKind.Fight && Group.Count > 0;

        public string Map => Entry?.Map ?? "";

        // the loot table to open once the fight is won, or empty
        public string Loot => Entry?.Loot ?? "";

        // empty when there is nothing to say
        public string LineKey => Table?.LineKey(Entry) ?? "";

        public override string ToString() =>
            $"{Table?.Id}: " +
            (!Triggered ? "nothing stirs"
             : Entry == null ? "triggered, and the table is empty"
             : $"{Entry.Id}" + (Group.Count > 0 ? " - " + string.Join(", ", Group) : ""));
    }

    // how many of one item turned up, after the count was rolled
    public sealed class Found
    {
        public Found(string item, int count)
        {
            Item = item ?? "";
            Count = count;
        }

        public string Item { get; }

        public int Count { get; }

        public override string ToString() => $"{Count} {Item}";
    }

    // what one opening of a loot table came to, the nested tables' finds included. like a
    // TableRoll it says what happened and decides nothing - putting it in the pack, and making room
    // when the pack is full, is the caller's (content/Inventory/Loot.cs)
    public sealed class LootRoll
    {
        public LootRoll(LootTable table, GmRoll pick, LootEntry entry, IReadOnlyList<Found> found,
                        int gold, LootRoll inner, IReadOnlyList<string> weightedOut,
                        IReadOnlyList<GmRoll> rolls)
        {
            Table = table;
            PickRoll = pick;
            Entry = entry;
            Found = found ?? Array.Empty<Found>();
            Gold = Math.Max(0, gold);
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

        // all the gold, nested tables included
        public int Gold { get; }

        // the table the entry rolled, or null
        public LootRoll Inner { get; }

        // the entries this hero could not have been given, so they were never in the draw
        public IReadOnlyList<string> WeightedOut { get; }

        // every roll made, in order - the pick, the counts, the gold, then the nested table's
        public IReadOnlyList<GmRoll> Rolls { get; }

        public bool IsEmpty => Found.Count == 0 && Gold == 0;

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
                                                        .Concat(Gold > 0 ? new[] { $"{Gold} gold" }
                                                                         : Array.Empty<string>())));
    }

    // the screen tells, it does not draw. the table scene's behind-the-screen dice, the log and the
    // sim are observers, the same as a fight's
    public interface IScreenObserver
    {
        void Rolled(GmRoll roll);

        void Consulted(TableRoll result);

        // a default so a watcher written before loot existed keeps compiling
        void Opened(LootRoll result) { }
    }

    // implement one method, ignore the rest
    public abstract class ScreenObserver : IScreenObserver
    {
        public virtual void Rolled(GmRoll roll) { }

        public virtual void Consulted(TableRoll result) { }

        public virtual void Opened(LootRoll result) { }
    }

    // several at once, in the order they were added, and one that throws doesn't take the roll
    // down with it - a broken dice sound must not lose the player an encounter
    public sealed class ScreenObservers : IScreenObserver
    {
        readonly List<IScreenObserver> _watchers = new List<IScreenObserver>();

        readonly List<Exception> _failures = new List<Exception>();

        public ScreenObservers(params IScreenObserver[] watchers)
        {
            foreach (IScreenObserver watcher in watchers ?? Array.Empty<IScreenObserver>())
                Add(watcher);
        }

        public void Add(IScreenObserver watcher)
        {
            if (watcher != null) _watchers.Add(watcher);
        }

        public IReadOnlyList<Exception> Failures => _failures;

        void Each(Action<IScreenObserver> tell)
        {
            foreach (IScreenObserver watcher in _watchers)
            {
                try
                {
                    tell(watcher);
                }
                catch (Exception problem)
                {
                    _failures.Add(problem);
                }
            }
        }

        public void Rolled(GmRoll roll) => Each(w => w.Rolled(roll));

        public void Consulted(TableRoll result) => Each(w => w.Consulted(result));

        public void Opened(LootRoll result) => Each(w => w.Opened(result));
    }

    // every roll and every result, as debug text. never shown to a player, so it is not localized,
    // and it does show hidden numbers - that is what it is for
    public sealed class ScreenLog : ScreenObserver
    {
        readonly List<string> _lines = new List<string>();

        public IReadOnlyList<string> Lines => _lines;

        public override void Rolled(GmRoll roll) => _lines.Add(roll.ToString());

        public override void Consulted(TableRoll result) => _lines.Add("== " + result);

        public override void Opened(LootRoll result) => _lines.Add("== " + result);

        public override string ToString() => string.Join("\n", _lines);
    }

    // THE GM'S SIDE OF THE TABLE. Every roll the narrator makes goes through here, so there is one
    // place that says whether it was hidden and one surface the presentation listens on.
    public sealed class GmScreen
    {
        readonly IRng _rng;
        readonly ScreenObservers _watchers;

        public GmScreen(IRng rng, params IScreenObserver[] watchers)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _watchers = new ScreenObservers(watchers);
        }

        public void Watch(IScreenObserver watcher) => _watchers.Add(watcher);

        public IReadOnlyList<Exception> Failures => _watchers.Failures;

        // any roll a campaign wants made behind the screen, not only a table's
        public GmRoll Roll(DiceRoll dice, Visibility visibility = Visibility.Hidden,
                           RollPurpose purpose = RollPurpose.Aside, string table = "")
        {
            int total = dice.Roll(_rng, out IReadOnlyList<int> faces);

            return Tell(new GmRoll(purpose, dice.ToString(), faces, total, visibility, table));
        }

        public TableRoll Consult(EncounterTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var rolls = new List<GmRoll>();

            GmRoll trigger = null;

            if (!table.Trigger.IsAlways)
            {
                trigger = Roll(table.Trigger.Dice, table.Visibility, RollPurpose.Trigger, table.Id);
                rolls.Add(trigger);
            }

            if (trigger != null && !table.Trigger.Fires(trigger.Total))
                return Tell(new TableRoll(table, false, trigger, null, null, null, rolls));

            // an empty table is refused at load; one built by hand is a quiet road, not a crash
            if (table.Entries.Count == 0)
                return Tell(new TableRoll(table, true, trigger, null, null, null, rolls));

            GmRoll pick = Pick(table);
            rolls.Add(pick);

            EncounterEntry entry = Entry(table, pick.Total);

            var group = new List<Mustered>();

            if (entry.Kind == EntryKind.Fight)
            {
                foreach (Band band in entry.Monsters)
                {
                    int count = band.Count.Modifier;

                    // a flat count is not rolled, so it makes no sound behind the screen
                    if (band.Count.RollsAnything)
                    {
                        GmRoll counted = Roll(band.Count, table.Visibility, RollPurpose.Count,
                                              table.Id);
                        rolls.Add(counted);
                        count = counted.Total;
                    }

                    // the reader refuses a count that can come to nothing; this holds a table
                    // built by hand to the same, so a fight entry always brings a monster
                    group.Add(new Mustered(band.Monster, Math.Max(1, count)));
                }
            }

            return Tell(new TableRoll(table, true, trigger, pick, entry, group, rolls));
        }

        // one draw across the weights - the same walk the consequence pool does
        GmRoll Pick(EncounterTable table)
        {
            int total = table.TotalWeight;
            int ticket = _rng.Roll(total);

            return Tell(new GmRoll(RollPurpose.Pick, "d" + total, new[] { ticket }, ticket,
                                   table.Visibility, table.Id));
        }

        static EncounterEntry Entry(EncounterTable table, int ticket)
        {
            foreach (EncounterEntry entry in table.Entries)
            {
                ticket -= entry.Weight;

                if (ticket <= 0) return entry;
            }

            return table.Entries[table.Entries.Count - 1];
        }

        // OPENING A LOOT TABLE. usable says which item ids this hero could be given; an entry that
        // would give them something they cannot use is weighted out before the dice are thrown, so
        // the odds are the author's odds across what is left (content/Inventory/Loot.cs has the
        // rule and the reason). tables is where a nested entry finds the table it rolls.
        public LootRoll Open(LootTable table, LootTables tables = null,
                             Func<string, bool> usable = null)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var opening = new HashSet<string>(StringComparer.Ordinal);

            return Tell(Open(table, tables ?? LootTables.None, usable ?? (_ => true), opening));
        }

        LootRoll Open(LootTable table, LootTables tables, Func<string, bool> usable,
                      HashSet<string> opening)
        {
            var rolls = new List<GmRoll>();

            // the reader refuses a table that rolls itself; this holds one built by hand to the
            // same, so a loop comes to an empty chest and not a stack overflow
            if (!opening.Add(table.Id))
                return new LootRoll(table, null, null, null, 0, null, null, rolls);

            var offered = new List<LootEntry>();
            var passed = new List<string>();

            foreach (LootEntry entry in table.Entries)
            {
                if (Offers(entry, tables, usable, opening)) offered.Add(entry);
                else passed.Add(entry.Id);
            }

            // nothing this hero could be given, and no "nothing" entry to land on - the find is
            // empty, and no dice are thrown for a draw that has no tickets
            if (offered.Count == 0)
            {
                opening.Remove(table.Id);
                return new LootRoll(table, null, null, null, 0, null, passed, rolls);
            }

            int weight = offered.Sum(e => e.Weight);
            int ticket = _rng.Roll(weight);

            GmRoll pick = Tell(new GmRoll(RollPurpose.Pick, "d" + weight, new[] { ticket }, ticket,
                                          table.Visibility, table.Id));
            rolls.Add(pick);

            LootEntry chosen = Walk(offered, ticket);

            var found = new List<Found>();
            int gold = 0;
            LootRoll inner = null;

            foreach (Lot lot in chosen.Items)
            {
                int count = lot.Count.Modifier;

                // a flat count is not rolled, so it makes no sound behind the screen
                if (lot.Count.RollsAnything)
                {
                    GmRoll counted = Roll(lot.Count, table.Visibility, RollPurpose.Count, table.Id);
                    rolls.Add(counted);
                    count = counted.Total;
                }

                // the reader refuses a count that can come to none; held here for a hand-built one
                found.Add(new Found(lot.Item, Math.Max(1, count)));
            }

            if (!chosen.Gold.IsEmpty)
            {
                int rolled = chosen.Gold.Dice.Modifier;

                if (chosen.Gold.Dice.RollsAnything)
                {
                    GmRoll counted = Roll(chosen.Gold.Dice, table.Visibility, RollPurpose.Gold,
                                          table.Id);
                    rolls.Add(counted);
                    rolled = counted.Total;
                }

                gold += chosen.Gold.Of(rolled);
            }

            LootTable next = chosen.Kind == LootKind.Table ? tables.Find(chosen.Table) : null;

            if (next != null)
            {
                inner = Open(next, tables, usable, opening);

                found.AddRange(inner.Found);
                gold += inner.Gold;
                rolls.AddRange(inner.Rolls);
            }

            opening.Remove(table.Id);

            return new LootRoll(table, pick, chosen, found, gold, inner, passed, rolls);
        }

        // whether an entry could give this hero anything but what they cannot use. "nothing" always
        // can - it is the author's odds of an empty chest - and a nested table can when any of its
        // own entries can
        static bool Offers(LootEntry entry, LootTables tables, Func<string, bool> usable,
                           HashSet<string> opening)
        {
            switch (entry.Kind)
            {
                case LootKind.Find:
                    return entry.Items.All(lot => usable(lot.Item));

                case LootKind.Table:
                    LootTable next = tables.Find(entry.Table);

                    if (next == null || !opening.Add(next.Id)) return false;

                    bool any = next.Entries.Any(e => Offers(e, tables, usable, opening));

                    opening.Remove(next.Id);
                    return any;

                default:
                    return true;
            }
        }

        static LootEntry Walk(IReadOnlyList<LootEntry> entries, int ticket)
        {
            foreach (LootEntry entry in entries)
            {
                ticket -= entry.Weight;

                if (ticket <= 0) return entry;
            }

            return entries[entries.Count - 1];
        }

        LootRoll Tell(LootRoll result)
        {
            _watchers.Opened(result);
            return result;
        }

        GmRoll Tell(GmRoll roll)
        {
            _watchers.Rolled(roll);
            return roll;
        }

        TableRoll Tell(TableRoll result)
        {
            _watchers.Consulted(result);
            return result;
        }
    }
}
