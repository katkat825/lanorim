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

        // 3.4, Lone Ward (cc_task_cb Part 2; Kathleen 2026-11-16, "if needed then all casters get an additional skill"):
        // the Mage, Cleric and Druid gain temporary hit points equal to proficiency bonus + level, once a long rest.
        // The feature is data (content/srd/features/shared.json, `lone_ward`); this switch says whether a hero has it.
        // On, from the sims of RUN_LOG_2026-11-16_cb.md: with the bot playing a caster as a person would, the Cleric and
        // Druid still missed the level 1 target in the rat cellar (87% / 18% drops, 86.5% / 19%); with the ward all three
        // meet it (Mage 94.5 / 8, Cleric 92.5 / 11, Druid 90.5 / 13)
        public static bool CasterWard { get; set; } = true;

        public const string WardId = "lone_ward";

        // whether a feature the hero's data lists is on under the solo switches: only Lone Ward has one
        public static bool Allows(string featureId) => featureId != WardId || CasterWard;

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
