using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Space;

using static Core.Magic.SpellReach;
using static Core.Magic.SpellShapes;

namespace Core.Magic
{
    // WHO AN EFFECT REACHES: the shape on the board, who the area counts, who can be touched at
    // all - and then each of them, through Apply
    public sealed partial class Incantation
    {
        void Resolve(Caster caster, Spell spell, SpellEffect effect, Aim aim, int castAt,
                     Encounter fight, Moment answering, List<Landing> landings,
                     List<Cell> squares, List<Cell> covered, Dictionary<Actor, Attempt> saves,
                     HashSet<Actor> only)
        {
            IReadOnlyList<Actor> targets =
                Targets(caster, spell, effect, aim, castAt, fight, covered);

            if (only != null) targets = targets.Where(only.Contains).ToList();

            targets = Counted(caster, effect, fight, covered, targets);

            // Shield: a creature a spell has picked out may answer it, once a cast, before it lands
            if (fight != null && (effect.AimKind == AimKind.Creature || effect.AimKind == AimKind.Creatures))
                foreach (Actor picked in targets.Where(t => !ReferenceEquals(t, caster.Actor)).Distinct().ToList())
                    if (_offered.Add(picked))
                        fight.Offer(Moment.Targeted(caster.Actor, picked, spell.Id), picked);

            // Dispel Magic on an effect, Disintegrate on a creation of force: whatever spell holds
            // the aimed square
            if (effect.Kind == Primitive.Dispel && effect.AimKind == AimKind.Place && fight != null &&
                aim.Square.HasValue)
            {
                int ended = DispelAt(caster, aim.Square.Value, castAt, fight, effect.EndsForce);
                landings.Add(new Landing(effect, null, ended > 0, ended));
                return;
            }

            // Slow's "up to six creatures of your choice": the caster's foes first, the nearest of them, then the turn
            // order, so the same cast picks the same six (by id until 2026-10-03, so a name chose them)
            if (effect.UpTo > 0 && targets.Count > effect.UpTo)
                targets = targets.OrderBy(t => t.Side == caster.Actor.Side ? 1 : 0)
                                 .ThenBy(t => fight?.Field.Distance(caster.Actor, t) ?? 0)
                                 .ThenBy(t => fight?.InitiativeRank(t) ?? 0)
                                 .ThenBy(t => t.Id, StringComparer.Ordinal)
                                 .Take(effect.UpTo).ToList();

            // Sunburst: magical darkness in the area is dispelled
            if (effect.DispelsDarkness && fight != null) Brighten(fight, effect, aim, covered);

            // A ZONE ON A BOARD IS A REAL THING NOW: it goes on the fight, acts when its pulses
            // come round, and comes off when the spell does
            if (effect.Handler.MakesAZone && fight != null)
                MakeZone(caster, spell, effect, aim, castAt, fight);

            if (effect.AimKind == AimKind.Place || effect.Handler.CoversGround)
                squares.AddRange(Placed(caster, effect, aim, fight));

            foreach (Actor target in targets)
                landings.Add(Apply(caster, spell, effect, aim, target, castAt, fight, answering,
                                   saves));

            if (effect.Leaps > 0 && targets.Count > 0)
                Leap(caster, spell, effect, aim, castAt, fight, answering, saves, landings, targets);
        }

        // Chromatic Orb: two matching dice and it leaps to another creature the caster picked,
        // within reach of the last one, once per slot level, never to the same one twice
        void Leap(Caster caster, Spell spell, SpellEffect effect, Aim aim, int castAt, Encounter fight,
                  Moment answering, Dictionary<Actor, Attempt> saves, List<Landing> landings,
                  IReadOnlyList<Actor> targets)
        {
            var struck = new HashSet<Actor>(targets);
            Actor last = targets[targets.Count - 1];
            int leaps = Math.Max(1, castAt);

            while (leaps > 0 && Matched(LastFaces))
            {
                Actor next = aim.Creatures.FirstOrDefault(
                    a => !struck.Contains(a) &&
                         (fight == null || fight.Field.Distance(last, a) <= effect.Leaps));

                if (next == null) break;

                struck.Add(next);
                leaps--;
                last = next;

                LastFaces = Array.Empty<int>();
                landings.Add(Apply(caster, spell, effect, aim, next, castAt, fight, answering, saves));
            }
        }

        // the faces of the last damage roll, for Chromatic Orb's leap
        internal IReadOnlyList<int> LastFaces { get; set; } = Array.Empty<int>();

        static bool Matched(IReadOnlyList<int> faces) =>
            faces.GroupBy(f => f).Any(g => g.Count() > 1);

        IReadOnlyList<Actor> Targets(Caster caster, Spell spell, SpellEffect effect, Aim aim,
                                     int castAt, Encounter fight, List<Cell> covered) => effect.AimKind switch
        {
            AimKind.Line or AimKind.Cone or AimKind.Cube => Swept(caster, effect, aim, fight, covered),
            AimKind.Caster => new[] { caster.Actor },

            // an aura cast with no board to stand on is on the caster alone: Pass without Trace on a
            // sneak through the campaign's story
            AimKind.Zone when effect.Handler.LastsWhileInTheZone(effect) && fight == null =>
                new[] { caster.Actor },

            // acts only when its zone pulses, never on the cast itself
            AimKind.Zone => Array.Empty<Actor>(),
            AimKind.Square => InSquare(effect, aim, fight, covered),
            AimKind.Place or AimKind.Wall => Array.Empty<Actor>(),
            AimKind.Around => AroundCaster(caster, effect, fight),
            AimKind.Burst => InBursts(effect, aim, fight),

            // several picks, and SRD lets a Scorching Ray or an Eldritch Blast put more than one of
            // them on the same creature - so a repeated creature is kept, not merged
            AimKind.Creatures => aim.Creatures.Take(effect.TargetsAt(spell.Level, castAt, caster.Actor.Level)).ToList(),
            _ => Picked(caster, spell, effect, aim, fight),
        };

        // one creature: the one picked - or, for Spiritual Weapon, the creature beside the force,
        // the one named or the nearest foe beside it when the aim named only the square
        IReadOnlyList<Actor> Picked(Caster caster, Spell spell, SpellEffect effect, Aim aim, Encounter fight)
        {
            if (effect.NearZone <= 0 || fight == null) return aim.Creatures.Take(1).ToList();

            Cell? force = aim.Square ??
                          ZonesOf(caster.Actor).FirstOrDefault(z => z.Source == spell.Id)?.Centre;

            if (!force.HasValue) return Array.Empty<Actor>();

            IEnumerable<Actor> beside = fight.Field.Pieces
                .Where(a => !a.IsDown && fight.Field.Where(a) is Cell at &&
                            Battlefield.Distance(at, force.Value) <= effect.NearZone);

            return aim.Creatures.Count > 0
                ? aim.Creatures.Where(beside.Contains).Take(1).ToList()
                : beside.Where(a => a.Side != caster.Actor.Side)
                        .OrderBy(a => a.Health.Current)
                        .ThenBy(a => a.Id, StringComparer.Ordinal)
                        .Take(1).ToList();
        }

        // every zone of magical darkness the effect's area reaches ends, and the spell holding it
        // with it
        void Brighten(Encounter fight, SpellEffect effect, Aim aim, List<Cell> covered)
        {
            var area = new HashSet<Cell>(covered);

            if (effect.AimKind == AimKind.Burst)
                foreach (Cell centre in aim.Squares.Take(Math.Max(1, effect.Points)))
                    area.UnionWith(fight.Field.Burst(centre, effect.Radius));

            foreach (SpellZone zone in _zones.On(fight).Where(z => z.Obscures == Obscurement.MagicalDarkness))
            {
                if (!zone.Squares(fight.Field).Any(area.Contains)) continue;

                if (zone.Owner.Concentrating == zone.Source) Release(zone.Owner);
                else
                {
                    fight.EndZone(zone);
                    _zones.Remove(fight, zone);
                }
            }
        }
    }
}
