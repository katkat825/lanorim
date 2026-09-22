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
    }

    public static class Advantages
    {
        public static Advantage Of(bool advantage, bool disadvantage) =>
            advantage == disadvantage ? Advantage.Flat
          : advantage ? Advantage.Advantage
          : Advantage.Disadvantage;

        // combining two existing states obeys the same rule: two advantages are one advantage
        public static Advantage And(this Advantage a, Advantage b) =>
            a == b ? a
          : a == Advantage.Flat ? b
          : b == Advantage.Flat ? a
          : Advantage.Flat;

        public static Advantage With(this Advantage a, bool advantage, bool disadvantage) =>
            a.And(Of(advantage, disadvantage));

        public static int Dice(this Advantage a) => a == Advantage.Flat ? 1 : 2;

        public static string Id(this Advantage a) => a switch
        {
            Advantage.Advantage => "advantage",
            Advantage.Disadvantage => "disadvantage",
            _ => "flat",
        };
    }
}
