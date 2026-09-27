namespace Core.Tables
{
    // how many of one item turned up, after the count was rolled
    public sealed class Found
    {
        public Found(string item, int count)
        {
            Item = item ?? "";
            Count = count;
        }

        public string Item { get; }

        public int Count { get; }

        public override string ToString() => $"{Count} {Item}";
    }
}
