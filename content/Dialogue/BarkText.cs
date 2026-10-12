using System.Text.RegularExpressions;

namespace Content.Dialogue
{
    // A BARK'S EMPHASIS (cc_task_i 2.1): tools/import_barks.py turns Kathleen's **bold** and *italics* into BBCode's
    // [b] and [i], which the companion's bubble shows. the log and the read-aloud get the words without the tags
    public static class BarkText
    {
        static readonly Regex Tags = new Regex(@"\[/?[bi]\]", RegexOptions.Compiled);

        public static string Plain(string line) => line == null ? null : Tags.Replace(line, "");
    }
}
