using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Localization;

namespace Core.Tables
{
    // whether the player sees the numbers. the GM's default is behind the screen: the player hears
    // the dice land and learns only what the table decided (decisions_checklist.md section 3)
    public enum Visibility
    {
        Hidden,

        Shown,
    }

    public enum EntryKind
    {
        // the dice said something could happen, and nothing did - which is its own kind of tension
        Nothing,

        // the narrator says one line and play goes on
        Line,

        // a group of monsters, and a fight
        Fight,
    }

    // "roll a d20, on 15 or more something happens". a trigger with no dice always fires, which is
    // the table an author rolls when they have already decided an encounter happens and only want
    // to know which one.
    public readonly struct Trigger
    {
        public Trigger(DiceRoll dice, int atLeast)
        {
            Dice = dice;
            AtLeast = atLeast;
        }

        public static readonly Trigger Always = new Trigger(DiceRoll.None, 0);

        public DiceRoll Dice { get; }

        public int AtLeast { get; }

        public bool IsAlways => !Dice.RollsAnything;

        // the two ways an author writes a trigger that is not really a chance
        public bool CanFire => IsAlways || AtLeast <= Dice.Maximum;

        public bool MustFire => IsAlways || AtLeast <= Dice.Minimum;

        public bool Fires(int rolled) => IsAlways || rolled >= AtLeast;

        public override string ToString() => IsAlways ? "always" : $"{Dice} >= {AtLeast}";
    }

    // one kind of monster in a fight entry, and how many of them - "1d4 goblins" is a count of 1d4
    public sealed class Band
    {
        public Band(string monster, DiceRoll count)
        {
            Monster = monster ?? "";
            Count = count.IsNothing ? DiceRoll.Flat(1) : count;
        }

        public string Monster { get; }

        public DiceRoll Count { get; }

        public override string ToString() => $"{Count} {Monster}";
    }

    public sealed class EncounterEntry
    {
        public EncounterEntry(string id, EntryKind kind, int weight = 1,
                              IReadOnlyList<Band> monsters = null, string map = null,
                              string loot = null)
        {
            Id = id ?? "";
            Kind = kind;
            Weight = weight < 1 ? 1 : weight;
            Monsters = monsters ?? Array.Empty<Band>();
            Map = map ?? "";
            Loot = loot ?? "";
        }

        public string Id { get; }

        public EntryKind Kind { get; }

        // relative chance of being picked against the rest of its table
        public int Weight { get; }

        public IReadOnlyList<Band> Monsters { get; }

        // the map the fight is played on, or empty for wherever the party already is
        public string Map { get; }

        // the loot table the fight leaves behind, or empty. named, not rolled - the fight has to be
        // won first, and whether it was is the caller's to know
        public string Loot { get; }

        public bool Speaks => Kind != EntryKind.Nothing;

        public override string ToString() =>
            $"{Id} x{Weight} {Kind.ToString().ToLowerInvariant()}" +
            (Monsters.Count > 0 ? ": " + string.Join(", ", Monsters) : "") +
            (Map.Length > 0 ? $" on {Map}" : "") +
            (Loot.Length > 0 ? $", leaves {Loot}" : "");
    }

    // A RANDOM-ENCOUNTER TABLE: a chance that anything happens, then a weighted pick of what. It is
    // data and nothing else - no hooks, no script, and it never runs itself. The GmScreen rolls it.
    public sealed class EncounterTable
    {
        public EncounterTable(string id, Trigger trigger, IEnumerable<EncounterEntry> entries,
                              Visibility visibility = Visibility.Hidden)
        {
            Id = id ?? "";
            Trigger = trigger;
            Visibility = visibility;
            Entries = (entries ?? Enumerable.Empty<EncounterEntry>()).Where(e => e != null).ToList();
        }

        public string Id { get; }

        public Trigger Trigger { get; }

        public Visibility Visibility { get; }

        public IReadOnlyList<EncounterEntry> Entries { get; }

        public int TotalWeight => Entries.Sum(e => e.Weight);

        // what the narrator says when an entry comes up. derived from the ids so there is no free
        // text in the table to get wrong, and a "nothing" entry has no line to owe the locale.
        public static string LineKey(string table, string entry) =>
            KeyConventions.Key(KeyConventions.EncounterNs, table, "line", entry);

        public string LineKey(EncounterEntry entry) =>
            entry == null || !entry.Speaks ? "" : LineKey(Id, entry.Id);

        public IEnumerable<string> Keys() =>
            Entries.Where(e => e.Speaks).Select(e => LineKey(Id, e.Id));

        public override string ToString() =>
            $"{Id}: {Trigger}, {Entries.Count} entries" +
            (Visibility == Visibility.Shown ? ", rolled in the open" : "");
    }
}
