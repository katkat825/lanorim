using System.Collections.Generic;
using System.Linq;
using Core.Words;

namespace Core.Characters
{
    // what a creature's tag does to an effect: Hold Person only on a Humanoid, Sleep never on a
    // sleepless elf, Flesh to Stone saved against by a Construct without rolling. one list of these
    // where there were five tag lists (cc_task_dedupe-effects.md, 3a #9)
    [Fallback(TagOutcome.Untouched)]
    public enum TagOutcome
    {
        // only creatures with one of the "only" tags are touched; anything else is as if it saved
        Only,

        // a creature with it is untouched: no save, nothing lands
        Untouched,

        // it succeeds on the save without rolling
        AutoSave,

        // it fails the save without rolling
        AutoFail,

        // it makes the save with disadvantage
        SaveDisadvantage,
    }

    public sealed record TagRule(string Tag, TagOutcome Outcome);

    public static class TagRules
    {
        // whether the rules let the effect touch this creature at all. "only" is the one rule that
        // lets through rather than stops: with any "only" rule, one of those tags has to match
        public static bool Touch(this IReadOnlyList<TagRule> rules, Actor target)
        {
            if (rules.Count == 0) return true;

            List<TagRule> only = rules.Where(r => r.Outcome == TagOutcome.Only).ToList();

            if (only.Count > 0 && !only.Any(r => target.Is(r.Tag))) return false;

            return !rules.Any(r => r.Outcome == TagOutcome.Untouched && target.Is(r.Tag));
        }

        // whether one of the creature's tags has this outcome for it
        public static bool Give(this IReadOnlyList<TagRule> rules, Actor target, TagOutcome outcome) =>
            rules.Any(r => r.Outcome == outcome && target.Is(r.Tag));
    }
}
