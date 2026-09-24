using Godot;

namespace Game.Campaigns
{
    // WHAT THE COMMAND LINE ASKED FOR. Every headless check that wants a particular campaign, a
    // particular map or a particular hero says so here, so a check script is a flag rather than a
    // scene edited and put back.
    //
    // LIFTED from the old build. What went with the World layer is --place and its format 1
    // spelling --encounter; a chapter walks maps now, so --map is the noun. --grown named the
    // trait-die steps a test hero had climbed and has nothing to mean under SRD - a level does that
    // job, so --level replaces it.
    public static class Requested
    {
        public const string CampaignArg = "--campaign=";

        public const string ChapterArg = "--chapter=";

        public const string MapArg = "--map=";

        public const string HeroArg = "--hero=";

        public const string LevelArg = "--level=";

        public static string Campaign => Value(CampaignArg);

        public static string Chapter => Value(ChapterArg);

        public static string Map => Value(MapArg);

        public static string Hero => Value(HeroArg);

        // 0 when nothing asked, which reads as "whatever the campaign starts you at"
        public static int Level =>
            int.TryParse(Value(LevelArg), out int level) && level > 0 ? level : 0;

        public static bool Any =>
            Campaign.Length > 0 || Chapter.Length > 0 || Map.Length > 0 || Hero.Length > 0;

        static string Value(string flag)
        {
            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg.StartsWith(flag, System.StringComparison.Ordinal))
                    return arg.Substring(flag.Length).Trim();

            return "";
        }
    }
}
