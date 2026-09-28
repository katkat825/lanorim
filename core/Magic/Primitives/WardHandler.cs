using System;
using System.Collections.Generic;

namespace Core.Magic
{
    // WARD: temporary hit points, a second pool that soaks damage first and does not stack
    public sealed class WardHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Ward;

        public IReadOnlyList<string> Keys { get; } = Array.Empty<string>();

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.Amount.IsNothing) yield return "a ward effect with no 'amount'";
        }

        public Landing Apply(Contact c)
        {
            int ward = Math.Max(0, c.Resolver.Roll(c.Amount, c.Caster.Actor));

            c.Target.Health.GrantTemporary(ward);

            return new Landing(c.Effect, c.Target, ward > 0, ward);
        }
    }
}
