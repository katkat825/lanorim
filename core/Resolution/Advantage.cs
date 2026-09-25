namespace Core.Resolution
{
    // SRD: advantage and disadvantage don't stack and they cancel. any number of sources on one
    // side is still one re-roll, and one source on each side is a flat roll - so this is a tally
    // of *whether*, not of *how many*.
    public enum Advantage
    {
        Flat = 0,
        Advantage = 1,
        Disadvantage = -1,

        // both have been seen: one die, like Flat - but unlike Flat, another advantage or
        // disadvantage can't tip it, because SRD 5.2.1 says any of each is neither (2026-09-25:
        // combining two already-cancelled values let a third source tip the roll)
        Cancelled = 2,
    }

    public static class Advantages
    {
        public static Advantage Of(bool advantage, bool disadvantage) =>
            advantage && disadvantage ? Advantage.Cancelled
          : advantage ? Advantage.Advantage
          : disadvantage ? Advantage.Disadvantage
          : Advantage.Flat;

        // combining two existing states obeys the same rule: two advantages are one advantage, and
        // one of each - or a state that already had one of each - is neither
        public static Advantage And(this Advantage a, Advantage b) =>
            a == Advantage.Cancelled || b == Advantage.Cancelled ? Advantage.Cancelled
          : a == b ? a
          : a == Advantage.Flat ? b
          : b == Advantage.Flat ? a
          : Advantage.Cancelled;

        // what the die sees: one d20 for flat or cancelled, two otherwise
        public static bool IsFlat(this Advantage a) => a == Advantage.Flat || a == Advantage.Cancelled;

        public static Advantage With(this Advantage a, bool advantage, bool disadvantage) =>
            a.And(Of(advantage, disadvantage));

        public static int Dice(this Advantage a) =>
            a == Advantage.Advantage || a == Advantage.Disadvantage ? 2 : 1;

        public static string Id(this Advantage a) => a switch
        {
            Advantage.Advantage => "advantage",
            Advantage.Disadvantage => "disadvantage",
            _ => "flat",
        };
    }
}
