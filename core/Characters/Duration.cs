namespace Core.Characters
{
    // how long something that was done to an actor stays done. v1 has no round timer and no
    // concentration save on damage (decisions_checklist.md section 6), so a duration is one of a
    // handful of words. the turn-shaped ones are the only ones the fight ticks, and it ticks them
    // at a turn's two edges rather than counting anything.
    public enum Duration
    {
        Instant,

        // held until the caster ends it or is downed
        Concentration,

        // to the end of the fight
        Encounter,

        // until the next short or long rest
        Rest,

        // until the start of its owner's next turn: Shield
        NextTurn,

        // until the end of its owner's next turn: Vicious Mockery's stumble (the target's). "next"
        // is the next turn to *begin* - a boon put on during its owner's own turn does not end
        // when that turn does
        NextTurnEnd,

        // until the end of the turn it was put on during: Stinking Cloud's poisoned, which comes
        // at the start of a turn and lasts only that turn
        TurnEnd,

        // until the next long rest: SRD's 8-hour and 24-hour spells, which a short rest does not
        // end - Aid, Death Ward, Mage Armor
        LongRest,

        // the same two, counted on the CASTER's turns rather than the bearer's: Ray of Frost's
        // slow "until the start of your next turn", Guiding Bolt's glimmer "until the end of your
        // next turn". these were a separate 'until: caster' setting (cc_task_dedupe-effects.md,
        // 3a #12); whatever carries one keeps the caster as its owner and the plain duration
        CasterNextTurn,

        CasterNextTurnEnd,

        // for good: what a creature is by nature or by a feature it has - a dwarf's poison
        // resistance, Fey Ancestry's advantage, a statblock's immunities. nothing ends it
        // (cc_task_dedupe-leftovers.md, Parts A and E: these used to be the actor's own lists)
        Permanent,
    }

    // the words are the names in snake case: "next_turn_end", "caster_next_turn" (EnumWords.Id)
    public static class Durations
    {
        // the caster's turns count it, not the bearer's
        public static bool OnCastersTurn(this Duration duration) =>
            duration == Duration.CasterNextTurn || duration == Duration.CasterNextTurnEnd;

        // the plain duration, with whose turn it is taken off: what a boon or a placement keeps,
        // beside the owner that says whose turn
        public static Duration Plain(this Duration duration) => duration switch
        {
            Duration.CasterNextTurn => Duration.NextTurn,
            Duration.CasterNextTurnEnd => Duration.NextTurnEnd,
            _ => duration,
        };
    }
}
