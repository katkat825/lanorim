using System;
using System.Collections.Generic;
using Core.Combat;

namespace Core.Magic
{
    // COUNTER: stops a spell while it is still being cast, so it never takes effect: Counterspell.
    // only the casting being answered can be countered
    public sealed class CounterHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Counter;

        public IReadOnlyList<string> Keys { get; } = Array.Empty<string>();

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel) => Array.Empty<string>();

        public Landing Apply(Contact c)
        {
            // only a casting can be countered, and only the one being answered. anywhere else this
            // is a spell with nothing in front of it
            bool casting = c.Answering != null && c.Answering.Trigger == Trigger.Cast &&
                           ReferenceEquals(c.Answering.Source, c.Target);

            if (!casting) return new Landing(c.Effect, c.Target, false, 0, c.Attempt);

            if (c.Landed) c.Answering.Stop();

            return new Landing(c.Effect, c.Target, c.Landed, 0, c.Attempt);
        }
    }
}
