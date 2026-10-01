using System;
using Core.Words;

namespace Core.Characters
{
    // what a boon forbids its bearer: Slow's reactions, Gaseous Form's attacks and spells, Stinking
    // Cloud's actions, Shocking Grasp's opportunity attacks, Moonbeam's shape-shifting. one set of
    // flags where there were six No... booleans (cc_task_dedupe-effects.md, 3a #10). Slow's "an
    // action or a bonus action" and Haste's extra action are not bans and are not here
    [Flags]
    public enum Forbid
    {
        [Unread] None = 0,

        // no action and no bonus action at all
        Actions = 1 << 0,

        Reactions = 1 << 1,

        Attacks = 1 << 2,

        Casting = 1 << 3,

        OpportunityAttacks = 1 << 4,

        // can't shape-shift: a creature Moonbeam turned back, while it is still in the beam
        Shifting = 1 << 5,

        // can't manipulate objects: Gaseous Form's mist (SRD 5.2.1). The one object a fight has is a door, so a door
        // stays shut to it (Encounter.Doors.cs; 2026-10-03)
        Objects = 1 << 6,
    }
}
