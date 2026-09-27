namespace Content.Combat
{
    public enum SessionPhase
    {
        // the hero's turn, nothing picked
        Choosing,

        // an option picked that needs a target, a square or a facing
        Targeting,

        // it is somebody else's turn (the session plays them; this is seen only mid-play)
        Waiting,

        Over,
    }
}
