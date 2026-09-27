using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): zones
    public sealed partial class Encounter
    {
        // --- zones ---------------------------------------------------------------------------------

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

        // who a zone has already done its once-a-turn thing to this turn
        readonly HashSet<(IZone, Actor, Pulse?)> _pulsedThisTurn = new();

        public IReadOnlyList<IZone> Zones => _zones.Select(p => p.Zone).ToList();

        // a zone arrives. whoever is already inside and does not get a pass is hit by the
        // appearing, if the zone is one that does that
        public void AddZone(IZone zone, Duration duration = Duration.Concentration)
        {
            if (zone == null) return;

            _zones.Add(new Placed(zone, duration));

            if (!zone.Pulses.Has(Pulse.Appear)) return;

            foreach (Actor creature in CaughtIn(zone).ToList())
                Once(zone, creature, Pulse.Appear);
        }

        // one zone gone, whatever made it - a Sunburst burning away a Darkness
        public bool EndZone(IZone zone)
        {
            if (_zones.RemoveAll(p => ReferenceEquals(p.Zone, zone)) == 0) return false;

            zone.Removed(this);
            return true;
        }

        // ends every zone from one source that one owner made - a concentration dropped
        public int EndZones(string source, Actor owner)
        {
            List<Placed> going = _zones.Where(p => p.Zone.Source == source &&
                                                   ReferenceEquals(p.Zone.Owner, owner)).ToList();

            foreach (Placed placed in going)
            {
                _zones.Remove(placed);
                placed.Zone.Removed(this);
            }

            return going.Count;
        }

        public bool Affects(IZone zone, Actor creature) =>
            creature != null && !creature.IsDown &&
            !(zone.Ground && creature.IsFlying) &&
            !(zone.SparesAllies && zone.Owner != null && creature.Side == zone.Owner.Side) &&
            !(zone.AlliesOnly && zone.Owner != null && creature.Side != zone.Owner.Side);

        public IEnumerable<Actor> CaughtIn(IZone zone) =>
            Field.Pieces.Where(a => Field.Where(a) is Cell c && zone.Covers(Field, c) &&
                                    Affects(zone, a));

        public bool IsRough(Cell cell, Actor mover) =>
            _zones.Any(p => p.Zone.Rough && p.Zone.Core(Field, cell) && Affects(p.Zone, mover));

        // a zone that was picked up and put down somewhere else - Moonbeam's beam, a Flaming
        // Sphere rolled across the floor - washes over whoever it now covers that it did not
        public void ZoneMoved(IZone zone, IEnumerable<Actor> wereIn)
        {
            if (zone == null) return;

            var before = new HashSet<Actor>(wereIn ?? Enumerable.Empty<Actor>());

            foreach (Actor creature in CaughtIn(zone).Where(a => !before.Contains(a)).ToList())
                if (zone.Pulses.Has(Pulse.Enter)) Once(zone, creature, Pulse.Enter);
        }

        // SRD's "only once per turn": Appear, Enter, StartTurn and EndTurn share it. EachSquare
        // does not, because Spike Growth's whole point is that every step hurts. a zone that acts
        // EachTime counts each kind of moment once a turn instead - Wall of Fire's "enters it for
        // the first time on a turn or ends its turn there" is two burns, not one and not three
        void Once(IZone zone, Actor creature, Pulse pulse)
        {
            if (!_pulsedThisTurn.Add((zone, creature, zone.EachTime ? pulse : (Pulse?)null))) return;

            zone.Act(this, creature, pulse);
        }

        void PulseWhereItStands(Actor creature, Pulse pulse)
        {
            if (!(Field.Where(creature) is Cell here)) return;

            foreach (Placed placed in _zones.ToList())
                if (placed.Zone.Pulses.Has(pulse) && placed.Zone.Covers(Field, here) &&
                    Affects(placed.Zone, creature))
                    Once(placed.Zone, creature, pulse);
        }

        void PulseStep(Actor mover, Cell from, Cell to)
        {
            foreach (Placed placed in _zones.ToList())
            {
                IZone zone = placed.Zone;

                if (!zone.Covers(Field, to) || !Affects(zone, mover)) continue;

                if (zone.Pulses.Has(Pulse.EachSquare)) zone.Act(this, mover, Pulse.EachSquare);

                if (zone.Pulses.Has(Pulse.Enter) && !zone.Covers(Field, from))
                    Once(zone, mover, Pulse.Enter);

                if (mover.IsDown) return;
            }
        }

        // the zones the mover carries (an emanation around its owner), with who each covers now
        IReadOnlyDictionary<IZone, List<Actor>> CaughtByZonesOf(Actor mover) =>
            _zones.Where(p => ReferenceEquals(p.Zone.Owner, mover))
                  .ToDictionary(p => p.Zone, p => CaughtIn(p.Zone).ToList());

        void PulseCarried(IReadOnlyDictionary<IZone, List<Actor>> before)
        {
            foreach (KeyValuePair<IZone, List<Actor>> carried in before)
                ZoneMoved(carried.Key, carried.Value);
        }

        // a zone that lasts until its owner's next turn starts or ends, counted the way boons are
        void TickZones(Actor whose, bool starting)
        {
            foreach (Placed placed in _zones.Where(p => ReferenceEquals(p.Zone.Owner, whose)).ToList())
            {
                if (starting && placed.Duration == Duration.NextTurn)
                {
                    _zones.Remove(placed);
                    placed.Zone.Removed(this);
                }
                else if (starting && placed.Duration == Duration.NextTurnEnd) placed.Armed = true;
                else if (!starting && placed.Duration == Duration.NextTurnEnd && placed.Armed)
                {
                    _zones.Remove(placed);
                    placed.Zone.Removed(this);
                }
            }
        }


        // leaving a hostile's reach offers it a reaction. the opportunity attack is one of the
        // things it might answer with, and no longer the only one
        void Provoke(Actor mover, Cell from, Cell to)
        {
            foreach (Actor watcher in Field.Enemies(mover).ToList())
                Offer(Moment.Leaving(mover, watcher, from, to), watcher);
        }
    }
}
