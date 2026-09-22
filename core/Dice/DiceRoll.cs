using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Core.Dice
{
    // "2d6+3" - the one notation the whole game writes damage, healing and hit points in.
    // immutable, parsed once at load, so a typo in a spell file is a load error and not a crash
    // mid-fight. a roll is a value type read off the RNG; nothing here keeps state.
    public readonly struct DiceRoll : IEquatable<DiceRoll>
    {
        public int Count { get; }

        public Die Die { get; }

        public int Modifier { get; }

        public DiceRoll(int count, Die die, int modifier = 0)
        {
            Count = count < 0 ? 0 : count;
            Die = die;
            Modifier = modifier;
        }

        public static readonly DiceRoll None = new DiceRoll(0, Die.None);

        public static DiceRoll Flat(int amount) => new DiceRoll(0, Die.None, amount);

        public bool RollsAnything => Count > 0 && Die.IsReal();

        public bool IsNothing => !RollsAnything && Modifier == 0;

        public int Minimum => (RollsAnything ? Count : 0) + Modifier;

        public int Maximum => (RollsAnything ? Count * Die.Sides() : 0) + Modifier;

        // doubled so the half-face average stays exact; SRD rounds the halved result down
        public int Average => (RollsAnything ? Count * Die.TwiceAverage() / 2 : 0) + Modifier;

        public DiceRoll WithModifier(int modifier) => new DiceRoll(Count, Die, modifier);

        public DiceRoll Plus(int extra) => new DiceRoll(Count, Die, Modifier + extra);

        // a critical hit doubles the dice, never the modifier - SRD 5.2.1, and the difference is
        // the whole reason a crit doesn't just multiply the final number
        public DiceRoll Doubled() => new DiceRoll(Count * 2, Die, Modifier);

        public int Roll(IRng rng) => Roll(rng, out _);

        public int Roll(IRng rng, out IReadOnlyList<int> faces)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            if (!RollsAnything)
            {
                faces = Array.Empty<int>();
                return Modifier;
            }

            var rolled = new int[Count];
            int total = Modifier;

            for (int i = 0; i < Count; i++)
            {
                rolled[i] = Die.Roll(rng);
                total += rolled[i];
            }

            faces = rolled;

            // signed on purpose - a big enough negative modifier gives a negative total, and it is
            // the caller that decides whether that floors at zero (damage) or not (a check)
            return total;
        }

        public static bool TryParse(string text, out DiceRoll dice, out string problem)
        {
            dice = None;
            problem = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                problem = "no dice in it - the text was empty";
                return false;
            }

            string s = text.Trim().ToLowerInvariant().Replace(" ", "");

            int modifier = 0;
            int sign = s.LastIndexOf('+');
            int minus = s.LastIndexOf('-');

            // the sign has to sit after the 'd', or "-1d6" would parse its own leading minus as a
            // modifier and silently roll one fewer die
            int split = Math.Max(sign, minus);
            int d = s.IndexOf('d');

            if (split > 0 && split > d)
            {
                string tail = s.Substring(split + 1);

                if (!int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out int m))
                {
                    problem = $"'{tail}' is not a number - the part after the sign is the modifier";
                    return false;
                }

                modifier = s[split] == '-' ? -m : m;
                s = s.Substring(0, split);
            }

            if (s.Length == 0)
            {
                problem = "a modifier on its own - write a flat number without a sign";
                return false;
            }

            // a bare number is a flat amount: "4" is 4 damage, no dice
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int flat))
            {
                dice = Flat(flat + modifier);
                return true;
            }

            d = s.IndexOf('d');

            if (d < 0)
            {
                problem = $"'{text}' has no 'd' in it - dice are written like 2d6+3";
                return false;
            }

            string countText = s.Substring(0, d);
            int count = 1;

            if (countText.Length > 0 &&
                !int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
            {
                problem = $"'{countText}' is not a number of dice";
                return false;
            }

            if (count < 1)
            {
                problem = $"{count} dice - roll at least one, or write a flat number";
                return false;
            }

            if (!DieExtensions.TryParse(s.Substring(d), out Die die))
            {
                problem = $"'{s.Substring(d)}' is not a die this game owns " +
                          "(d4, d6, d8, d10, d12, d20, d100)";
                return false;
            }

            dice = new DiceRoll(count, die, modifier);
            return true;
        }

        public static DiceRoll Parse(string text)
        {
            if (TryParse(text, out DiceRoll dice, out string problem)) return dice;

            throw new FormatException($"'{text}' is not dice: {problem}");
        }

        public bool Equals(DiceRoll other) =>
            Count == other.Count && Die == other.Die && Modifier == other.Modifier;

        public override bool Equals(object obj) => obj is DiceRoll other && Equals(other);

        public override int GetHashCode() =>
            unchecked((Count * 397 ^ (int)Die) * 397 ^ Modifier);

        public static bool operator ==(DiceRoll a, DiceRoll b) => a.Equals(b);

        public static bool operator !=(DiceRoll a, DiceRoll b) => !a.Equals(b);

        // round-trips through TryParse - the reader test pins that, so this is safe to write into
        // a data file by hand
        public override string ToString()
        {
            if (IsNothing) return "0";

            var s = new StringBuilder();

            if (RollsAnything) s.Append(Count).Append(Die.Label());

            if (Modifier > 0) s.Append(RollsAnything ? "+" : "").Append(Modifier);
            else if (Modifier < 0) s.Append(Modifier);

            return s.ToString();
        }
    }
}
