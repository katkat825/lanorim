using System;
using System.Collections.Generic;

namespace Core
{
    // SEVERAL WATCHERS AT ONCE, told in the order they were added. one that throws doesn't stop the
    // others or the thing being watched - a broken narrator must not lose the player's combat, and a
    // broken dice sound must not lose them an encounter; what it threw is kept for a check to read.
    // the fight's Observers and the GM screen's ScreenObservers each had this loop
    // (cc_task_dedupe-methods.md #8)
    public abstract class ObserverList<T> where T : class
    {
        readonly List<T> _watchers = new List<T>();

        readonly List<Exception> _failures = new List<Exception>();

        protected ObserverList(IEnumerable<T> watchers)
        {
            foreach (T watcher in watchers ?? Array.Empty<T>())
                Add(watcher);
        }

        public void Add(T watcher)
        {
            if (watcher != null) _watchers.Add(watcher);
        }

        public IReadOnlyList<Exception> Failures => _failures;

        protected void Each(Action<T> tell)
        {
            foreach (T watcher in _watchers)
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
    }
}
