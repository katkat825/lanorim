namespace Content.SoloPlay
{
    // HOW HARD THE FIGHTS ARE (cc_task_h 3.3): the player's setting, changeable at any time. It touches only the
    // enemies (SoloRules.EnemyHitPoints, EnemyDamage); the hero's rules never change. Standard is the game exactly
    // as it was before the setting existed
    public enum PlayDifficulty
    {
        Story,
        Standard,
        Hard,
    }
}
