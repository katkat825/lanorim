using System;
using System.Collections.Generic;

namespace Core.Magic
{
    // ILLUMINATE: a square the light reaches, or takes away. told, not played, in a fight
    public sealed class IlluminateHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Illuminate;

        public IReadOnlyList<string> Keys { get; } = Array.Empty<string>();

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel) => Array.Empty<string>();

        public Landing Apply(Contact c) => c.Report(c.Landed);
    }
}
