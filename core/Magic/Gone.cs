using System.Collections.Generic;

namespace Core.Magic
{
    // held this many rounds, a banished creature with one of these tags doesn't come back:
    // Banishment's minute on an Aberration, Celestial, Elemental, Fey or Fiend. the pair
    // gone_after_rounds + gone_tags, as one record (cc_task_dedupe-effects.md, 3b); its tags are
    // 'only' tag rules, the effect's words (cc_task_dedupe-leftovers.md #13)
    public sealed record Gone(int AfterRounds, IReadOnlyList<Core.Characters.TagRule> TagRules);
}
