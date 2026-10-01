using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Space;

namespace Core.Magic
{
    // WHICH ZONES LIE ON WHICH FIGHT, in the order they went down: the incantation's own record of the zones its
    // spells made, so it can move, pulse, dispel and take them up again (Incantation.Zones). the fight keeps its
    // own list of what is on the board; this is who made what. was Incantation's private state
    // (cc_task_d-seams-and-duplication.md §8). every list it hands out is a copy, safe to remove from while walked
    internal sealed class ZoneLedger
    {
        readonly List<(Encounter Fight, SpellZone Zone)> _zones = new();

        public void Add(Encounter fight, SpellZone zone) => _zones.Add((fight, zone));

        public void Remove(Encounter fight, SpellZone zone) => _zones.Remove((fight, zone));

        // every zone on one fight
        public List<SpellZone> On(Encounter fight) =>
            _zones.Where(z => z.Fight == fight).Select(z => z.Zone).ToList();

        // every zone one creature's spells made, wherever it is
        public IEnumerable<SpellZone> Of(Actor owner) =>
            _zones.Where(z => ReferenceEquals(z.Zone.Owner, owner)).Select(z => z.Zone);

        // the zones of one creature's one spell, with the fight each is on
        public List<(Encounter Fight, SpellZone Zone)> Of(Actor owner, string spell) =>
            _zones.Where(z => ReferenceEquals(z.Zone.Owner, owner) && z.Zone.Source == spell).ToList();

        // the Forcecage the creature is in, if going to that square would take it out
        public SpellZone Caged(Encounter fight, Actor creature, Cell to)
        {
            if (!(fight.Field.Where(creature) is Cell here)) return null;

            return _zones.Where(z => z.Fight == fight && z.Zone.Area.Encloses != Edge.None)
                         .Select(z => z.Zone)
                         .FirstOrDefault(z => z.Within(here) && !z.Within(to));
        }
    }
}
