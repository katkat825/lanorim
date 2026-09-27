namespace Core.Tables
{
    // implement one method, ignore the rest
    public abstract class ScreenObserver : IScreenObserver
    {
        public virtual void Rolled(GmRoll roll) { }

        public virtual void Consulted(TableRoll result) { }

        public virtual void Opened(LootRoll result) { }
    }
}
