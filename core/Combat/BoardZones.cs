using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Core.Combat
{
    // THE ZONES ON THE BOARD, in the order they went down, with how long each lasts; and who each has already done its
    // once-a-turn thing to this turn. Was Encounter's private state (cc_task_j-split-classes-dev-hooks.md Part 1),
    // mirroring Incantation's ZoneLedger: this is the books, and what a zone does to whoever it covers is still the
    // fight's (Encounter.Zones.cs)
    internal sealed class BoardZones
    {
        sealed class Placed
        {
            public Placed(IZone zone, Duration duration)
            {
                Zone = zone;
                Duration = duration;
            }

            public IZone Zone { get; }

            public Duration Duration { get; }

            public bool Armed { get; set; }
        }

        readonly List<Placed> _zones = new();

        readonly HashSet<(IZone, Actor, Pulses?)> _pulsedThisTurn = new();

        // every zone, read where it lies: for a question that changes nothing (a route's step, a line of sight)
        public IEnumerable<IZone> Each => _zones.Select(p => p.Zone);

        // every zone, as a copy: safe to end zones from while it is walked
        public List<IZone> Snapshot() => _zones.Select(p => p.Zone).ToList();

        public void Add(IZone zone, Duration duration) => _zones.Add(new Placed(zone, duration));

        // every place this zone lies; false when it lay nowhere
        public bool RemoveAll(IZone zone) => _zones.RemoveAll(p => ReferenceEquals(p.Zone, zone)) > 0;

        // the first place it lies
        public void Remove(IZone zone)
        {
            int at = _zones.FindIndex(p => ReferenceEquals(p.Zone, zone));

            if (at >= 0) _zones.RemoveAt(at);
        }

        // the zones from one source one owner made (a concentration dropped)
        public List<IZone> From(string source, Actor owner) =>
            _zones.Where(p => p.Zone.Source == source && ReferenceEquals(p.Zone.Owner, owner)).Select(p => p.Zone).ToList();

        // the zones a creature carries (an emanation around its owner)
        public IEnumerable<IZone> OwnedBy(Actor owner) =>
            _zones.Where(p => ReferenceEquals(p.Zone.Owner, owner)).Select(p => p.Zone);

        // a zone that lasts until its owner's next turn starts or ends, counted the way boons are: the zones whose
        // time is up now, still on the books (the fight takes each up and says so). one that lasts until the end of
        // the next turn is armed as that turn starts
        public List<IZone> Lapsing(Actor whose, bool starting)
        {
            var ending = new List<IZone>();

            foreach (Placed placed in _zones.Where(p => ReferenceEquals(p.Zone.Owner, whose)).ToList())
            {
                if (starting && placed.Duration == Duration.NextTurn) ending.Add(placed.Zone);
                else if (starting && placed.Duration == Duration.NextTurnEnd) placed.Armed = true;
                else if (!starting && placed.Duration == Duration.NextTurnEnd && placed.Armed) ending.Add(placed.Zone);
            }

            return ending;
        }

        // SRD's "only once per turn": true the first time this zone does this to this creature this turn. a zone
        // that acts EachTime counts each kind of moment once a turn instead
        public bool FirstThisTurn(IZone zone, Actor creature, Pulses pulse) =>
            _pulsedThisTurn.Add((zone, creature, zone.EachTime ? pulse : (Pulses?)null));

        // every turn starts with a clean slate
        public void NewTurn() => _pulsedThisTurn.Clear();
    }
}
