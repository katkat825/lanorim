namespace Content.Creation
{
    // the character creator, as logic. guided, in the order the sheet reads
    // (decisions_checklist.md section 3 settles it as "whatever's easiest"), and every step can be
    // asked what it will accept - so the UI is a list of buttons over this and holds no rules.
    public enum Step
    {
        Class,
        Species,
        Lineage,
        Background,
        Abilities,

        // a character made above level 4 spends its ability score improvements here, one by one.
        // it is a step Next STOPS on while any is unspent: the screen pre-fills each with the
        // class's suggestion (SuggestedImprovement), and the player says yes or changes it - never
        // spent on the player's behalf (decisions_checklist.md section 1, 2026-09-24)
        Improvements,

        Skills,

        // the cantrips and the levelled spells are two pages, each with its own count
        // (cc_task_ui-issues-9-30.md 3.3)
        Cantrips,
        Spells,

        // A STEP `Next` NEVER STOPS ON, and that is not an oversight. The choice is pre-answered
        // with the SRD's own mode, so there is nothing creation has to wait for - a player who
        // never opens this step gets slots, which is the right default. It is a Step so the UI has
        // somewhere to put it; ChoosesResource says whether to show it at all.
        SpellResource,

        // ALSO NEVER STOPPED ON, for the same reason: pre-answered (true neutral), so a player who
        // skips it still has the field the sheet requires. make Next stop here if it should be
        // asked every time - that is a one-line change and the call is Kathleen's
        Alignment,

        Name,
        Done,
    }
}
