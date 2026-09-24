using System;

namespace Core.Magic
{
    // WHICH WAY THIS CHARACTER PAYS FOR A LEVELED SPELL. Chosen once, at character creation, and
    // kept for the life of the character (decisions_checklist.md section 1).
    //
    // Both modes ride the same cast-at-level engine, so spell EFFECTS and upcasting are shared and
    // identical - what differs is only the accounting and the widget on the sheet. That is the
    // whole reason this is cheap: `Casting` already carries the level a spell is cast at, and the
    // primitives already scale off it.
    public enum SpellResourceMode
    {
        // the SRD default, and what a new character is offered first
        Slots,

        // one pool and a price list. NOT SRD - see SpellPoints for the provenance note
        Points,
    }

    // how much of a day's resources a rest gives back. Short is here because the interface has to
    // be able to say "and this one gives nothing back", which is today's rule for both modes.
    public enum Rest
    {
        Short,

        Long,
    }

    // HOW FAST A CLASS CLIMBS THE SPELL LEVELS. The two standard shapes, named once, so a class
    // data file says which one it is rather than listing twenty rows of slots itself.
    public enum CasterProgression
    {
        None,

        // Mage, Cleric, Druid: 1st-level slots at character level 1, up to 9th
        Full,

        // Paladin: nothing at level 1, slots from level 2, and it never passes 5th
        Half,
    }

    // THE ONE THING `Casting` IS ALLOWED TO ASK. Two questions and a reset, and deliberately
    // nothing else: the moment the caster can see WHICH mode it is holding, every rule downstream
    // grows a branch, and the claim that both modes cast identically stops being structural.
    //
    // `Mode` is on here for the sheet and the save, which genuinely do have to know. Nothing in the
    // rules reads it.
    public interface ISpellResource
    {
        SpellResourceMode Mode { get; }

        // can a spell cast AT THIS LEVEL be paid for right now
        bool CanPay(int castLevel);

        // pay for it. False and nothing is spent - a half-paid cast is not a state that exists
        bool Pay(int castLevel);

        void Restore(Rest rest);

        // the highest level this resource could pay for right now; 0 when only cantrips are left.
        // The sheet asks; so does a tactic deciding whether a spell is worth considering
        int Highest { get; }

        // what the widget on the sheet reads. Developer-facing; the sheet localizes its own labels
        string Describe();
    }

    public static class SpellLevels
    {
        // SRD tops out at 9th, and a cantrip is level 0 and never touches a resource
        public const int Highest = 9;

        public const int Lowest = 1;

        // the level at and above which a spell is a once-a-day event in points mode
        public const int HighLevel = 6;

        public static bool IsLeveled(int level) => level >= Lowest && level <= Highest;

        public static int Clamp(int level) => Math.Clamp(level, 0, Highest);
    }
}
