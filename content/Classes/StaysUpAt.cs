namespace Content.Classes
{
    // the hit points a death intercept stays up with instead of dropping: Relentless Endurance's 1,
    // Relentless Rage's twice the Barbarian level. one record where a feature used 'flat' for one and
    // 'hp_per_level' for the other (cc_task_dedupe-leftovers.md #4)
    public sealed record StaysUpAt(int HitPoints, int PerLevel)
    {
        public int At(int level) => PerLevel > 0 ? PerLevel * level : System.Math.Max(1, HitPoints);
    }
}
