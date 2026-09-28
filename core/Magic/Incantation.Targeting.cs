using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Space;

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

            // Slow's "up to six creatures of your choice": the caster's foes first, then by id, so
            // the same cast picks the same six
            if (effect.UpTo > 0 && targets.Count > effect.UpTo)
                targets = targets.OrderBy(t => t.Side == caster.Actor.Side ? 1 : 0)
                                 .ThenBy(t => t.Id, StringComparer.Ordinal)
                                 .Take(effect.UpTo).ToList();

            // Sunburst: magical darkness in the area is dispelled
            if (effect.DispelsDarkness && fight != null) Brighten(fight, effect, aim, covered);

            // A ZONE ON A BOARD IS A REAL THING NOW: it goes on the fight, acts when its pulses
            // come round, and comes off when the spell does
            if (effect.Kind == Primitive.Zone && fight != null)
                MakeZone(caster, spell, effect, aim, castAt, fight);

            if (effect.AimKind == AimKind.Place || effect.Kind == Primitive.Zone ||
                effect.Kind == Primitive.Illuminate)
                squares.AddRange(Placed(caster, effect, aim, fight));

            foreach (Actor target in targets)
                landings.Add(Apply(caster, spell, effect, aim, target, castAt, fight, answering,
                                   saves));

            if (effect.Leaps > 0 && targets.Count > 0)
                Leap(caster, spell, effect, aim, castAt, fight, answering, saves, landings, targets);
        }

        // who the area counts, of those in it
        static IReadOnlyList<Actor> Counted(Caster caster, SpellEffect effect, Encounter fight, List<Cell> covered,
                                            IReadOnlyList<Actor> targets)
        {
            // on the zone itself it is the zone's pulses that spare them
            if (effect.Kind != Primitive.Zone)
            {
                // "creatures of your choice": an area that leaves the caster's own side alone
                if (effect.Affects == Affects.Foes)
                    targets = targets.Where(t => t.Side != caster.Actor.Side).ToList();

                // only the caster's own side
                if (effect.Affects == Affects.Allies)
                    targets = targets.Where(t => t.Side == caster.Actor.Side).ToList();
            }

            // Entangle: "each creature (other than you)"
            if (effect.Affects == Affects.NotCaster)
                targets = targets.Where(t => !ReferenceEquals(t, caster.Actor)).ToList();

            // Hypnotic Pattern: only a creature that can see the pattern - some square of it, not
            // past a wall or through a heavily obscured square (its own included) - and not Blinded
            if (effect.NeedsSight && fight != null && covered.Count > 0)
            {
                List<Cell> pattern = covered.Distinct().ToList();

                targets = targets.Where(t => fight.Field.Where(t) is Cell at &&
                                             pattern.Any(c => fight.Field.CanSee(at, c) &&
                                                              !fight.Obscured(at, c, t))).ToList();
            }

            return targets;
        }

        // the squares a place, a zone or a light takes up. one the caster carries sits on the
        // caster, whatever else was aimed
        static IEnumerable<Cell> Placed(Caster caster, SpellEffect effect, Aim aim, Encounter fight)
        {
            bool carried = effect.AimKind == AimKind.Caster || effect.AimKind == AimKind.Around;

            Cell? here = fight?.Field.Where(caster.Actor);
            Cell? centre = carried ? here ?? aim.Square : aim.Square ?? here;

            if (centre.HasValue && fight != null) return fight.Field.Burst(centre.Value, Math.Max(0, effect.Radius));

            return centre.HasValue ? new[] { centre.Value } : Array.Empty<Cell>();
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
            AimKind.Zone when effect.Kind == Primitive.Sway && effect.Linger.WhileInZone && fight == null =>
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

        // a line, a cone or a cube from the caster's square
        static IReadOnlyList<Actor> Swept(Caster caster, SpellEffect effect, Aim aim, Encounter fight, List<Cell> covered)
        {
            Cell? here = fight?.Field.Where(caster.Actor);

            // off the board there is no shape, so whoever the caller says was in it was
            if (!here.HasValue || fight == null) return aim.Creatures;

            Facing? facing = aim.Facing ??
                             (aim.Square.HasValue ? Template.Toward(here.Value, aim.Square.Value) : (Facing?)null);

            if (!facing.HasValue) return Array.Empty<Actor>();

            List<Cell> swept = (effect.AimKind switch
            {
                AimKind.Line => fight.Field.Line(here.Value, facing.Value, effect.Length, Math.Max(1, effect.Width)),
                AimKind.Cone => fight.Field.Cone(here.Value, facing.Value, effect.Length),
                _ => fight.Field.Cube(here.Value, facing.Value, effect.Length),
            }).ToList();

            covered.AddRange(swept);

            // the shape starts past the caster's own square, so the caster is never in it
            return fight.Field.Caught(swept).ToList();
        }

        static IReadOnlyList<Actor> InSquare(SpellEffect effect, Aim aim, Encounter fight, List<Cell> covered)
        {
            if (fight == null || !aim.Square.HasValue) return aim.Creatures;

            List<Cell> square = fight.Field.Square(aim.Square.Value, Math.Max(1, effect.Length)).ToList();

            covered.AddRange(square);

            return fight.Field.Caught(square).ToList();
        }

        // SRD bursts centred on you spare you; the eight-foot fireball in your own lap is a
        // different spell
        static IReadOnlyList<Actor> AroundCaster(Caster caster, SpellEffect effect, Encounter fight)
        {
            Cell? here = fight?.Field.Where(caster.Actor);

            if (!here.HasValue || fight == null) return Array.Empty<Actor>();

            return fight.Field.Caught(here.Value, effect.Radius)
                        .Where(a => !ReferenceEquals(a, caster.Actor))
                        .ToList();
        }

        // several centres, one creature caught once however many it stands in
        static IReadOnlyList<Actor> InBursts(SpellEffect effect, Aim aim, Encounter fight)
        {
            if (fight == null || !aim.Square.HasValue) return aim.Creatures;

            return aim.Squares.Take(Math.Max(1, effect.Points))
                      .SelectMany(c => fight.Field.Caught(c, effect.Radius))
                      .Distinct()
                      .ToList();
        }

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

            foreach ((Encounter where, SpellZone zone) in _zones.Where(z => z.fight == fight &&
                                                                           z.zone.Obscures == Obscurement.MagicalDarkness)
                                                               .ToList())
            {
                if (!zone.Squares(fight.Field).Any(area.Contains)) continue;

                if (zone.Owner.Concentrating == zone.Source) Release(zone.Owner);
                else
                {
                    fight.EndZone(zone);
                    _zones.Remove((where, zone));
                }
            }
        }

        // whether this creature is one the effect can touch at all: Hold Person's Humanoid, an
        // elf's Trance against Sleep, Power Word Stun's 150 hit points. a creature it cannot touch
        // is untouched - no save, nothing lands - the way SRD's "is unaffected" reads
        static bool Touches(SpellEffect effect, Actor target)
        {
            if (!effect.TagRules.Touch(target)) return false;

            if (effect.HitPoints != null && !effect.HitPoints.Lets(target)) return false;

            if (effect.NeedsSight && target.Has(Condition.Blinded)) return false;

            if (effect.MaxSize.HasValue && target.CurrentSize > effect.MaxSize.Value) return false;

            return true;
        }

        // Call Lightning: a bolt must fall under the cloud. at the cast the cloud is not there yet,
        // so it is where it will be; after, it is where it is
        static string UnderTheZone(Caster caster, Spell spell, Aim aim, Encounter fight,
                                   SpellZone made)
        {
            if (!spell.Effects.Any(e => e.WithinZone) || !aim.Square.HasValue) return null;

            SpellEffect area = spell.Effects.FirstOrDefault(e => e.Kind == Primitive.Zone);

            if (area == null) return null;

            bool under;

            if (made != null)
                under = made.Covers(fight.Field, aim.Square.Value);
            else
            {
                Cell? centre = area.OnCaster ? fight.Field.Where(caster.Actor) : aim.Square;

                under = centre.HasValue &&
                        Battlefield.Distance(centre.Value, aim.Square.Value) <= area.Radius;
            }

            return under ? null : "it has to fall under the spell's area";
        }
    }
}
