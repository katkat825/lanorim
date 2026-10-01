using System;
using System.Collections.Generic;

namespace Core.Magic
{
    // NARRATE: nothing mechanical - the narrator handles it, and the campaign decides what that means.
    // Prestidigitation, Disguise Self, Minor Illusion
    public sealed class NarrateHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Narrate;

        public bool Helps(SpellEffect effect) => true;

        public IReadOnlyList<string> Keys { get; } = Array.Empty<string>();

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel) => Array.Empty<string>();

        public Landing Apply(Contact c) => c.Report(c.Landed);
    }
}
