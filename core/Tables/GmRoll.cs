using System;
using System.Collections.Generic;
using Core.Words;

namespace Core.Tables
{
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
            $"{EnumWords.Name(Purpose)} {Dice} = {Total}" +
            (Table.Length > 0 ? $" ({Table})" : "");
    }
}
