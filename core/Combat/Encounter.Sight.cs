using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): sight
    public sealed partial class Encounter
    {
        // --- sight ------------------------------------------------------------------------------

        // SRD 5.2.1 sight on a board: the creature's own eyes (Blinded, Invisible, Truesight) and
        // a clear line between the two squares. heavily obscured squares on the line - fog,
        // magical darkness - block it the way a wall does, except to Truesight in magical
        // darkness. obscurers are the zones that say so
        public bool Sees(Actor looker, Actor seen)
        {
            if (looker == null || seen == null) return false;

            if (ReferenceEquals(looker, seen)) return true;

            if (!looker.CanSee(seen)) return false;

            Cell? from = Field.Where(looker);
            Cell? to = Field.Where(seen);

            if (!from.HasValue || !to.HasValue) return true;

            if (!Field.CanSee(from.Value, to.Value)) return false;

            return !Obscured(from.Value, to.Value, looker);
        }

        // a heavily obscured square anywhere on the line, the two ends included
        public bool Obscured(Cell from, Cell to, Actor looker = null)
        {
            List<IZone> blinding = _zones.Select(p => p.Zone)
                                         .Where(z => z.Obscures == Obscurement.Heavy)
                                         .Where(z => !(z.Magical && looker != null &&
                                                       looker.Boons.Truesight))
                                         .ToList();

            if (blinding.Count == 0) return false;

            foreach (Cell cell in Sight.Between(from, to))
                if (blinding.Any(z => z.Core(Field, cell)))
                    return true;

            return false;
        }

        // SRD cover from a zone standing between the two: Blade Barrier's three-quarters, +5.
        // the squares the attacker and the target stand in do not count - it has to be between
        public int Cover(Actor attacker, Actor target)
        {
            if (!(Field.Where(attacker) is Cell from) || !(Field.Where(target) is Cell to))
                return 0;

            List<IZone> covering = _zones.Select(p => p.Zone).Where(z => z.Cover > 0).ToList();

            if (covering.Count == 0) return 0;

            int best = 0;

            foreach (Cell cell in Sight.Between(from, to))
            {
                if (cell == from || cell == to) continue;

                foreach (IZone zone in covering)
                    if (zone.Core(Field, cell))
                        best = Math.Max(best, zone.Cover);
            }

            return best;
        }
    }
}
