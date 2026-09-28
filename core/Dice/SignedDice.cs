namespace Core.Dice
{
    // dice that are added or taken off: Enlarge's +1d4 and Reduce's -1d4 on a weapon hit. a
    // DiceRoll can't be negative - its count clamps at 0 and "-1d4" doesn't parse - so the sign is
    // this one flag beside it, and a data file still writes it "-1d4"
    public readonly record struct SignedDice(DiceRoll Dice, bool Less)
    {
        public static readonly SignedDice None = new SignedDice(DiceRoll.None, false);

        public bool IsNothing => Dice.IsNothing;

        public SignedDice Doubled() => new SignedDice(Dice.Doubled(), Less);

        public static bool TryParse(string text, out SignedDice dice, out string problem)
        {
            string s = (text ?? "").Trim();
            bool less = s.StartsWith("-");

            if (less || s.StartsWith("+")) s = s.Substring(1);

            bool read = DiceRoll.TryParse(s, out DiceRoll roll, out problem);

            dice = read ? new SignedDice(roll, less) : None;
            return read;
        }

        public override string ToString() => IsNothing ? "0" : (Less ? "-" : "+") + Dice;
    }
}
