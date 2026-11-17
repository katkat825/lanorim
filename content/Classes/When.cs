namespace Content.Classes
{
    // which part of the turn an ActionGrant adds to. the base two actions are solo compensation,
    // not a stand-in for Extra Attack, so a whole extra action every round is allowed - it is
    // exactly what Extra Attack ports as (v1_class_roster.md, corrected 2026-09-23). the one limit
    // is ActionBudget's guardrail on how many a single turn can hold.
    // when a Rider fires
    public enum When
    {
        Always,

        // you had advantage on the attack - the Rogue's setup, expressed as a rule rather than a
        // second "is anybody next to them" system
        WithAdvantage,

        // the attack roll was not at disadvantage: a flat roll, advantage, or both cancelled. Lanorim's solo
        // Sneak Attack (cc_task_sa, Kathleen 2026-11-16): the SRD's "an ally next to the target" can't
        // happen with no party, so this stands in for it
        UnlessDisadvantage,

        OnCritical,

        // you spent the resource to make it happen. Divine Smite
        WhenSpent,
    }
}
