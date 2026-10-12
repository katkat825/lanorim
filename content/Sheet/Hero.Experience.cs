namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // --- experience points (cc_task_i 3.4) ----------------------------------------------------------------
        //
        // only a campaign that levels by XP gives any (Leveling.Xp): a won fight's monsters, the story's <<xp n>>.
        // A solo hero takes all of a fight's XP. Kept in the save; a milestone campaign leaves it at 0

        public int Experience { get; private set; }

        // the XP the next level needs, or 0 at level 20
        public int NextLevelAt => Level >= Advancement.HighestLevel ? 0 : Advancement.XpFor(Level + 1);

        // earns XP and goes up every level it reaches; the levels it gained
        public int Earn(int xp)
        {
            if (xp <= 0) return 0;

            Experience += xp;

            int was = Level;
            int reached = Advancement.LevelFor(Experience);

            if (reached > Level) LevelTo(reached);

            return Level - was;
        }

        // a save putting it back
        internal void Remember(int experience) => Experience = System.Math.Max(0, experience);
    }
}
