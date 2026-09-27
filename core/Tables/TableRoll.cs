using System;
using System.Collections.Generic;

namespace Core.Tables
{
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
}
