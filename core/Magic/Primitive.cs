using System;
using System.Collections.Generic;
using System.Linq;

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

    // what casting a spell costs out of the turn. SRD's other casting times - a minute, an hour -
    // are rituals and travel, which the campaign narrates rather than the fight counting.
    public enum CastingTime
    {
        Action,

        BonusAction,

        // cast only in answer to a moment the fight offers: Shield when hit, Counterspell when
        // somebody casts. never on your own turn
        Reaction,
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

        // the effect lands only on a SUCCESSFUL save: Flesh to Stone's "on a successful save, its
        // Speed is 0"
        OnSuccess,
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

    // which way a Sway tips a roll rather than how far: advantage and disadvantage, on the
    // bearer's own rolls or on the rolls made at it. Guiding Bolt is advantage against; being
    // Invisible is advantage on your attacks and disadvantage against you.
    [Flags]
    public enum Leans
    {
        None = 0,
        AdvantageOnAttacks = 1 << 0,
        DisadvantageOnAttacks = 1 << 1,
        AdvantageAgainst = 1 << 2,
        DisadvantageAgainst = 1 << 3,
        AdvantageOnChecks = 1 << 4,
        DisadvantageOnChecks = 1 << 5,
        AdvantageOnSaves = 1 << 6,
        DisadvantageOnSaves = 1 << 7,
    }

    // whose turn a turn-shaped duration counts: the creature wearing the effect, or the caster
    // who put it there. Vicious Mockery lasts to the end of the *target's* next turn; Guiding
    // Bolt's glimmer to the end of the *caster's*
    public enum Until
    {
        Bearer,
        Caster,
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
                         Reach.Line, Reach.Cone, Reach.Cube, Reach.Square, Reach.Zone,
                         Reach.Wall,
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
            OnSave.OnSuccess => "on_success",
            _ => "none",
        };

        public static bool TryParse(string id, out OnSave save)
        {
            foreach (OnSave s in new[]
                     { OnSave.None, OnSave.Half, OnSave.Negates, OnSave.KeepsDamage,
                       OnSave.OnSuccess })
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
            reach == Reach.Burst || reach == Reach.Around || reach == Reach.Square ||
            reach.IsDirected();

        // a shape that comes out of the caster and has to be pointed somewhere
        public static bool IsDirected(this Reach reach) =>
            reach == Reach.Line || reach == Reach.Cone || reach == Reach.Cube;

        public static bool NeedsATargetSquare(this Reach reach) =>
            reach == Reach.Burst || reach == Reach.Place || reach == Reach.Square ||
            reach == Reach.Wall;

        static readonly (Leans lean, string id)[] LeanIds =
        {
            (Leans.AdvantageOnAttacks, "advantage_on_attacks"),
            (Leans.DisadvantageOnAttacks, "disadvantage_on_attacks"),
            (Leans.AdvantageAgainst, "advantage_against"),
            (Leans.DisadvantageAgainst, "disadvantage_against"),
            (Leans.AdvantageOnChecks, "advantage_on_checks"),
            (Leans.DisadvantageOnChecks, "disadvantage_on_checks"),
            (Leans.AdvantageOnSaves, "advantage_on_saves"),
            (Leans.DisadvantageOnSaves, "disadvantage_on_saves"),
        };

        public static string Id(this Leans leans) =>
            leans == Leans.None
                ? "none"
                : string.Join("|", LeanIds.Where(l => (leans & l.lean) != 0).Select(l => l.id));

        // pipes again, for the same reason as Sways
        public static bool TryParse(string id, out Leans leans)
        {
            leans = Leans.None;

            if (string.IsNullOrWhiteSpace(id) || id == "none") return true;

            foreach (string part in id.Split('|'))
            {
                string wanted = part.Trim().ToLowerInvariant();
                (Leans lean, string id) match = LeanIds.FirstOrDefault(l => l.id == wanted);

                if (match.id == null) return false;

                leans |= match.lean;
            }

            return true;
        }

        public static bool TryParse(string id, out Until until)
        {
            switch ((id ?? "").Trim().ToLowerInvariant())
            {
                case "":
                case "bearer":
                case "target":
                    until = Until.Bearer;
                    return true;

                case "caster":
                    until = Until.Caster;
                    return true;
            }

            until = Until.Bearer;
            return false;
        }

        public static string Id(this CastingTime time) => time switch
        {
            CastingTime.BonusAction => "bonus_action",
            CastingTime.Reaction => "reaction",
            _ => "action",
        };

        public static bool TryParse(string id, out CastingTime time)
        {
            foreach (CastingTime t in new[]
                     { CastingTime.Action, CastingTime.BonusAction, CastingTime.Reaction })
            {
                if (!string.Equals(t.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                time = t;
                return true;
            }

            time = CastingTime.Action;
            return false;
        }

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
