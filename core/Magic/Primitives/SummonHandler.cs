using System;
using System.Collections.Generic;

namespace Core.Magic
{
    // SUMMON: a creature the caster did not have a moment ago. v1 summons are fixed archetypes, which
    // the campaign or the board layer brings
    public sealed class SummonHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Summon;

        public IReadOnlyList<string> Keys { get; } = Array.Empty<string>();

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel) => Array.Empty<string>();

        public Landing Apply(Contact c) => c.Report(c.Landed);
    }
}
