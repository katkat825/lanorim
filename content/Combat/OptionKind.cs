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
    }
}
