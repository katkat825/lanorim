using Core.Dice;

namespace Core.Magic
{
    // what casting it one slot level higher adds, per level: more dice, more targets, a wider
    // zone, a stronger globe. one record where there were three loose fields and one rule written
    // only in code - per_extra_level, extra_targets_per_level, radius_per_extra_level, and the
    // Globe's "one higher per slot level" (cc_task_dedupe-effects.md, 3b). Spell.Upcastable reads
    // this, so the spell card offers every spell that grows
    public sealed record Upcast
    {
        public static readonly Upcast Nothing = new Upcast();

        // added to the amount: Cure Wounds' 2d8
        public DiceRoll Amount { get; init; }

        // more picks: Bless's one more creature
        public int Targets { get; init; }

        // squares more radius, for a zone: Fog Cloud's 20 feet
        public int Radius { get; init; }

        // spell levels more that a globe stops: Globe of Invulnerability's one
        public int BlocksSpellsUpTo { get; init; }

        public bool IsNothing => Amount.IsNothing && Targets == 0 && Radius == 0 && BlocksSpellsUpTo == 0;
    }
}
