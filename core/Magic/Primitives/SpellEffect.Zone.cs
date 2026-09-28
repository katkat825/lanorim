using Core.Combat;
using Core.Space;

namespace Core.Magic
{
    // the settings only a zone reads. ZoneHandler declares their keys
    public sealed partial class SpellEffect
    {
        // difficult terrain inside it
        public bool Rough { get; init; }

        // on the ground, which a flyer passes over: Grease, Spike Growth
        public bool Ground { get; init; }

        // it acts every time, not once per turn - Wall of Fire
        public bool EachTime { get; init; }

        // cover to a creature behind it: Blade Barrier's three-quarters (+5)
        public int Cover { get; init; }

        // its outline becomes walls on the board - "bars" (seen and shot through, never walked
        // through) or "solid" - Forcecage's cage and box
        public Edge Encloses { get; init; }

        // for a wall: how many squares across its ring is (the block it closes round); 0 is a
        // wall that can only be straight
        public int RingSize { get; init; }

        // for a wall: how far beside it, on the side the caster picks, it still reaches -
        // Wall of Fire's 10 feet
        public int Beside { get; init; }

        // it moves this many squares straight away from its caster at the start of the caster's
        // turn - Cloudkill's 10 feet
        public int Drifts { get; init; }

        // what it does to sight inside it: Fog Cloud's heavy, Darkness's magical darkness
        public Obscurement Obscures { get; init; }

        // spells of this level or lower cast from outside cannot affect anything inside - Globe of
        // Invulnerability's 5 (its upcast says how much higher per slot level)
        public int BlocksSpellsUpTo { get; init; }

        // centred on the caster's square wherever the aim was - Call Lightning's cloud above you,
        // the Globe of Invulnerability
        public bool OnCaster { get; init; }

        // it has to be put down on an empty square - Flaming Sphere
        public bool Unoccupied { get; init; }
    }
}
