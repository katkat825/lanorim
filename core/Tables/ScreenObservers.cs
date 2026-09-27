using System;
using System.Collections.Generic;

namespace Core.Tables
{
    // several at once, in the order they were added, and one that throws doesn't take the roll
    // down with it - a broken dice sound must not lose the player an encounter
    public sealed class ScreenObservers : IScreenObserver
    {
        readonly List<IScreenObserver> _watchers = new List<IScreenObserver>();

        readonly List<Exception> _failures = new List<Exception>();

        public ScreenObservers(params IScreenObserver[] watchers)
        {
            foreach (IScreenObserver watcher in watchers ?? Array.Empty<IScreenObserver>())
                Add(watcher);
        }

        public void Add(IScreenObserver watcher)
        {
            if (watcher != null) _watchers.Add(watcher);
        }

        public IReadOnlyList<Exception> Failures => _failures;

        void Each(Action<IScreenObserver> tell)
        {
            foreach (IScreenObserver watcher in _watchers)
            {
                try
                {
                    tell(watcher);
                }
                catch (Exception problem)
                {
                    _failures.Add(problem);
                }
            }
        }

        public void Rolled(GmRoll roll) => Each(w => w.Rolled(roll));

        public void Consulted(TableRoll result) => Each(w => w.Consulted(result));

        public void Opened(LootRoll result) => Each(w => w.Opened(result));
    }
}
