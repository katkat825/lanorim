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

        // how many coins were in it - gold, silver or copper
        Gold,

        // anything else a campaign rolls behind the screen
        Aside,
    }
}
