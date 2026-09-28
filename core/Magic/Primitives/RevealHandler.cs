using System;
using System.Collections.Generic;

namespace Core.Magic
{
    // REVEAL: information - Detect Magic, True Seeing, Identify. answers a question, changes nothing
    public sealed class RevealHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Reveal;

        public IReadOnlyList<string> Keys { get; } = Array.Empty<string>();

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel) => Array.Empty<string>();

        public Landing Apply(Contact c) => c.Report(c.Landed);
    }
}
