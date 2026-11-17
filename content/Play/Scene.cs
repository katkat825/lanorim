namespace Content.Play
{
    // what the table shows next while a campaign plays
    public enum Scene
    {
        // a line of dialogue; Continue moves on
        Line,

        // options to pick from
        Choice,

        // a fight to run - the combat screen plays it and hands back the outcome
        Fight,

        // a merchant's counter - the shop screen, and back
        Shop,

        // the arena's roster (cc_task_cg Part 6): pick a challenger, or go back to the story
        Roster,

        // the F1 help card, opened at a section by a story's <<tip>> (cc_task_nt Part 4): closing it goes on
        Tip,

        // the hero died: the death screen, and reload
        Dead,

        // the campaign's story has run out: the end screen
        Over,
    }
}
