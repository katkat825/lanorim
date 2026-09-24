namespace Content.Saves
{
    public static class SaveFormat
    {
        // FORMAT 1, AND LANORIM STARTS HERE. The old build reached 2 and kept a reader for 1 beside
        // it because the World layer arrived mid-project and there were saves written before it.
        // There are no lanorim saves yet, so there is nothing to be compatible with.
        public const int Current = 1;

        public const int Oldest = 1;

        public static bool CanRead(int format) => format >= Oldest && format <= Current;

        // A SAVE IS READ AS FAR AS IT CAN BE, WHICH IS NOT HOW A CAMPAIGN IS READ. A campaign with
        // a fault is refused, because a half-loaded campaign fails in the middle of a fight. A save
        // is somebody's afternoon: reading what is there and saying what was not understood is
        // kinder than refusing it, and the reader collects rather than throws so it can.
        public static string Unfamiliar(int format) =>
            $"this save says it is format {format} and this build writes {Current} - it is read " +
            "as far as it can be, and anything this build does not recognise is listed below";
    }
}
