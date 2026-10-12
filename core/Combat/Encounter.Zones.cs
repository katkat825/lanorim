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

        // which zones lie where, for how long, and who each has acted on this turn are BoardZones' (cc_task_j Part 1)
        public IReadOnlyList<IZone> Zones => _zones.Snapshot();

        // a zone arrives. whoever is already inside and does not get a pass is hit by the
        // appearing, if the zone is one that does that
        public void AddZone(IZone zone, Duration duration = Duration.Concentration)
        {
            if (zone == null) return;

            _zones.Add(zone, duration);

            if (!zone.Pulses.HasFlag(Pulses.Appear)) return;

            foreach (Actor creature in CaughtIn(zone).ToList())
                Once(zone, creature, Pulses.Appear);
        }

        // one zone gone, whatever made it - a Sunburst burning away a Darkness
        public bool EndZone(IZone zone)
        {
            if (!_zones.RemoveAll(zone)) return false;

            zone.Removed(this);
            return true;
        }

        // ends every zone from one source that one owner made - a concentration dropped
        public int EndZones(string source, Actor owner)
        {
            List<IZone> going = _zones.From(source, owner);

            foreach (IZone zone in going) Take(zone);

            return going.Count;
        }

        public bool Affects(IZone zone, Actor creature) =>
            creature != null && !creature.IsDown &&
            !(zone.Ground && creature.IsFlying) &&
            (zone.Owner == null || zone.Affects switch
            {
                Core.Combat.Affects.Foes => creature.Side != zone.Owner.Side,
                Core.Combat.Affects.Allies => creature.Side == zone.Owner.Side,
                Core.Combat.Affects.NotCaster => creature != zone.Owner,
                _ => true,
            });

        public IEnumerable<Actor> CaughtIn(IZone zone) =>
            Field.Pieces.Where(a => Field.Where(a) is Cell c && zone.Covers(Field, c) &&
                                    Affects(zone, a));

        public bool IsRough(Cell cell, Actor mover) =>
            _zones.Each.Any(z => z.Rough && z.Core(Field, cell) && Affects(z, mover));

        // a zone that was picked up and put down somewhere else - Moonbeam's beam, a Flaming
        // Sphere rolled across the floor - washes over whoever it now covers that it did not
        public void ZoneMoved(IZone zone, IEnumerable<Actor> wereIn)
        {
            if (zone == null) return;

            var before = new HashSet<Actor>(wereIn ?? Enumerable.Empty<Actor>());

            foreach (Actor creature in CaughtIn(zone).Where(a => !before.Contains(a)).ToList())
                if (zone.Pulses.HasFlag(Pulses.Enter)) Once(zone, creature, Pulses.Enter);
        }

        // SRD's "only once per turn": Appear, Enter, StartTurn and EndTurn share it. EachSquare
        // does not, because Spike Growth's whole point is that every step hurts. a zone that acts
        // EachTime counts each kind of moment once a turn instead - Wall of Fire's "enters it for
        // the first time on a turn or ends its turn there" is two burns, not one and not three
        void Once(IZone zone, Actor creature, Pulses pulse)
        {
            if (!_zones.FirstThisTurn(zone, creature, pulse)) return;

            zone.Act(this, creature, pulse);
        }

        void PulseWhereItStands(Actor creature, Pulses pulse)
        {
            if (!(Field.Where(creature) is Cell here)) return;

            foreach (IZone zone in _zones.Snapshot())
                if (zone.Pulses.HasFlag(pulse) && zone.Covers(Field, here) && Affects(zone, creature))
                    Once(zone, creature, pulse);
        }

        void PulseStep(Actor mover, Cell from, Cell to)
        {
            foreach (IZone zone in _zones.Snapshot())
            {
                if (!zone.Covers(Field, to) || !Affects(zone, mover)) continue;

                if (zone.Pulses.HasFlag(Pulses.EachSquare)) zone.Act(this, mover, Pulses.EachSquare);

                if (zone.Pulses.HasFlag(Pulses.Enter) && !zone.Covers(Field, from))
                    Once(zone, mover, Pulses.Enter);

                if (mover.IsDown) return;
            }
        }

        // the zones the mover carries (an emanation around its owner), with who each covers now
        IReadOnlyDictionary<IZone, List<Actor>> CaughtByZonesOf(Actor mover) =>
            _zones.OwnedBy(mover).ToDictionary(z => z, z => CaughtIn(z).ToList());

        void PulseCarried(IReadOnlyDictionary<IZone, List<Actor>> before)
        {
            foreach (KeyValuePair<IZone, List<Actor>> carried in before)
                ZoneMoved(carried.Key, carried.Value);
        }

        // a zone that lasts until its owner's next turn starts or ends, counted the way boons are
        void TickZones(Actor whose, bool starting)
        {
            foreach (IZone zone in _zones.Lapsing(whose, starting)) Take(zone);
        }

        // one zone taken up, and told so
        void Take(IZone zone)
        {
            _zones.Remove(zone);
            zone.Removed(this);
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
