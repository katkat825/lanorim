namespace Core.Magic
{
    // a creature killed by it rises as this statblock at the start of the caster's next turn, on the
    // caster's side - only one its tag rules let through, when it has any: Finger of Death's
    // Humanoid and Zombie. the pair raises_as + raises_tag, as one record (cc_task_dedupe-effects.md,
    // 3b); the tag as an 'only' tag rule, the effect's words (cc_task_dedupe-leftovers.md #13)
    public sealed record Raises(string As, System.Collections.Generic.IReadOnlyList<Core.Characters.TagRule> TagRules);
}
