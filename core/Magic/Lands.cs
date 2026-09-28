namespace Core.Magic
{
    // WHEN an effect lands. one word where there were five flags - delayed, recurs, on_end, repeats,
    // repeat_only (cc_task_dedupe-effects.md, 3a #3)
    public enum Lands
    {
        // when the spell is cast
        Now,

        // at the end of the target's next turn instead of now: Vitriolic Sphere's second splash
        NextTurnEnd,

        // at the start of each of the target's turns while it lasts: Searing Smite's burning
        EachTurn,

        // when the spell ends on the creature: Haste's lethargy
        OnEnd,

        // when cast, and again each time the caster spends the spell's repeat on a later turn:
        // Spiritual Weapon's swing, Hex moving
        NowAndOnRepeat,

        // only on a repeat, never on the cast itself: Produce Flame's hurl
        OnRepeat,
    }

    public static class Landings
    {
        // done again on a later turn, without paying again
        public static bool Repeats(this Lands lands) => lands == Lands.NowAndOnRepeat || lands == Lands.OnRepeat;
    }
}
