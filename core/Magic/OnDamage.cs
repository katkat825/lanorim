using Core.Words;

namespace Core.Magic
{
    // what taking damage does to what a spell left on a creature. one word where there were three -
    // ends_on_damage, ends_at_zero and save_on_damage (cc_task_dedupe-effects.md, 3a #7)
    public enum OnDamage
    {
        Nothing,

        // any damage ends it: Sleep, Hypnotic Pattern
        Ends,

        // damage from the caster or the caster's allies ends it: Charm Person
        [Word("ends_if_caster_side")] EndsIfCastersSide,

        // it ends when the bearer drops to 0 hit points: Gaseous Form
        EndsAtZero,

        // damage calls for the repeat save, with advantage: Hideous Laughter
        SavesAgain,
    }
}
