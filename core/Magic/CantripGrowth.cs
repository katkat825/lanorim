namespace Core.Magic
{
    // how a cantrip grows at caster levels 5, 11 and 17. one word where there were two flags -
    // cantrip_scaling and cantrip_beams (cc_task_dedupe-effects.md, 3a #4)
    public enum CantripGrowth
    {
        None,

        // its amount gains its dice again at each tier: Fire Bolt's 1d10, 2d10, 3d10, 4d10
        Dice,

        // one more beam at each tier, each its own attack roll: Eldritch Blast
        Beams,
    }
}
