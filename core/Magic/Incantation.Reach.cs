using System;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Space;

namespace Core.Magic
{
    public sealed partial class Incantation
    {
        static string OutOfReach(Caster caster, Spell spell, Aim aim, Encounter fight)
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
    }
}
