using Core.Words;

namespace Content.Classes
{
    // what a rest gives back of a limited feature (SRD 5.2.1). the words are the rest words a
    // duration and an item's 'vanishes' use: "long_rest", where a recharge said "long"
    // (cc_task_dedupe-leftovers.md #13)
    public enum Recharge
    {
        // every use on a short or a long rest
        [Word("short_rest")] Short,

        // one use on a short rest, every use on a long rest: Rage, Second Wind, Channel Divinity,
        // Wild Shape
        [Word("one_per_short_rest")] ShortOne,

        // only a long rest: Lay on Hands, Indomitable
        [Word("long_rest")] Long,
    }
}
