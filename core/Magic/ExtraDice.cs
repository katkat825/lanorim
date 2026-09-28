using System.Collections.Generic;
using Core.Characters;
using Core.Dice;

namespace Core.Magic
{
    // more damage dice when something is true: against a creature with one of these tags (Divine
    // Smite's fiends and undead, as 'only' tag rules), or when the fight's setting has this tag
    // (Call Lightning in a storm). one record where there were two pairs - extra_against +
    // extra_amount and bonus_if + bonus_amount (cc_task_dedupe-effects.md, 3b); its tags are the
    // effect's tag_rules words, where they were 'against' (cc_task_dedupe-leftovers.md #13).
    // doubled on a critical like the hit's own dice
    public sealed record ExtraDice(DiceRoll Dice, IReadOnlyList<TagRule> TagRules, string Setting)
    {
        public bool Applies(Actor target, ISet<string> setting) =>
            TagRules.Count > 0 ? TagRules.Give(target, TagOutcome.Only)
                               : !string.IsNullOrEmpty(Setting) && setting != null && setting.Contains(Setting);
    }
}
