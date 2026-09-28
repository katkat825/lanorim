namespace Core.Magic
{
    // what a conjure makes, and how many: Goodberry's ten berries. the pair item + count, as one
    // record (cc_task_dedupe-effects.md, 3b)
    public sealed record ConjuredItem(string Id, int Count);
}
