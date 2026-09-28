namespace Core.Magic
{
    // WHAT A SPELL LEAVES BEHIND ON A CREATURE, and how it ends: the way out, the save track, what
    // damage does to it, what holds it to a zone. an effect carries one of these; what the engine
    // keeps for each creature it landed on (Casting's Placement) is made from it, and adds only who,
    // when, and how the saves have gone so far. it used to be ~20 fields copied from SpellEffect
    // onto Placement one by one (cc_task_dedupe-effects.md, Phase 4)
    public sealed record LingerSpec
    {
        public static readonly LingerSpec Nothing = new LingerSpec();

        // an action and a check to break free: Web, Maze. null is no way out but the spell ending
        public Escape Escape { get; init; }

        // a save made again to end it: Hold Person, Slow, Searing Smite. null is none
        public RepeatSave RepeatSave { get; init; }

        public OnDamage OnDamage { get; init; }

        // it holds only while its bearer is in the spell's zone. on an afflict, the condition drops
        // when the creature leaves (Web); on a sway that reaches the zone, it is an aura on whoever
        // stands inside (Spirit Guardians, Pass without Trace). one flag where there were two -
        // while_in_zone and while_inside (3a #8)
        public bool WhileInZone { get; init; }

        // it ends the moment its bearer makes an attack roll, deals damage or casts: Invisibility
        public bool EndsOnAct { get; init; }

        // someone within 5 feet may spend an action to end it: Sleep, Hypnotic Pattern
        public bool Shakeable { get; init; }

        // while it holds, the creature Dashes away from the caster at the start of each of its
        // turns by the safest route: Fear
        public bool Flees { get; init; }

        // held so many rounds, a banished creature of another plane doesn't come back: Banishment
        public Gone Gone { get; init; }

        // held this many rounds, the condition stays when the spell ends: Flesh to Stone's minute.
        // the engine only honours it for Petrified
        public int PermanentAfterRounds { get; init; }
    }
}
