using System;
using System.Collections.Generic;

namespace Core.Magic
{
    // the effect primitives. 131 spells are composed from these rather than each being its own
    // piece of code - decisions_checklist.md section 6 calls spell effects the number one cost and
    // this closed list is the cheap path out of it.
    //
    // closed on purpose: an open string here would be a scripting hook, and content is data, not
    // code. a spell that needs something not on this list is either an approximation (the eight
    // flagged in v1_spell_list.md) or campaign narrative.
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
        // Wall of Fire. v1 keeps one shape, a burst
        Zone,

        // a square the light reaches, or takes away
        Illuminate,

        // information: Detect Magic, True Seeing, Identify. answers a question, changes nothing
        Reveal,

        // ends another effect: Dispel Magic, Counterspell
        Dispel,

        // a creature the caster did not have a moment ago. v1 summons are fixed archetypes
        Summon,

        // nothing mechanical - the narrator handles it, and the campaign decides what that means.
        // Prestidigitation, Disguise Self, Minor Illusion
        Narrate,
    }

    // who or what an effect lands on
    public enum Reach
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
    }

    // what a successful saving throw does about it
    public enum OnSave
    {
        // there is no save
        None,

        // half the damage, and any condition still lands
        Half,

        // nothing at all happens
        Negates,

        // the damage lands in full but the condition does not
        KeepsDamage,
    }

    // which rolls a Sway touches. a Bless is attacks and saves; a Guidance is one check; a
    // Shield is armor class. declared rather than guessed, so the boon it builds is unambiguous.
    [Flags]
    public enum Sways
    {
        None = 0,
        Attacks = 1 << 0,
        Saves = 1 << 1,
        Checks = 1 << 2,
        Damage = 1 << 3,
        ArmorClass = 1 << 4,
    }

    public static class Primitives
    {
        public static readonly IReadOnlyList<Primitive> All = new[]
        {
            Primitive.Damage, Primitive.Heal, Primitive.Ward, Primitive.Afflict,
            Primitive.Relieve, Primitive.Sway, Primitive.Shift, Primitive.Zone,
            Primitive.Illuminate, Primitive.Reveal, Primitive.Dispel, Primitive.Summon,
            Primitive.Narrate,
        };

        public static string Id(this Primitive primitive) => primitive.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out Primitive primitive)
        {
            foreach (Primitive p in All)
            {
                if (!string.Equals(p.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                primitive = p;
                return true;
            }

            primitive = Primitive.Narrate;
            return false;
        }

        public static string Id(this Reach reach) => reach.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out Reach reach)
        {
            foreach (Reach r in new[]
                     {
                         Reach.Caster, Reach.Creature, Reach.Creatures,
                         Reach.Burst, Reach.Around, Reach.Place,
                     })
            {
                if (!string.Equals(r.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                reach = r;
                return true;
            }

            reach = Reach.Creature;
            return false;
        }

        public static string Id(this OnSave save) => save switch
        {
            OnSave.None => "none",
            OnSave.Half => "half",
            OnSave.Negates => "negates",
            OnSave.KeepsDamage => "keeps_damage",
            _ => "none",
        };

        public static bool TryParse(string id, out OnSave save)
        {
            foreach (OnSave s in new[]
                     { OnSave.None, OnSave.Half, OnSave.Negates, OnSave.KeepsDamage })
            {
                if (!string.Equals(s.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                save = s;
                return true;
            }

            save = OnSave.None;
            return false;
        }

        // an effect that lands on more than one creature at a time
        public static bool IsArea(this Reach reach) =>
            reach == Reach.Burst || reach == Reach.Around;

        public static bool NeedsATargetSquare(this Reach reach) =>
            reach == Reach.Burst || reach == Reach.Place;

        public static string Id(this Sways sways) =>
            sways == Sways.None ? "none" : sways.ToString().ToLowerInvariant().Replace(" ", "");

        // "attacks|saves" - a pipe, because a comma reads as the next field in a data file
        public static bool TryParse(string id, out Sways sways)
        {
            sways = Sways.None;

            if (string.IsNullOrWhiteSpace(id) || id == "none") return true;

            foreach (string part in id.Split('|'))
            {
                switch (part.Trim().ToLowerInvariant())
                {
                    case "attacks": sways |= Sways.Attacks; break;
                    case "saves": sways |= Sways.Saves; break;
                    case "checks": sways |= Sways.Checks; break;
                    case "damage": sways |= Sways.Damage; break;
                    case "armor_class": sways |= Sways.ArmorClass; break;
                    default: return false;
                }
            }

            return true;
        }
    }
}
