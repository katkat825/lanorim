using System;
using System.Collections.Generic;

namespace Core.Dice
{
    // the enum value is the side count, so a cast gives you the faces.
    // the old build's ladder (d4-d12, stepped up and down by traits) is gone with the homebrew:
    // SRD rolls a named die a fixed number of times and adds a modifier. d100 is two d10s at the
    // table but one uniform draw here - the tray decides how to show it.
    public enum Die
    {
        None = 0,
        D4 = 4,
        D6 = 6,
        D8 = 8,
        D10 = 10,
        D12 = 12,
        D20 = 20,
        D100 = 100,
    }

    public static class DieExtensions
    {
        // every die the game owns, in the order a dice set is laid out
        public static readonly IReadOnlyList<Die> All = new[]
        {
            Die.D4, Die.D6, Die.D8, Die.D10, Die.D12, Die.D20, Die.D100,
        };

        public static int Sides(this Die d) => (int)d;

        public static bool IsReal(this Die d) => d != Die.None;

        public static string Label(this Die d) => d == Die.None ? "-" : "d" + (int)d;

        public static int Roll(this Die d, IRng rng)
        {
            if (!d.IsReal()) return 0;
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            return rng.Roll(d.Sides());
        }

        // the average face, doubled, so hit-dice and monster-HP averages stay in integers
        // (SRD's "average" rounds a .5 down: 4.5 on a d8 is 4)
        public static int TwiceAverage(this Die d) => d.IsReal() ? d.Sides() + 1 : 0;

        public static int Average(this Die d) => d.TwiceAverage() / 2;

        public static bool TryParse(string text, out Die die)
        {
            die = Die.None;

            if (string.IsNullOrWhiteSpace(text)) return false;

            string trimmed = text.Trim().ToLowerInvariant();

            if (trimmed.Length < 2 || trimmed[0] != 'd') return false;

            if (!int.TryParse(trimmed.Substring(1), out int sides)) return false;

            foreach (Die known in All)
            {
                if (known.Sides() != sides) continue;

                die = known;
                return true;
            }

            return false;
        }
    }
}
