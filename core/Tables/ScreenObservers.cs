namespace Core.Tables
{
    // several at once (ObserverList): a broken dice sound must not lose the player an encounter
    public sealed class ScreenObservers : ObserverList<IScreenObserver>, IScreenObserver
    {
        public ScreenObservers(params IScreenObserver[] watchers) : base(watchers)
        {
        }

        public void Rolled(GmRoll roll) => Each(w => w.Rolled(roll));

        public void Consulted(TableRoll result) => Each(w => w.Consulted(result));

        public void Opened(LootRoll result) => Each(w => w.Opened(result));
    }
}
