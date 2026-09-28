using System;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // the moments a zone does something to a creature. SRD 5.2.1's persistent spells all use the
    // same handful of words - "when the area appears", "when a creature enters it for the first
    // time on a turn", "starts its turn there", "ends its turn there", "for every 5 feet it
    // travels there" - so those are the whole list. a zone's are several bits; the moment that is
    // happening, handed to IZone.Act, is one bit set (there was a second enum, Pulse, for that, and a
    // bridge between the two: cc_task_dedupe-leftovers.md #10)
    [Flags]
    public enum Pulses
    {
        None = 0,

        // when the zone is made, to whoever is already in it (Moonbeam, Cloudkill)
        Appear = 1 << 0,

        // stepping in, or the zone moving onto you - once per turn per zone (Spirit Guardians)
        Enter = 1 << 1,

        StartTurn = 1 << 2,

        EndTurn = 1 << 3,

        // every square moved inside it (Spike Growth)
        EachSquare = 1 << 4,

        // the zone itself was moved into the creature's square (Flaming Sphere rolled into it)
        Ram = 1 << 5,
    }

    // what a zone does to sight. SRD 5.2.1: a lightly obscured area gives disadvantage on
    // Wisdom (Perception) checks that rely on sight; a heavily obscured one blocks vision entirely
    public enum Obscurement
    {
        None,
        Light,
        Heavy,

        // heavily obscured, and magical: Truesight sees through it, Darkvision does not - Darkness.
        // a third value where there used to be a flag beside 'heavy' (cc_task_dedupe-effects.md,
        // 3a #11)
        MagicalDarkness,
    }

    public static class Obscurements
    {
        // nothing is seen through it: heavy, or magical darkness
        public static bool BlocksSight(this Obscurement obscures) =>
            obscures == Obscurement.Heavy || obscures == Obscurement.MagicalDarkness;
    }

    // A PERSISTENT AREA ON THE BOARD - the zone primitive of v1_spell_list.md, which until now was
    // only a marker. the fight owns where it is and when it fires; what it DOES is the zone's own
    // business (a spell's, in practice), which is why this is an interface and the fight never
    // looks inside it.
    public interface IZone
    {
        // the spell or feature it came from; ending that ends the zone
        string Source { get; }

        Actor Owner { get; }

        // the moments it acts on
        Pulses Pulses { get; }

        // difficult terrain inside it: every square costs double to walk into
        bool Rough { get; }

        // it is on the ground - grease, spikes, grasping weeds: a flyer passes over it untouched
        bool Ground => false;

        // which creatures it touches: its owner's foes ("creatures of your choice", Spirit
        // Guardians), its owner's side (Pass without Trace), or everybody. the area's own word,
        // where the zone used to carry two flags (cc_task_dedupe-leftovers.md #5)
        Affects Affects { get; }

        // what it does to sight: fog, sleet, magical darkness (heavy, and Truesight sees
        // through it) - the area's own value, where magical darkness used to come back as heavy
        // plus a flag
        Obscurement Obscures { get; }

        // spells of this level or lower cast from outside it cannot touch anything inside.
        // 0 is a zone that blocks nothing
        int BlocksSpellsUpTo { get; }

        // the squares it covers right now. an emanation follows its owner, so this is asked, not
        // stored
        bool Covers(Battlefield field, Cell cell);

        // the squares the thing itself stands in, for a zone that also reaches beside itself: a
        // Wall of Fire's wall, not the ten feet it burns on one side. everything else is Covers
        bool Core(Battlefield field, Cell cell) => Covers(field, cell);

        // armor class it gives a creature behind it from an attack through it: Blade Barrier's
        // three-quarters cover, +5
        int Cover => 0;

        // the fight is taking it off the board
        void Removed(Encounter fight) { }

        // SRD's "once per turn" does not hold for it: it acts at every moment it names
        bool EachTime => false;

        void Act(Encounter fight, Actor creature, Pulses pulse);
    }
}
