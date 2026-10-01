using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Space;

namespace Core.Magic
{
    // WHO AN AREA TAKES IN: a line, a cone or a cube from the caster, a square, a burst or several, the squares a
    // place, a zone or a light covers, and who of those in it count. no state of its own: it was the static half
    // of Incantation.Targeting (cc_task_d-seams-and-duplication.md §8), which still decides what lands on them
    internal static class SpellShapes
    {
        // who the area counts, of those in it
        internal static IReadOnlyList<Actor> Counted(Caster caster, SpellEffect effect, Encounter fight,
                                                     List<Cell> covered, IReadOnlyList<Actor> targets)
        {
            // on the zone itself it is the zone's pulses that spare them
            if (!effect.Handler.MakesAZone)
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
        internal static IEnumerable<Cell> Placed(Caster caster, SpellEffect effect, Aim aim, Encounter fight)
        {
            bool carried = effect.AimKind == AimKind.Caster || effect.AimKind == AimKind.Around;

            Cell? here = fight?.Field.Where(caster.Actor);
            Cell? centre = carried ? here ?? aim.Square : aim.Square ?? here;

            if (centre.HasValue && fight != null) return fight.Field.Burst(centre.Value, Math.Max(0, effect.Radius));

            return centre.HasValue ? new[] { centre.Value } : Array.Empty<Cell>();
        }

        // a line, a cone or a cube from the caster's square
        internal static IReadOnlyList<Actor> Swept(Caster caster, SpellEffect effect, Aim aim, Encounter fight,
                                                   List<Cell> covered)
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

        internal static IReadOnlyList<Actor> InSquare(SpellEffect effect, Aim aim, Encounter fight, List<Cell> covered)
        {
            if (fight == null || !aim.Square.HasValue) return aim.Creatures;

            List<Cell> square = fight.Field.Square(aim.Square.Value, Math.Max(1, effect.Length)).ToList();

            covered.AddRange(square);

            return fight.Field.Caught(square).ToList();
        }

        // SRD bursts centred on you spare you; the eight-foot fireball in your own lap is a
        // different spell
        internal static IReadOnlyList<Actor> AroundCaster(Caster caster, SpellEffect effect, Encounter fight)
        {
            Cell? here = fight?.Field.Where(caster.Actor);

            if (!here.HasValue || fight == null) return Array.Empty<Actor>();

            return fight.Field.Caught(here.Value, effect.Radius)
                        .Where(a => !ReferenceEquals(a, caster.Actor))
                        .ToList();
        }

        // several centres, one creature caught once however many it stands in
        internal static IReadOnlyList<Actor> InBursts(SpellEffect effect, Aim aim, Encounter fight)
        {
            if (fight == null || !aim.Square.HasValue) return aim.Creatures;

            return aim.Squares.Take(Math.Max(1, effect.Points))
                      .SelectMany(c => fight.Field.Caught(c, effect.Radius))
                      .Distinct()
                      .ToList();
        }
    }
}
