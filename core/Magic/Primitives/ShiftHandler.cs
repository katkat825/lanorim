using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Magic
{
    // SHIFT: anything that changes which square something is on. the caster teleporting (Misty
    // Step, Dimension Door), a push away from the caster (Thunderwave), a creature moved to a
    // square (Telekinesis) - and, on a repeat, the spell's own zone moved (Incantation.MoveZone)
    public sealed class ShiftHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Shift;

        public IReadOnlyList<string> Keys { get; } = new[] { "push", "teleports", "passenger", "unseen", "rams" };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.Rams && effect.AimKind != AimKind.Zone)
                yield return "'rams' is how a zone is moved - a shift that reaches it";

            if ((effect.Teleports || effect.Passenger || effect.Unseen) && effect.AimKind != AimKind.Caster)
                yield return "'teleports', 'passenger' and 'unseen' are the caster's own travel - 'aim': 'caster'";
        }

        public Landing Apply(Contact c)
        {
            SpellEffect effect = c.Effect;

            if (effect.AimKind == AimKind.Caster) return Travel(c);

            if (effect.Push > 0) return Push(c);

            if (effect.AimKind == AimKind.Creature || effect.AimKind == AimKind.Creatures) return Move(c);

            // a zone moved on a repeat is Incantation's to do; the cast itself reports
            return c.Report(c.Landed);
        }

        // Misty Step, Dimension Door: the caster goes to the aimed square
        static Landing Travel(Contact c)
        {
            SpellEffect effect = c.Effect;
            Actor target = c.Target;
            Encounter fight = c.Fight;

            if (fight == null || !c.Aim.Square.HasValue)
                return new Landing(effect, target, true);

            Cell to = c.Aim.Square.Value;

            // a seen, empty square within the spell's range - or, for Dimension Door, one it can
            // visualize or describe
            bool reachable = fight.Field.Where(target) is Cell from &&
                             Battlefield.Distance(from, to) <= c.Spell.RangeAt(c.Caster.Actor.Level) &&
                             (effect.Unseen || fight.Field.CanSee(from, to));

            // SRD 5.2.1 Dimension Door: the one willing creature beside you, to a space within 5
            // feet of where you arrive
            Actor rider = effect.Passenger
                ? c.Aim.Creatures.FirstOrDefault(a => !ReferenceEquals(a, target) &&
                                                      fight.Field.Distance(target, a) <= 1)
                : null;

            Cell? riderTo = rider == null
                ? null
                : fight.Field.Map.Cells.Where(cell => fight.Field.Map.IsPassable(cell) && cell != to &&
                                                      Battlefield.Distance(cell, to) <= 1 &&
                                                      !fight.Field.Occupies(cell, rider))
                       .OrderBy(cell => cell.Y).ThenBy(cell => cell.X)
                       .Select(cell => (Cell?)cell).FirstOrDefault();

            // arriving in an occupied space: 4d6 force to each traveller, and it fails
            bool blocked = fight.Field.Occupies(to, target) || !fight.Field.Map.IsPassable(to) ||
                           rider != null && !riderTo.HasValue;

            if (reachable && effect.Unseen && blocked)
            {
                int hurt = 0;

                foreach (Actor traveller in new[] { target, rider }.Where(a => a != null))
                {
                    int took = traveller.Suffer(Math.Max(0, c.Resolver.Roll(DiceRoll.Parse("4d6"), c.Caster.Actor)),
                                                DamageType.Force);
                    fight.Hurt(c.Caster.Actor, traveller, took);
                    hurt += took;
                }

                return new Landing(effect, target, false, hurt);
            }

            if (!reachable || blocked)
                return new Landing(effect, target, false);

            // Forcecage: magical travel out of it needs a Charisma save
            if (effect.Teleports && c.Magic.Caged(fight, target, to) is SpellZone cage)
            {
                Attempt save = Checks.Save(c.Resolver, target, Ability.Charisma, cage.Caster.SaveDc);

                if (save.Failed) return new Landing(effect, target, false, 0, save);
            }

            if (!fight.Field.Place(target, to)) return new Landing(effect, target, false);

            fight.Observer.Moved(target, new[] { to });

            if (rider != null && riderTo.HasValue && fight.Field.Place(rider, riderTo.Value))
                fight.Observer.Moved(rider, new[] { riderTo.Value });

            return new Landing(effect, target, true);
        }

        // Thunderwave: pushed straight away from the caster, stopped by walls and bodies
        static Landing Push(Contact c)
        {
            SpellEffect effect = c.Effect;
            Actor target = c.Target;
            Encounter fight = c.Fight;

            if (c.Resisted) return new Landing(effect, target, false, 0, c.Attempt);

            if (fight == null || !(fight.Field.Where(c.Caster.Actor) is Cell from) ||
                !(fight.Field.Where(target) is Cell at))
                return new Landing(effect, target, c.Landed, 0, c.Attempt);

            int dx = Math.Sign(at.X - from.X);
            int dy = Math.Sign(at.Y - from.Y);
            Cell to = at;

            for (int i = 0; i < effect.Push; i++)
            {
                var next = new Cell(to.X + dx, to.Y + dy);

                if (!fight.Field.Map.IsPassable(next) ||
                    (dx == 0 || dy == 0) && !fight.Field.Map.CanCross(to, next))
                    break;

                if (fight.Field.Occupies(next, target)) break;

                to = next;
            }

            bool pushed = to != at && fight.Shove(target, to);

            return new Landing(effect, target, pushed, 0, c.Attempt);
        }

        // Telekinesis: a creature moved to the aimed square, as far as the effect's length
        static Landing Move(Contact c)
        {
            SpellEffect effect = c.Effect;
            Actor target = c.Target;
            Encounter fight = c.Fight;

            if (c.Resisted) return new Landing(effect, target, false, 0, c.Attempt);

            if (fight == null || !c.Aim.Square.HasValue)
                return new Landing(effect, target, c.Landed, 0, c.Attempt);

            Cell to = c.Aim.Square.Value;
            Cell? from = fight.Field.Where(target);
            Cell? here = fight.Field.Where(c.Caster.Actor);

            bool fits = from.HasValue && here.HasValue &&
                        Battlefield.Distance(from.Value, to) <= Math.Max(1, effect.Length) &&
                        Battlefield.Distance(here.Value, to) <= c.Spell.RangeAt(c.Caster.Actor.Level);

            bool moved = fits && fight.Shove(target, to);

            return new Landing(effect, target, moved, 0, c.Attempt);
        }
    }
}
