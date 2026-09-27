namespace Core.Tables
{
    public enum RollPurpose
    {
        // does anything happen at all
        Trigger,

        // which entry of the table
        Pick,

        // how many of one monster, or of one item
        Count,

        // how much gold was in it
        Gold,

        // anything else a campaign rolls behind the screen
        Aside,
    }
}
