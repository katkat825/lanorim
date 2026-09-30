namespace Core.Combat
{
    // a number on a creature that a spell can change and the table says it changed: "your Armor Class
    // is now 14". Hit points going down are said by the damage line (ICombatObserver.Dealt), so a
    // HitPoints change is only told when they go up
    public enum Stat
    {
        ArmorClass,

        HitPoints,

        TemporaryHitPoints,

        MaxHitPoints,

        Speed,
    }
}
