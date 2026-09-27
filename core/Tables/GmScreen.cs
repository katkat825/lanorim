using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Core.Tables
{
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
