namespace Core.Tables
{
    // HOW HARD A FIGHT IS MEANT TO BE, as an author declares it (cc_task_h 3.5): the SRD 5.2.1 "Combat Encounter
    // Difficulty" words. The campaign check holds a fight's monsters to the XP Budget per Character for ONE character
    // at this difficulty (FightBudget). Not the player's difficulty setting, and not a check's DC ladder
    public enum EncounterDifficulty
    {
        Low,
        Moderate,
        High,
    }
}
