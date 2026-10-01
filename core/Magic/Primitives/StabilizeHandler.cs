using System.Collections.Generic;

namespace Core.Magic
{
    // STABILIZE: a creature at 0 hit points stops dying - SRD 5.2.1's Stable. Spare the Dying
    public sealed class StabilizeHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Stabilize;

        public bool Helps(SpellEffect effect) => true;

        public bool KindWhateverElse => true;

        public IReadOnlyList<string> Keys { get; } = System.Array.Empty<string>();

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.AimKind != AimKind.Creature) yield return "stabilize is for one creature";
        }

        public Landing Apply(Contact c) => new Landing(c.Effect, c.Target, c.Target.Stabilize());
    }
}
