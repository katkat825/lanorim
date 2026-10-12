namespace Content.Campaigns
{
    // HOW A CAMPAIGN'S OWN POOL SITS ON THE GAME'S (cc_task_i 4.3): its consequences added to the game-wide pool, or
    // in place of it
    public enum Layering
    {
        Add,

        Replace,
    }
}
