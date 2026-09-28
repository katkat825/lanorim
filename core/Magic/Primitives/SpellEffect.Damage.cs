namespace Core.Magic
{
    // the settings only a damage reads. DamageHandler declares their keys
    public sealed partial class SpellEffect
    {
        // a creature at or below this many hit points dies instead of rolling damage; a Death Ward
        // stops it: Power Word Kill's 100
        public int SlaysAtOrBelow { get; init; }

        // reduced to 0 hit points by it, the creature is dust: dead at once and past any revival
        // v1 has. Disintegrate
        public bool Dust { get; init; }

        // a creature it kills rises on the caster's side: Finger of Death
        public Raises Raises { get; init; }

        // Chromatic Orb: when two of the damage dice match, the effect leaps to another creature
        // within this many squares of the one it hit, once per slot level
        public int Leaps { get; init; }

        // more dice against some creatures, or in some settings: Divine Smite, Call Lightning
        public ExtraDice ExtraDice { get; init; }

        // a failed save turns a shape-shifted creature back, and it can't shift again until it
        // leaves the zone: Moonbeam
        public bool RevertsShape { get; init; }

        // for damage with a save: a creature that fails spends its reaction moving as far as its
        // speed allows away from the caster (Dissonant Whispers)
        public bool ReactionFlee { get; init; }

        // every creature after the first must be within this many squares of the first, and each
        // only once: Chain Lightning's leaps
        public int NearFirst { get; init; }

        // the attack is made from the spell's own zone, against a creature this many squares from
        // it: Spiritual Weapon's force, "a creature within 5 feet of the force"
        public int NearZone { get; init; }

        // the square this effect is aimed at has to be under the spell's zone: Call Lightning's
        // bolts fall only under the cloud
        public bool WithinZone { get; init; }
    }
}
