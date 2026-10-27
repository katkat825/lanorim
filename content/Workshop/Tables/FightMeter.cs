using System;
using System.Collections.Generic;
using System.Linq;
using Content.Encounters;
using Content.Monsters;
using Content.Screens;
using Content.Schema;
using Core.Dice;
using Core.Tables;
using Core.Words;

namespace Content.Workshop.Tables
{
    public enum MeterColour
    {
        Green,
        Amber,
        Red,
    }

    // THE BUDGET METER (cc_task_n 1.5): a fight's monsters' XP, at the most their dice can come to, against the SRD's
    // budget for one hero of its level and difficulty, and its enemies against the solo cap - FightBudget's own sums, so
    // the meter is green exactly when the campaign check has nothing to say about it. Amber is over the XP budget by up
    // to half again; red is over by more, or over the cap. It never stops a save (FightBudget only warns)
    public sealed record FightMeter(int Xp, int Budget, int Enemies, int Cap, int Level, EncounterDifficulty Difficulty,
                                    IReadOnlyList<ContentProblem> Warnings)
    {
        public bool OverBudget => Xp > Budget;

        public bool OverCap => Enemies > Cap;

        public MeterColour Colour =>
            OverCap || Xp * 2 > Budget * 3 ? MeterColour.Red : OverBudget ? MeterColour.Amber : MeterColour.Green;

        // the one plain sentence under the bar
        public Said Sentence =>
            OverCap ? new Said(TableWords.MeterCapKey, Enemies, Level)
            : Colour == MeterColour.Red ? new Said(TableWords.MeterFarOverKey, TableWords.Difficulty(Difficulty), Level, Xp, Budget)
            : OverBudget ? new Said(TableWords.MeterOverKey, TableWords.Difficulty(Difficulty), Level, Xp, Budget)
            : new Said(TableWords.MeterRightKey, TableWords.Difficulty(Difficulty), Level);

        // the fight's entry as the game reads it, for FightBudget
        public static EncounterEntry Entry(EncounterDraft fight)
        {
            EnumWords.TryName(fight.Difficulty.Length == 0 ? "moderate" : fight.Difficulty, out EncounterDifficulty difficulty);

            return new EncounterEntry(fight.Id, EntryKind.Fight, 1,
                fight.Monsters.Select(m => new Band(m.Monster, Count(m.Count), m.Leader)).ToList(),
                fight.Map, fight.Loot, level: Math.Max(1, fight.Level), difficulty: difficulty);
        }

        static DiceRoll Count(string count) =>
            count.Length > 0 && DiceRoll.TryParse(count, out DiceRoll dice, out _) && !dice.IsNothing ? dice : DiceRoll.Flat(1);

        public static FightMeter Of(EncounterDraft fight, Func<string, Monster> find)
        {
            EncounterEntry entry = Entry(fight);

            (int xp, int enemies) = FightBudget.Tally(entry, find);

            return new FightMeter(xp, FightBudget.BudgetFor(entry.Level, entry.Difficulty), enemies,
                                  FightBudget.Cap(entry.Level),
                                  entry.Level, entry.Difficulty,
                                  FightBudget.Check(entry, find, "", fight.Id).ToList());
        }
    }

    // HOW OFTEN SOMETHING HAPPENS (cc_task_n Part 2): Always (no trigger), Most of the time (1d20, 4 or more),
    // Sometimes (11 or more), Rarely (16 or more), and anything else the author wrote by hand is Other (its numbers
    // in Advanced)
    public enum HowOften
    {
        Always,
        Most,
        Sometimes,
        Rarely,
        Other,
    }

    public static class Oftenness
    {
        static readonly (HowOften How, int AtLeast)[] OnD20 = { (HowOften.Most, 4), (HowOften.Sometimes, 11), (HowOften.Rarely, 16) };

        public static HowOften Of(TriggerDraft trigger)
        {
            if (trigger == null) return HowOften.Always;

            if (!DiceRoll.TryParse(trigger.Roll, out DiceRoll dice, out _) || dice.ToString() != "1d20") return HowOften.Other;

            foreach ((HowOften how, int atLeast) in OnD20)
                if (trigger.AtLeast == atLeast) return how;

            return HowOften.Other;
        }

        // the trigger a choice writes; Other keeps what's there
        public static TriggerDraft Trigger(HowOften how, TriggerDraft now) => how switch
        {
            HowOften.Always => null,
            HowOften.Other => now ?? new TriggerDraft { Roll = "1d20", AtLeast = 11 },
            _ => new TriggerDraft { Roll = "1d20", AtLeast = OnD20.First(o => o.How == how).AtLeast, Kept = now?.Kept ?? new() },
        };
    }
}
