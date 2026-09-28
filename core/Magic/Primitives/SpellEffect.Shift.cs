namespace Core.Magic
{
    // the settings only a shift reads. ShiftHandler declares their keys
    public sealed partial class SpellEffect
    {
        // pushed this many squares straight away from the caster, stopped by walls and bodies -
        // Thunderwave's 10 feet
        public int Push { get; init; }

        // a shift of the caster that is magical travel: a creature inside a Forcecage has to make a
        // Charisma save first
        public bool Teleports { get; init; }

        // a teleport that brings one willing creature from beside the caster along: Dimension Door
        public bool Passenger { get; init; }

        // a teleport to anywhere in range, seen or not, that fails for 4d6 force on an occupied
        // square: Dimension Door
        public bool Unseen { get; init; }

        // a repeating shift of the zone rolls it square by square and stops at the first creature
        // in its way, which it rams (the zone's "ram" pulse) - Flaming Sphere
        public bool Rams { get; init; }
    }
}
