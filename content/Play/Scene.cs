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

        // the hero died: the death screen, and reload
        Dead,

        // the campaign's story has run out: the end screen
        Over,
    }
}
