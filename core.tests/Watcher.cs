using System.Collections.Generic;
using Core.Characters;
using Core.Combat;
using Core.Words;

namespace Core.Tests
{
    // answers anything of one trigger and remembers what it was shown
    sealed class Watcher : IReaction
    {
        public Watcher(Trigger trigger, bool stops = false)
        {
            Trigger = trigger;
            _stops = stops;
        }

        readonly bool _stops;

        public List<Moment> Seen { get; } = new List<Moment>();

        public string Id => "watcher_" + Trigger.Id();

        public Trigger Trigger { get; }

        public int Deflects => 0;

        public bool CanAnswer(Encounter fight, Actor reactor, Moment moment) => true;

        public void Answer(Encounter fight, Actor reactor, Moment moment)
        {
            Seen.Add(moment);

            if (_stops) moment.Stop();
        }
    }
}
