using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Space;

namespace Core.Magic
{
    // ZONES: put down, moved, drifting, pulsing, carrying auras, and taken up again
    public sealed partial class Incantation
    {
        readonly List<(Encounter fight, SpellZone zone)> _zones = new();

        // what a zone's pulse did, for whoever is showing it - the fight log, the table
        public event Action<SpellZone, Actor, Pulses, IReadOnlyList<Landing>> Pulsed;

        void MakeZone(Caster caster, Spell spell, SpellEffect area, Aim aim, int castAt,
                      Encounter fight)
        {
            bool carried = area.AimKind == AimKind.Caster || area.AimKind == AimKind.Around;

            Cell? centre = carried ? null
                         : area.OnCaster ? fight.Field.Where(caster.Actor)
                         : aim.Square ?? fight.Field.Where(caster.Actor);

            if (!carried && !centre.HasValue) return;

            var zone = new SpellZone(this, caster, spell, area, castAt, centre)
            {
                Facing = aim.Facing ?? Facing.East,
                Side = aim.Side,
                Ring = aim.Mode == "ring",
            };

            _zones.Add((fight, zone));

            // Forcecage: the outline goes up as walls, and comes down with the zone
            if (area.Encloses != Edge.None)
                zone.Enclose(fight.Field);

            fight.AddZone(zone, area.Duration);

            if (area.Drifts > 0) Watch(fight);

            if (zone.Auras.Any())
            {
                Watch(fight);
                Refresh(fight);
            }
        }

        // a repeating shift that reaches the zone MOVES it - Moonbeam's beam, a Flaming Sphere
        // rolled - and whoever it now covers is washed over as if they had walked in. a refusal,
        // or null
        string MoveZone(Caster caster, Spell spell, SpellEffect move, Aim aim, Encounter fight)
        {
            SpellZone zone = ZonesOf(caster.Actor).FirstOrDefault(z => z.Source == spell.Id &&
                                                                      !z.FollowsOwner);

            if (zone == null || !aim.Square.HasValue || fight == null) return null;

            if (move.Length > 0 &&
                Battlefield.Distance(zone.Centre.Value, aim.Square.Value) > move.Length)
                return "further than it can be moved";

            List<Actor> wereIn = fight.CaughtIn(zone).ToList();

            if (move.Rams)
            {
                // Flaming Sphere: rolled square by square, and the first creature in its way
                // is rammed and stops it for the turn
                Cell at = zone.Centre.Value;
                Actor rammed = null;

                foreach (Cell next in Sight.Between(at, aim.Square.Value).Skip(1))
                {
                    Actor there = fight.Field.At(next);

                    if (there != null && !ReferenceEquals(there, caster.Actor))
                    {
                        rammed = there;
                        break;
                    }

                    if (!fight.Field.Map.Contains(next) || !fight.Field.Map.CanCross(at, next))
                        break;

                    at = next;
                }

                zone.MoveTo(at);
                fight.ZoneMoved(zone, wereIn);

                if (rammed != null && zone.Pulses.HasFlag(Core.Combat.Pulses.Ram) && fight.Affects(zone, rammed))
                    zone.Act(fight, rammed, Core.Combat.Pulses.Ram);

                return null;
            }

            zone.MoveTo(aim.Square.Value);
            fight.ZoneMoved(zone, wereIn);

            return null;
        }

        static string AuraId(string spell) => spell + "_aura";

        // the Forcecage the creature is in, if going to that square would take it out
        internal SpellZone Caged(Encounter fight, Actor creature, Cell to)
        {
            if (!(fight.Field.Where(creature) is Cell here)) return null;

            return _zones.Where(z => z.fight == fight && z.zone.Area.Encloses != Edge.None)
                         .Select(z => z.zone)
                         .FirstOrDefault(z => z.Within(here) && !z.Within(to));
        }

        // who is inside each aura now: the aura's sways go on whoever stepped in and come off
        // whoever stepped out
        void Refresh(Encounter fight)
        {
            // SRD 5.2.1 Web: "Restrained ... while in the webs". a condition held to the zone
            // drops on leaving it; a sway held to it is an aura, which the loop below keeps
            foreach (Placement held in _placed.Where(p => p.Spec.WhileInZone &&
                                                          p.Condition != Condition.None).ToList())
            {
                SpellZone zone = _zones.Where(z => z.fight == fight && z.zone.Source == held.Spell)
                                       .Select(z => z.zone).FirstOrDefault();

                if (zone == null || !(fight.Field.Where(held.Target) is Cell at) ||
                    !zone.Covers(fight.Field, at))
                    Drop(held, fight);
            }

            // Moonbeam: out of the beam, it can shape-shift again
            foreach ((Encounter where, SpellZone zone) in _zones.Where(z => z.fight == fight).ToList())
            {
                if (!zone.Acts.Any(e => e.RevertsShape)) continue;

                string reverted = RevertedId(zone.Source);

                foreach (Actor actor in fight.Actors.Where(a => a.Boons.All.Any(b => b.Id == reverted)))
                    if (!(fight.Field.Where(actor) is Cell at) || !zone.Covers(fight.Field, at))
                        actor.Boons.EndId(reverted);
            }

            foreach ((Encounter where, SpellZone zone) in _zones.Where(z => z.fight == fight).ToList())
            {
                List<SpellEffect> auras = zone.Auras.ToList();

                if (auras.Count == 0) continue;

                string id = AuraId(zone.Source);
                var inside = new HashSet<Actor>(fight.CaughtIn(zone));

                foreach (Actor actor in fight.Actors)
                {
                    bool has = actor.Boons.All.Any(b => b.Id == id);

                    if (inside.Contains(actor) && !has)
                        foreach (SpellEffect aura in auras)
                            actor.Boons.Add(SwayHandler.BoonFor(zone.Spell, aura, zone.Caster, Aim.Nothing, 0, id));
                    else if (!inside.Contains(actor) && has)
                        actor.Boons.EndId(id);
                }
            }
        }

        // Cloudkill: at the start of its caster's turn the zone moves this many squares straight
        // away from the caster, washing over whoever it now covers
        void Drift(Encounter fight, Actor whose)
        {
            foreach ((Encounter where, SpellZone zone) in _zones.Where(z => z.fight == fight &&
                                                                           ReferenceEquals(z.zone.Owner, whose) &&
                                                                           z.zone.Area.Drifts > 0 &&
                                                                           z.zone.Centre.HasValue)
                                                               .ToList())
            {
                Cell? from = fight.Field.Where(whose);

                if (!from.HasValue) continue;

                Cell centre = zone.Centre.Value;
                int dx = Math.Sign(centre.X - from.Value.X);
                int dy = Math.Sign(centre.Y - from.Value.Y);

                if (dx == 0 && dy == 0) continue;

                Cell to = centre;

                for (int i = 0; i < zone.Area.Drifts; i++)
                {
                    var next = new Cell(to.X + dx, to.Y + dy);

                    if (!fight.Field.Map.Contains(next)) break;

                    to = next;
                }

                if (to == centre) continue;

                List<Actor> wereIn = fight.CaughtIn(zone).ToList();

                zone.MoveTo(to);
                fight.ZoneMoved(zone, wereIn);
            }
        }

        public IEnumerable<SpellZone> ZonesOf(Actor caster) =>
            _zones.Where(z => ReferenceEquals(z.zone.Owner, caster)).Select(z => z.zone);

        // the zone acting on one creature at one moment: the spell's zone effects whose pulses
        // include it, each resolved against that creature exactly as a cast would, at the level
        // the spell was cast at and with the caster's numbers as they are now
        internal void Pulse(SpellZone zone, Encounter fight, Actor creature, Pulses pulse)
        {
            var landings = new List<Landing>();
            var saves = new Dictionary<Actor, Attempt>();
            Aim aim = Aim.At(creature);

            // Spirit Guardians: radiant or necrotic, as picked at the cast
            if (_chosenType.TryGetValue((zone.Caster.Actor, zone.Spell.Id), out DamageType picked))
                aim = aim.Choosing(picked);

            bool previousLanded = true;

            Cell? standing = fight.Field.Where(creature);

            foreach (SpellEffect effect in zone.Acts.Where(e => e.Pulses.HasFlag(pulse)))
            {
                // Wall of Fire: entering is entering the wall, not the ground beside it
                if (effect.CoreOnly && (!standing.HasValue || !zone.Core(fight.Field, standing.Value)))
                    continue;

                // an effect that follows the one before only comes with it: Stinking Cloud's
                // lost actions come with its Poisoned, and a creature that cannot be poisoned
                // keeps them
                if (effect.Follows && !previousLanded) continue;

                Landing landing = Apply(zone.Caster, zone.Spell, effect, aim, creature, zone.CastAt,
                                        fight, null, saves);

                previousLanded = landing.Landed;
                landings.Add(landing);
            }

            if (landings.Count > 0) Pulsed?.Invoke(zone, creature, pulse, landings);
        }

        void EndZones(Actor caster, string spell)
        {
            // an aura's sways go with it, from everybody it was on
            foreach ((Encounter fight, SpellZone zone) in
                     _zones.Where(z => ReferenceEquals(z.zone.Owner, caster) &&
                                       z.zone.Source == spell && z.zone.Auras.Any()).ToList())
                foreach (Actor actor in fight.Actors)
                    actor.Boons.EndId(AuraId(spell));

            foreach ((Encounter fight, SpellZone zone) in
                     _zones.Where(z => ReferenceEquals(z.zone.Owner, caster) &&
                                       z.zone.Source == spell).ToList())
            {
                fight.EndZones(spell, caster);
                _zones.Remove((fight, zone));
            }
        }

        // a Globe of Invulnerability between the caster and the target: the target stands in a
        // spell-blocking zone the caster is outside, and the spell is of a level it stops
        static bool Shielded(Encounter fight, Actor caster, Actor target, int castAt)
        {
            Cell? inside = fight.Field.Where(target);
            Cell? from = fight.Field.Where(caster);

            if (!inside.HasValue || !from.HasValue) return false;

            return fight.Zones.Any(z => z.BlocksSpellsUpTo > 0 && castAt <= z.BlocksSpellsUpTo &&
                                        z.Covers(fight.Field, inside.Value) &&
                                        !z.Covers(fight.Field, from.Value));
        }
    }
}
