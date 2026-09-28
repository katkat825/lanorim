using System.Collections.Generic;

namespace Core.Magic
{
    // ONE PRIMITIVE, IN ONE PLACE: the keys it takes, the rules its data has to obey, and what it
    // does when it lands. Incantation.Apply makes the checks every primitive shares (a Globe in the
    // way, an attack roll or a save) and hands the rest to the primitive's handler; SpellReader
    // refuses a key the primitive's handler doesn't take, which is what stops a setting from loading
    // on the wrong primitive and quietly doing nothing (cc_task_dedupe-effects.md, Phase 5)
    public interface IPrimitiveHandler
    {
        Primitive Kind { get; }

        // the keys this primitive takes beyond the ones every effect may carry (SpellReader's
        // common keys): its own settings, and the BoonSpec or LingerSpec keys it uses
        IReadOnlyList<string> Keys { get; }

        // what an effect of this primitive has to say, and must not say, in engineer's English
        IEnumerable<string> Check(SpellEffect effect, int spellLevel);

        // what it does to one creature, the shared checks already made
        Landing Apply(Contact contact);
    }
}
