using Core.Characters;

namespace Core.Magic
{
    // THE SAVE TRACK: a save made again to end it. at the end of each of the creature's turns - or,
    // on damage that burns each turn, right after the burn (Searing Smite). one record where there
    // were seven fields: repeat_save and end_save (the same idea: a named ability, or the effect's
    // own save), worsens, worsens_after, ends_after and repeat_unseen (cc_task_dedupe-effects.md,
    // 3a #2 and 3b)
    public sealed record RepeatSave(Ability Ability)
    {
        // how many successes end it: Flesh to Stone's three. SRD's usual is one
        public int EndsAfter { get; init; } = 1;

        // enough failures swap the condition for this one and the saving stops: Sleep's
        // Unconscious, Flesh to Stone's Petrified. the save that put it there counts as the first
        public Condition Worsens { get; init; }

        public int WorsensAfter { get; init; } = 1;

        // only when the creature ends its turn out of the caster's sight: Fear
        public bool OnlyUnseen { get; init; }
    }
}
