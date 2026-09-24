using System;

namespace Core
{
    // THE RULES VERSION. A campaign names the oldest build that can play it, so a game older than
    // the campaign is refused outright rather than loading most of it - a missing rule would look
    // right on the shelf and be wrong in the middle of a fight.
    //
    // It is the RULES that are versioned, not the file format. ContentFormat is how a campaign
    // folder is SHAPED; this is what the rules inside it may assume exists.
    public static class EngineVersion
    {
        // lanorim is pre-release and this is its first numbered rules build. It moves when the
        // rules gain something a campaign could name and an older build could not honour.
        public static readonly Version Current = new Version(0, 1);

        public static bool Satisfies(Version wanted) => wanted == null || wanted <= Current;
    }
}
