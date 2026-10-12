using System.Collections.Generic;

namespace Content.Dialogue
{
    // THE V1 BARK POOLS (Kathleen, 2026-10-12: "there's too many bark pools for v1"). The engine's names stay (her
    // rule); the ones she folded play from the pool she writes, and the ones cut from v1 still move the companion's
    // mood but never say a line (docs/deferred.md, "v1 bark pools trimmed").
    public static class BarkPools
    {
        // the pool a moment plays its line from: a natural 1 on a check (Trouble) is a Snag line, a natural 20 on a
        // check or save (Perfect) a Maxed line
        public static Bark Pool(this Bark situation) => situation switch
        {
            Bark.Trouble => Bark.Snag,
            Bark.Perfect => Bark.Maxed,
            _ => situation,
        };

        // the pools that speak in v1, highest first: the order a bark cuts in on another (cc_task_i 2.5)
        static readonly Bark[] Order =
        {
            Bark.Snag, Bark.Maxed, Bark.Danger, Bark.Victory, Bark.Secret, Bark.Camp, Bark.Nearby,
        };

        public static IReadOnlyList<Bark> Spoken => Order;

        public static bool Speaks(this Bark situation) => Priority(situation) > 0;

        // higher cuts in on lower; 0 is a pool that says nothing in v1 (Nerve, Down, Nudge, Overasked)
        public static int Priority(this Bark situation)
        {
            int at = System.Array.IndexOf(Order, situation.Pool());

            return at < 0 ? 0 : Order.Length - at;
        }

        // the ones that come round often enough that the "Some" chatter setting halves them
        public static bool Frequent(this Bark situation) =>
            situation.Pool() is Bark.Victory or Bark.Snag or Bark.Maxed;
    }
}
