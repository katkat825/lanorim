using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Dialogue
{
    // What a camp conversation is about. Camp is "reactive to what happened that day"
    // (CORE_RULES.md section 11), and this is the closed vocabulary the day is read into - a node
    // marked 'topic: bloodied' is offered on the night after a bad fight and not otherwise.
    //
    // Closed for the usual reason: an open string would be a campaign asking the engine to invent a
    // question it has never been asked. Ordered worst-first; the day picks the first that fits.
    public enum Topic
    {
        // somebody nearly died, and both of you know which throw it was
        Loss,

        Boss,

        Bloodied,

        // a Nerve spent to shrug off a Trouble - the companion has an opinion about being pushed
        Trouble,

        Wounded,

        Untouched,

        // nothing happened. Most nights are this one, and it is the most important bank to write
        Quiet,
    }

    public static class Topics
    {
        // worst first, so a night with several answers gets the one worth talking about
        public static IReadOnlyList<Topic> WorstFirst =>
            Enum.GetValues<Topic>().OrderBy(t => (int)t).ToArray();
    }
}
