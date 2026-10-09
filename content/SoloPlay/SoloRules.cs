using System;

namespace Content.SoloPlay
{
    // THE SOLO DELTAS, EACH BEHIND ITS OWN SWITCH (cc_task_h Part 3; docs/decisions_checklist.md §1). One hero
    // instead of a party, and Kathleen's call that "the melee classes are already well speced for solo play, so we
    // should focus more on the casters". Every switch is on in the game; the sims turn them off one at a time to
    // compare (`sim classes 200 --solo none`), and so can Kathleen, here. A test that flips one runs in the
    // "solo rules" collection, alone, so it can't change another test's rules under it.
    public static class SoloRules
    {
        // 3.1: a Mage, Cleric and Druid (full casters; not the Paladin) have one more 1st-level slot at every level,
        // or a 1st-level spell's cost more in points
        public static bool CasterSlot { get; set; } = true;

        // 3.2: every hero carries a keepsake that stands them back up the first time they drop to 0 each long rest
        public static bool Keepsake { get; set; } = true;

        // 3.3: the player's difficulty, which only the enemies feel. Set from the game's settings
        public static PlayDifficulty Difficulty { get; set; } = PlayDifficulty.Standard;

        // the extra 1st-level slots a caster of this progression gets
        public static int ExtraFirstLevelSlots(bool fullCaster) => CasterSlot && fullCaster ? 1 : 0;

        // THE DIFFICULTY TABLE, as percentages: enemy hit points and enemy damage. Settled from the sims
        // (RUN_LOG_2026-10-08_h.md): Story is gentler on both, Hard is longer fights at the same damage
        public static int EnemyHitPoints(PlayDifficulty difficulty) => difficulty switch
        {
            PlayDifficulty.Story => 75,
            PlayDifficulty.Hard => 125,
            _ => 100,
        };

        public static int EnemyDamage(PlayDifficulty difficulty) => difficulty switch
        {
            PlayDifficulty.Story => 75,
            _ => 100,
        };

    }
}
