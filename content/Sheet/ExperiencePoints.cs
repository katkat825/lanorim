namespace Content.Sheet
{
    // A HERO'S EXPERIENCE POINTS (cc_task_i 3.4), and the SRD's thresholds they're counted against (Advancement). Only
    // a campaign that levels by XP gives any; a milestone campaign leaves them at 0. Was part of Hero; its own type
    // since cc_task_j Part 1. Going up a level is the hero's (Hero.Earn)
    internal sealed class ExperiencePoints
    {
        public int Total { get; private set; }

        // the XP the level after this one needs, or 0 at level 20
        public static int NextLevelAt(int level) =>
            level >= Advancement.HighestLevel ? 0 : Advancement.XpFor(level + 1);

        // earns them; the level the total now reaches
        public int Add(int xp)
        {
            Total += xp;

            return Advancement.LevelFor(Total);
        }

        // a save putting them back
        public void Remember(int total) => Total = System.Math.Max(0, total);
    }
}
