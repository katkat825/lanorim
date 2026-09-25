namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // DISCARDED ITEMS ARE GONE, and the warning that says so is dismissed for good per campaign
        // character - not per player (inventory_decisions.md). So it lives on the hero and in the save
        public bool DiscardWarningDismissed { get; set; }
    }
}
