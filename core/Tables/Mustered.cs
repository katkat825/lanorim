namespace Core.Tables
{
    // how many of one monster turned up, after the count was rolled
    public sealed class Mustered
    {
        public Mustered(string monster, int count)
        {
            Monster = monster ?? "";
            Count = count;
        }

        public string Monster { get; }

        public int Count { get; }

        public override string ToString() => $"{Count} {Monster}";
    }
}
