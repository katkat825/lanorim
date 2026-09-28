using System.Collections.Generic;

namespace Core.Magic
{
    // DISPEL: ends another spell already in place - Dispel Magic by level, Remove Curse on every
    // curse. aimed at a square it ends what holds the square (Incantation.DispelAt)
    public sealed class DispelHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Dispel;

        public IReadOnlyList<string> Keys { get; } = new[] { "curses", "ends_force" };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.EndsForce && effect.AimKind != AimKind.Place)
                yield return "'ends_force' ends a creation of force at a square - 'aim': 'place'";
        }

        public Landing Apply(Contact c)
        {
            int ended = c.Effect.Curses ? c.Magic.Uncurse(c.Target) : c.Magic.Dispel(c.Caster, c.Target, c.CastAt);

            return new Landing(c.Effect, c.Target, ended > 0, ended);
        }
    }
}
