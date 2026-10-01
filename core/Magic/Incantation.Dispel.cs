using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Rules;
using Core.Space;

namespace Core.Magic
{
    // DISPELLING: Dispel Magic on a creature or at a square, Remove Curse, Disintegrate on a
    // creation of force
    public sealed partial class Incantation
    {
        // SRD 5.2.1 Dispel Magic on "a magical effect", and Disintegrate on "a creation of magical
        // force": every spell with a zone on the square ends - Dispel Magic's at or below the slot,
        // or on a spellcasting check against 10 + its level
        int DispelAt(Caster caster, Cell square, int castAt, Encounter fight, bool forceOnly)
        {
            int ended = 0;

            foreach (SpellZone zone in _zones.On(fight).Where(z => z.Covers(fight.Field, square)))
            {
                if (forceOnly && !zone.Spell.ForceCreation) continue;

                int level = zone.CastAt;

                bool ends = forceOnly || level <= castAt ||
                            Checks.Check(_resolver, caster.Actor, caster.Ability, 10 + level).Succeeded;

                if (!ends) continue;

                Actor owner = zone.Caster.Actor;

                if (owner.Concentrating == zone.Source) Release(owner);
                else EndZones(owner, zone.Source);

                ended++;
            }

            return ended;
        }

        // SRD 5.2.1 Remove Curse: every curse on the creature ends, whatever its level
        internal int Uncurse(Actor target)
        {
            List<string> curses = _placed.Where(p => ReferenceEquals(p.Target, target) &&
                                                     p.Caster != null &&
                                                     p.Caster.Find(p.Spell)?.Curse == true)
                                         .Select(p => p.Spell)
                                         .Distinct()
                                         .ToList();

            foreach (string curse in curses) Lift(target, curse);

            return curses.Count;
        }

        // SRD 5.2.1 Dispel Magic, per spell on the target: at or below the dispel's level it ends;
        // above it, a check with the caster's spellcasting ability against 10 + that level
        internal int Dispel(Caster caster, Actor target, int castAt)
        {
            int ended = 0;

            foreach (IGrouping<string, Placement> held in
                     _placed.Where(p => ReferenceEquals(p.Target, target))
                            .GroupBy(p => p.Spell)
                            .ToList())
            {
                int level = held.Max(p => p.Level);

                bool ends = level <= castAt ||
                            Checks.Check(_resolver, caster.Actor, caster.Ability, 10 + level)
                                  .Succeeded;

                if (!ends) continue;

                Lift(target, held.Key);

                ended++;
            }

            return ended;
        }
    }
}
