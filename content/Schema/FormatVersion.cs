namespace Content.Schema
{
    // A FILE FORMAT'S RANGE: the oldest format this build still reads and the one it writes. a
    // campaign's (ContentFormat) and a save's (SaveFormat) each wrote the check out
    // (cc_task_dedupe-methods.md #9); what each says when it can't read one stays its own
    public readonly struct FormatVersion
    {
        public FormatVersion(int oldest, int current)
        {
            Oldest = oldest;
            Current = current;
        }

        public int Oldest { get; }

        public int Current { get; }

        public bool CanRead(int format) => format >= Oldest && format <= Current;
    }
}
