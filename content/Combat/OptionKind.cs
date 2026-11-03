namespace Content.Combat
{
    public enum OptionKind
    {
        Attack,
        Spell,
        Again,
        Dash,
        Disengage,
        Hide,
        StandUp,
        BreakFree,
        Shake,
        Item,
        Feature,
        Shape,
        Surge,
        Flee,
        EndTurn,
        Grapple,
        Shove,

        // a shut door beside the hero (Encounter.Doors.cs): free once a turn, then an action
        OpenDoor,

        // give the bout up (cc_task_i 3.2): a fight that continues after defeat, with no way off the map
        Yield,

        // a weapon from the pack taken in hand (an action), and a held spell let go (free): the character sheet's Swap and
        // End concentration, which press these same options (cc_task_cs Part 1)
        Swap,
        EndConcentration,
    }
}
