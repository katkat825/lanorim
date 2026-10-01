using System.Collections.Generic;
using Core.Characters;

namespace Core.Magic
{
    // ONE PRIMITIVE, IN ONE PLACE: the keys it takes, the rules its data has to obey, and what it
    // does when it lands. Incantation.Apply makes the checks every primitive shares (a Globe in the
    // way, an attack roll or a save) and hands the rest to the primitive's handler; SpellReader
    // refuses a key the primitive's handler doesn't take, which is what stops a setting from loading
    // on the wrong primitive and quietly doing nothing (cc_task_dedupe-effects.md, Phase 5)
    public interface IPrimitiveHandler
    {
        Primitive Kind { get; }

        // the keys this primitive takes beyond the ones every effect may carry (SpellReader's
        // common keys): its own settings, and the BoonSpec or LingerSpec keys it uses
        IReadOnlyList<string> Keys { get; }

        // what an effect of this primitive has to say, and must not say, in engineer's English
        IEnumerable<string> Check(SpellEffect effect, int spellLevel);

        // what it does to one creature, the shared checks already made
        Landing Apply(Contact contact);

        // --- what play asks of it (cc_task_d-seams-and-duplication.md §4-5), instead of its Kind ---

        // THIS EFFECT ONLY HELPS the creature it lands on: a heal, a ward, Invisibility, a boon only the
        // unwilling save against. Spell.Kindly asks it of every effect: Charmed lets nothing else at the
        // charmer, and a spell whose effects all help is aimed at friends. the default is "it may harm"
        bool Helps(SpellEffect effect) => false;

        // a spell that does this at all is a kind one, whatever else it does: Raise Dead's penalty is
        // the price of the gift, and Greater Restoration's curse mode goes on a friend like the rest
        bool KindWhateverElse => false;

        // IT IS THE SPELL'S ZONE: it goes on the fight and stays (MakeZone). only a zone is put down
        // as a wall, sorts who counts with 'affects' wherever it is aimed, or grows an upcast radius;
        // one to a spell, or to each mode
        bool MakesAZone => false;

        // it is put on squares, sized by a radius: a zone, a light
        bool CoversGround => MakesAZone;

        // aimed at the zone on a repeat, it moves the zone (Moonbeam's beam, a Flaming Sphere rolled)
        // rather than acting through its pulses
        bool MovesTheZone(SpellEffect effect) => false;

        // the player picks a square for it though it is not aimed at one: a teleport's landing, the
        // zone's new place
        bool AimsAtASquare(SpellEffect effect) => false;

        // it lasts on whoever stands in the zone, with no pulse: an aura (Pass without Trace)
        bool LastsWhileInTheZone(SpellEffect effect) => false;

        // the condition it puts on the creature it lands on, if any: Fey Ancestry's advantage on the
        // save is against this, and a monster doesn't waste one on a creature that has it
        Condition? Inflicts(SpellEffect effect) => null;

        // its data is a boon, whole (BoonSpec, read by BoonSpecReader): it takes every boon key
        bool CarriesABoon => false;

        // a common setting (SpellReader.CommonKeys) only some primitives may carry, by its vocabulary
        // word: "add_modifier" (damage, heal), the lands "each_turn" and "next_turn_end" (damage)
        bool Allows(string setting) => false;
    }
}
