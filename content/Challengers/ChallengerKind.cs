namespace Content.Challengers
{
    // WHAT A CHALLENGER TYPE IS TO ITS STAT BLOCKS (cc_task_cg, Kathleen 2026-11-09), from a `kind:` line in her lists
    public enum ChallengerKind
    {
        // the default: the type claims its stat blocks, so they are offered only under its own lists
        Own,

        // a champion of its stat blocks ("Champion (melee)": gladiator or berserker): named challengers of those stat
        // blocks, which leave the plain ones to the shared lists as well
        Champion,
    }
}
