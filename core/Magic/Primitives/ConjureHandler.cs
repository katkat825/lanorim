using System;
using System.Collections.Generic;

namespace Core.Magic
{
    // CONJURE: items that were not there a moment ago, into the caster's hands - Goodberry's ten
    // berries. core names the item; the content layer puts it in the pack (Casting.Conjured)
    public sealed class ConjureHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Conjure;

        public IReadOnlyList<string> Keys { get; } = new[] { "item" };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.Item == null || string.IsNullOrEmpty(effect.Item.Id))
                yield return "a conjure names its 'item': {\"id\": ..., \"count\": ...}";
        }

        public Landing Apply(Contact c) => new Landing(c.Effect, c.Target, true, Math.Max(1, c.Effect.Item.Count));
    }
}
