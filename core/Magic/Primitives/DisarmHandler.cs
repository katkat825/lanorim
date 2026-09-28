using System;
using System.Collections.Generic;

namespace Core.Magic
{
    // DISARM: what the creature holds is taken from it - Telekinesis pulling a weapon away
    public sealed class DisarmHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Disarm;

        public IReadOnlyList<string> Keys { get; } = Array.Empty<string>();

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel) => Array.Empty<string>();

        public Landing Apply(Contact c)
        {
            if (c.Resisted) return new Landing(c.Effect, c.Target, false, 0, c.Attempt);

            c.Target.Disarm(c.Aim.Square ?? c.Fight?.Field.Where(c.Target));

            return new Landing(c.Effect, c.Target, true, 0, c.Attempt);
        }
    }
}
