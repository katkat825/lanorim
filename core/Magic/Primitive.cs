using System.Collections.Generic;
using Core.Words;

namespace Core.Magic
{
    // the effect primitives. 131 spells are composed from these rather than each being its own
    // piece of code - decisions_checklist.md section 6 calls spell effects the number one cost and
    // this closed list is the cheap path out of it.
    //
    // closed on purpose: an open string here would be a scripting hook, and content is data, not
    // code. a spell that needs something not on this list is either an approximation (the eight
    // flagged in v1_spell_list.md) or campaign narrative.
    [Fallback(Primitive.Narrate)]
    public enum Primitive
    {
        // hit points off, by damage type
        Damage,

        // hit points on, never past the maximum
        Heal,

        // a second pool that soaks damage first and does not stack
        Ward,

        // one of the six v1 conditions, for a duration
        Afflict,

        // takes a condition away - Lesser Restoration, Greater Restoration
        Relieve,

        // a numeric shift to a roll: Bless, Bane, Guidance, Shield's +5 AC
        Sway,

        // teleport, push, pull - anything that changes which square something is on
        Shift,

        // an area that persists and does something to whoever is in it: Web, Spirit Guardians,
        // Wall of Fire
        Zone,

        // a square the light reaches, or takes away
        Illuminate,

        // information: Detect Magic, True Seeing, Identify. answers a question, changes nothing
        Reveal,

        // ends another effect that is already in place: Dispel Magic
        Dispel,

        // stops a spell while it is still being cast, so it never takes effect: Counterspell.
        // kept apart from Dispel because the two land on different things - Dispel on what a
        // creature is holding up, Counter on the casting in front of it - and only a reaction to
        // a cast has a casting in front of it
        Counter,

        // a creature the caster did not have a moment ago. v1 summons are fixed archetypes
        Summon,

        // a creature at 0 hit points stops dying: SRD 5.2.1's Stable. Spare the Dying
        Stabilize,

        // one attack with a weapon the caster holds, made by the spell: True Strike
        Strike,

        // the creature's next turn is decided for it, from a closed list of words: Command
        Direct,

        // what the creature holds is taken from it: Telekinesis pulling a weapon away
        Disarm,

        // items that were not there a moment ago, into the caster's hands: Goodberry's ten
        // berries. core names the item; the content layer puts it in the pack
        Conjure,

        // nothing mechanical - the narrator handles it, and the campaign decides what that means.
        // Prestidigitation, Disguise Self, Minor Illusion
        Narrate,
    }

    // who or what an effect lands on: its 'aim' in the data. it was 'reach', which is also an
    // attack's reach in squares - SRD's word for that (cc_task_dedupe-leftovers.md #13). Aim is
    // the aim taken at the cast; this is the kind of aim an effect wants
    [Fallback(AimKind.Creature)]
    public enum AimKind
    {
        // the caster, and only the caster
        Caster,

        // one creature the caster picks
        Creature,

        // several creatures the caster picks, up to Targets
        Creatures,

        // everything in a burst centred on a square
        Burst,

        // everything in a burst centred on the caster
        Around,

        // a square, not a creature: a wall, a light, a zone
        Place,

        // everything in a line coming out of the caster: Lightning Bolt, Sunbeam
        Line,

        // everything in a cone coming out of the caster: Burning Hands, Cone of Cold
        Cone,

        // everything in a cube against the caster's own square: Thunderwave
        Cube,

        // everything in a square area put down on the aimed square: Web, Faerie Fire
        Square,

        // a wall of squares put down within range: a straight run from the aimed square in the
        // aimed facing, or a ring round a block with the aimed square at its corner - Wall of Fire,
        // Blade Barrier, Forcecage. only a zone takes this shape
        Wall,

        // whoever the spell's zone is acting on, when it acts - an effect with this reach does
        // nothing when the spell is cast, only when its zone pulses (Spirit Guardians' damage)
        Zone,
    }


    // what a successful saving throw does about it
    public enum OnSave
    {
        // there is no save
        None,

        // half the damage on a successful save. Nothing else on the effect lands on a success - a
        // condition, a sway, a push: Contact.Resisted is true for Half as for Negates, so AfflictHandler
        // gives no condition (the comment here used to say it did; checked 2026-10-03,
        // cc_task_open-questions-answers.md 1.4). A spell that halves its damage and still imposes a
        // condition on a success would give the condition to its own afflict with no save; SRD 5.2.1 has
        // none in v1. One that halves and spares the condition gives it to an afflict with 'same_save'
        // and 'negates' (Sunbeam, Sunburst). Beware 'follows' after a Half: it lands where the damage
        // landed, which is on a success too
        Half,

        // nothing at all happens: the effect doesn't land on a successful save
        Negates,

        // the effect lands only on a SUCCESSFUL save: Flesh to Stone's "on a successful save, its
        // Speed is 0"
        OnSuccess,
    }

    public static class Primitives
    {
        public static readonly IReadOnlyList<Primitive> All = new[]
        {
            Primitive.Damage, Primitive.Heal, Primitive.Ward, Primitive.Afflict,
            Primitive.Relieve, Primitive.Sway, Primitive.Shift, Primitive.Zone,
            Primitive.Illuminate, Primitive.Reveal, Primitive.Dispel, Primitive.Counter,
            Primitive.Summon, Primitive.Stabilize, Primitive.Strike, Primitive.Direct,
            Primitive.Disarm, Primitive.Conjure, Primitive.Narrate,
        };


        // an effect that lands on more than one creature at a time
        public static bool IsArea(this AimKind aim) =>
            aim == AimKind.Burst || aim == AimKind.Around || aim == AimKind.Square ||
            aim.IsDirected();

        // a shape that comes out of the caster and has to be pointed somewhere
        public static bool IsDirected(this AimKind aim) =>
            aim == AimKind.Line || aim == AimKind.Cone || aim == AimKind.Cube;

        public static bool NeedsATargetSquare(this AimKind aim) =>
            aim == AimKind.Burst || aim == AimKind.Place || aim == AimKind.Square ||
            aim == AimKind.Wall;

    }
}
