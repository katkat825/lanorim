namespace Content.Sheet
{
    // SRD 5.2.1's starting-equipment choice, made once for the class and once for the background: the gear (with
    // its little gold), or the gold instead (cc_task_e-shop-species-and-ui-notes.md 1.4). gold means the hero shops
    // before the campaign begins (CampaignRun.Start)
    public enum KitChoice
    {
        Gear,

        Gold,
    }
}
