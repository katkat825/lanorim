using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Space;

namespace Core.Magic
{
    // WHETHER A CAST CAN REACH: range and sight, a weapon's reach, the area a bolt must fall under, a Globe in the
    // way, and a creature the effect cannot touch at all. no state of its own: Incantation asks it before
    // anything lands (cc_task_d-seams-and-duplication.md §8, out of Incantation.Reach, .Targeting and .Zones)
    internal static class SpellReach
    {
        internal static string OutOfReach(Caster caster, Spell spell, Aim aim, Encounter fight)
        {
            Cell? here = fight.Field.Where(caster.Actor);

            if (!here.HasValue) return null;

            int reach = Math.Max(1, spell.RangeAt(caster.Actor.Level));

            // a spell that swings a weapon reaches as far as the weapon does, and the swing says so
            bool picksCreatures = !spell.Strikes &&
                                  spell.Effects.Any(e => e.AimKind == AimKind.Creature ||
                                                         e.AimKind == AimKind.Creatures);

            // Spiritual Weapon: the attack comes from the force, at a creature beside it
            SpellEffect near = spell.Effects.FirstOrDefault(e => e.NearZone > 0);
            // Dimension Door: the one creature it takes along stands beside the caster
            bool passenger = spell.Effects.Any(e => e.Passenger);

            if (picksCreatures && near == null && !passenger)
                foreach (Actor target in aim.Creatures.Where(a => !ReferenceEquals(a, caster.Actor)))
                    if (!fight.Field.InRange(caster.Actor, target, reach) ||
                        !fight.Sees(caster.Actor, target))
                        return $"{target.Id} is out of range or out of sight";

            if (near != null && aim.Square.HasValue)
                foreach (Actor target in aim.Creatures)
                    if (!(fight.Field.Where(target) is Cell at) ||
                        Battlefield.Distance(at, aim.Square.Value) > near.NearZone)
                        return $"{target.Id} is not beside it";

            if (passenger)
                foreach (Actor target in aim.Creatures.Where(a => !ReferenceEquals(a, caster.Actor)))
                    if (fight.Field.Distance(caster.Actor, target) > 1)
                        return $"{target.Id} has to be beside you to come along";

            // Chain Lightning: the bolts leap from the first target to others within 30 feet of
            // it, and no creature is struck by two
            SpellEffect leap = spell.Effects.FirstOrDefault(e => e.NearFirst > 0);

            if (leap != null && aim.Creatures.Count > 1)
            {
                if (aim.Creatures.Distinct().Count() != aim.Creatures.Count)
                    return "a creature can be struck by only one of the bolts";

                Actor first = aim.Creatures[0];

                foreach (Actor other in aim.Creatures.Skip(1))
                    if (fight.Field.Distance(first, other) > leap.NearFirst)
                        return $"{other.Id} is too far from the first target";
            }

            bool picksSquares = spell.Effects.Any(e => e.AimKind == AimKind.Burst ||
                                                       e.AimKind == AimKind.Place ||
                                                       e.AimKind == AimKind.Wall);

            // Dimension Door: a place you can visualize or describe, not only one you can see
            bool unseen = spell.Effects.Any(e => e.Unseen);

            if (picksSquares)
                foreach (Cell square in aim.Squares)
                    if (Battlefield.Distance(here.Value, square) > reach ||
                        !unseen && !fight.Field.CanSee(here.Value, square))
                        return $"{square} is out of range or out of sight";

            if (unseen && aim.Square.HasValue &&
                Battlefield.Distance(here.Value, aim.Square.Value) > reach)
                return $"{aim.Square.Value} is out of range";

            return null;
        }

        // whether this creature is one the effect can touch at all: Hold Person's Humanoid, an
        // elf's Trance against Sleep, Power Word Stun's 150 hit points. a creature it cannot touch
        // is untouched - no save, nothing lands - the way SRD's "is unaffected" reads
        internal static bool Touches(SpellEffect effect, Actor target)
        {
            if (!effect.TagRules.Touch(target)) return false;

            if (effect.HitPoints != null && !effect.HitPoints.Lets(target)) return false;

            if (effect.NeedsSight && target.Has(Condition.Blinded)) return false;

            if (effect.MaxSize.HasValue && target.CurrentSize > effect.MaxSize.Value) return false;

            return true;
        }

        // Call Lightning: a bolt must fall under the cloud. at the cast the cloud is not there yet,
        // so it is where it will be; after, it is where it is
        internal static string UnderTheZone(Caster caster, Spell spell, Aim aim, Encounter fight,
                                            SpellZone made)
        {
            if (!spell.Effects.Any(e => e.WithinZone) || !aim.Square.HasValue) return null;

            SpellEffect area = spell.Effects.FirstOrDefault(e => e.Handler.MakesAZone);

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

        // a Globe of Invulnerability between the caster and the target: the target stands in a
        // spell-blocking zone the caster is outside, and the spell is of a level it stops
        internal static bool Shielded(Encounter fight, Actor caster, Actor target, int castAt)
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
