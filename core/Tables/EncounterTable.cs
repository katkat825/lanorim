using System;
using System.Collections.Generic;
using Core.Dice;
using Core.Words;

namespace Core.Tables
{
    // whether the player sees the numbers. the GM's default is behind the screen: the player hears
    // the dice land and learns only what the table decided (decisions_checklist.md section 3)
    public enum Visibility
    {
        // behind the screen, and heard: the screen rattles (cc_task_i Part 1)
        Hidden,

        Shown,

        // behind the screen and not heard: a table the author doesn't want the player to notice
        Quiet,
    }

    public static class Visibilities
    {
        // a roll the player doesn't see the numbers of, heard or not
        public static bool IsBehind(this Visibility visibility) => visibility != Visibility.Shown;
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
        public Band(string monster, DiceRoll count, bool leader = false)
        {
            Monster = monster ?? "";
            Count = count.IsNothing ? DiceRoll.Flat(1) : count;
            Leader = leader;
        }

        // the leader whose fall breaks a "morale": "leader" fight (cc_task_h 3.4): the first of this band
        public bool Leader { get; }

        public string Monster { get; }

        public DiceRoll Count { get; }

        public override string ToString() => $"{Count} {Monster}";
    }

    public sealed class EncounterEntry : ITableEntry
    {
        public EncounterEntry(string id, EntryKind kind, int weight = 1,
                              IReadOnlyList<Band> monsters = null, string map = null,
                              string loot = null, Combat.Morale morale = Combat.Morale.None,
                              int level = 0, EncounterDifficulty difficulty = EncounterDifficulty.Moderate,
                              Combat.Defeat defeat = Combat.Defeat.Unsaid)
        {
            Morale = morale;
            Defeat = defeat;
            Level = level;
            Difficulty = difficulty;
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

        // when its enemies break (cc_task_h 3.4): none, leader or half. the campaign's, per fight
        public Combat.Morale Morale { get; }

        // what losing it means, when the fight says (cc_task_i 3.1); Unsaid is the campaign's
        public Combat.Defeat Defeat { get; }

        // the level the author meant it for, 0 when unsaid, and how hard (cc_task_h 3.5): what the campaign check holds
        // its monsters to (FightBudget). never read in play
        public int Level { get; }

        public EncounterDifficulty Difficulty { get; }

        public bool Speaks => Kind != EntryKind.Nothing;

        public override string ToString() =>
            $"{Id} x{Weight} {EnumWords.Name(Kind)}" +
            (Monsters.Count > 0 ? ": " + string.Join(", ", Monsters) : "") +
            (Map.Length > 0 ? $" on {Map}" : "") +
            (Loot.Length > 0 ? $", leaves {Loot}" : "");
    }

    // A RANDOM-ENCOUNTER TABLE: a chance that anything happens, then a weighted pick of what. It is
    // data and nothing else - no hooks, no script, and it never runs itself. The GmScreen rolls it.
    public sealed class EncounterTable : GmTable<EncounterEntry>
    {
        public EncounterTable(string id, Trigger trigger, IEnumerable<EncounterEntry> entries,
                              Visibility visibility = Visibility.Hidden)
            : base(id, entries, visibility, "line")
        {
            Trigger = trigger;
        }

        public Trigger Trigger { get; }

        public override string ToString() => $"{Id}: {Trigger}, {Entries.Count} entries" + RolledInTheOpen;
    }
}
