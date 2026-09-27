namespace Core.Tables
{
    // the screen tells, it does not draw. the table scene's behind-the-screen dice, the log and the
    // sim are observers, the same as a fight's
    public interface IScreenObserver
    {
        void Rolled(GmRoll roll);

        void Consulted(TableRoll result);

        // a default so a watcher written before loot existed keeps compiling
        void Opened(LootRoll result) { }
    }
}
