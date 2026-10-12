using Core.Words;

namespace Core.Combat
{
    // WHAT LOSING A FIGHT MEANS (cc_task_i 3.1). Death is the game as it always was: the hero falls, and the death
    // screen. Continues is the Crucible's: a lost bout goes back to the story ($fight is "lost"), the hero is brought
    // round, and a failed death save is a knockout rather than death. A campaign says it for all its fights
    // ("defeat": "continues" in pack.json) and a fight entry can say it for itself
    public enum Defeat
    {
        // a fight entry that doesn't say: the campaign's
        [Word(""), Unread] Unsaid,

        Death,

        Continues,
    }
}
