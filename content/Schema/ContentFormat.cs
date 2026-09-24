namespace Content.Schema
{
    // Oldest is a promise: raising it abandons every campaign written below it, so move it only
    // when a shape truly cannot be read.
    public static class ContentFormat
    {
        // FORMAT 1, AND LANORIM STARTS HERE. The old build reached format 2 and carried a reader
        // for format 1 beside it, because its format 2 was the World layer arriving mid-project and
        // there were campaigns written before it. None of that history is lanorim's: this is a
        // fresh format for a different rule set, no campaign has ever been written against it, and
        // the compatibility code that would read one does not need to exist yet.
        //
        // The first campaign someone else writes is the moment this stops being free to change.
        public const int Current = 1;

        public const int Oldest = 1;

        public static bool CanRead(int format) => format >= Oldest && format <= Current;

        // two directions that look the same to a version check but need different sentences
        public static string WhyNot(int format) =>
            format > Current
                ? $"this campaign is written for content format {format} and this build reads up " +
                  $"to {Current} - the game is older than the campaign, so update the game"
                : $"this campaign is written for content format {format} and this build no longer " +
                  $"reads anything below {Oldest}";
    }
}
