namespace Core.Combat
{
    // which creatures in an area or a zone count. one word where there were three flags -
    // spares_allies, spares_caster, allies_only (cc_task_dedupe-effects.md, 3a #1). the "around"
    // reach leaves its caster out whatever this says: an eight-foot fireball in your own lap is a
    // different spell
    public enum Affects
    {
        // everybody in it
        All,

        // everybody but the caster: Entangle's "each creature (other than you)"
        NotCaster,

        // the caster's foes only - SRD's "creatures of your choice", which in a solo game is
        // everybody on the other side: Sleep, Slow, Spirit Guardians
        Foes,

        // the caster's own side only: Pass without Trace's "you and each creature you choose"
        Allies,
    }
}
