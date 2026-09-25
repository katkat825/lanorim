using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // the moments a zone does something to a creature. SRD 5.2.1's persistent spells all use the
    // same handful of words - "when the area appears", "when a creature enters it for the first
    // time on a turn", "starts its turn there", "ends its turn there", "for every 5 feet it
    // travels there" - so those are the whole list.
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

    // which moment a pulse is. a single value of Pulses, named for the call
    public enum Pulse
    {
        Appear,
        Enter,
        StartTurn,
        EndTurn,
        EachSquare,
        Ram,
    }

    // what a zone does to sight. SRD 5.2.1: a lightly obscured area gives disadvantage on
    // Wisdom (Perception) checks that rely on sight; a heavily obscured one blocks vision entirely
    public enum Obscurement
    {
        None,
        Light,
        Heavy,
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

        // the owner's side is left alone - "creatures of your choice", which in a solo game is
        // everybody on the other side
        bool SparesAllies { get; }

        // what it does to sight: fog, sleet, magical darkness
        Obscurement Obscures { get; }

        // magical darkness: Darkvision does not see through it, Truesight does
        bool Magical { get; }

        // only the owner's side - an aura for friends (Pass without Trace)
        bool AlliesOnly { get; }

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

        void Act(Encounter fight, Actor creature, Pulse pulse);
    }

    public static class ZonePulses
    {
        public static bool Has(this Pulses pulses, Pulse pulse) =>
            (pulses & (Pulses)(1 << (int)pulse)) != 0;

        static readonly (Pulses pulse, string id)[] Ids =
        {
            (Pulses.Appear, "appear"),
            (Pulses.Enter, "enter"),
            (Pulses.StartTurn, "start_turn"),
            (Pulses.EndTurn, "end_turn"),
            (Pulses.EachSquare, "each_square"),
            (Pulses.Ram, "ram"),
        };

        public static string Id(this Pulses pulses) =>
            pulses == Pulses.None
                ? "none"
                : string.Join("|", Ids.Where(p => (pulses & p.pulse) != 0).Select(p => p.id));

        public static bool TryParse(string id, out Pulses pulses)
        {
            pulses = Pulses.None;

            if (string.IsNullOrWhiteSpace(id) || id == "none") return true;

            foreach (string part in id.Split('|'))
            {
                string wanted = part.Trim().ToLowerInvariant();
                (Pulses pulse, string id) match = Ids.FirstOrDefault(p => p.id == wanted);

                if (match.id == null) return false;

                pulses |= match.pulse;
            }

            return true;
        }
    }
}
