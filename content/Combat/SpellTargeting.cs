using System.Collections.Generic;
using System.Linq;
using Core.Magic;

namespace Content.Combat
{
    // HOW A SPELL IS AIMED FROM THE BAR: a direction, a square, some creatures or one, or nothing to pick; and how many
    // creatures it may pick at a level. Only the spell decides, so it isn't the session's: it was two static helpers
    // at the end of CombatSession.Options.cs until cc_task_j Part 1 brought that file under 400 lines
    public static class SpellTargeting
    {
        // `again`: the spell's later turns (a held Moonbeam, a Spiritual Weapon's swing), aimed by its repeating effects
        public static Targeting Of(Spell spell, bool again)
        {
            IEnumerable<SpellEffect> effects = spell.Effects.Where(e => again ? e.Lands.Repeats() : e.Lands != Lands.OnRepeat);

            if (effects.Any(e => e.AimKind.IsDirected())) return Targeting.Direction;

            if (effects.Any(e => e.AimKind.NeedsATargetSquare() || e.Handler.AimsAtASquare(e)))
                return Targeting.Square;

            if (effects.Any(e => e.AimKind == AimKind.Creatures)) return Targeting.Creatures;

            if (effects.Any(e => e.AimKind == AimKind.Creature)) return Targeting.Creature;

            return Targeting.None;
        }

        public static int MostTargets(Spell spell, int castAt) =>
            spell.Effects.Where(e => e.AimKind == AimKind.Creatures)
                 .Select(e => e.TargetsAt(spell.Level, castAt))
                 .DefaultIfEmpty(1)
                 .Max();
    }
}
