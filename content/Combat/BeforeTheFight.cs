using System.Collections.Generic;
using System.Linq;
using Content.Sheet;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Magic;
using Core.Resolution;

namespace Content.Combat
{
    // A SPELL CAST BEFORE THE FIGHT (cc_task_ui-issues-9-30.md 5): SRD-faithful help for a squishy
    // caster. A spell that outlasts a fight - Mage Armor's 8 hours, Aid, Death Ward - can be cast on
    // yourself in the moment before the fight begins, paid for as normal. v1 has no surprise, so
    // nothing in the rules or the story stops it (the Referee settles checks, rests and loot; it has
    // no say over a cast). The table offers it at the fight's ready moment; the AutoPlayer and the
    // sim never take it unless asked, so play is unchanged unless the player uses it.
    public static class BeforeTheFight
    {
        // lasts past the end of a fight: to a rest, a long rest, or for good
        static readonly Duration[] Outlasting = { Duration.Rest, Duration.LongRest, Duration.Permanent };

        // what the hero can put on themselves now that outlasts a fight and isn't on them already:
        // a kindly spell (a ward, a boon with no save) aimed at the caster or a creature, not held in
        // concentration, and paid for at its own level
        public static IReadOnlyList<Spell> Offered(Hero hero)
        {
            Caster caster = hero?.Caster;

            if (caster == null || hero.Actor.IsShifted) return new List<Spell>();

            return caster.Known
                         .Where(s => !s.IsCantrip && !s.Answers && !s.Concentration)
                         .Where(s => Outlasting.Contains(SpellCard.Of(s).Lasts))
                         .Where(s => s.Effects.All(e => e.AimKind is AimKind.Caster or AimKind.Creature or AimKind.Creatures))
                         .Where(CombatSession.Kindly)
                         .Where(s => caster.CanCast(s, s.Level))
                         .Where(s => !hero.Actor.Boons.Has(s.Id))
                         .OrderBy(s => s.Level).ThenBy(s => s.Id)
                         .ToList();
        }

        // cast it on yourself, at its own level, outside any fight; and what it changed on you
        public static Casting Cast(Hero hero, Spell spell, IResolver resolver, out IReadOnlyList<Change> changed)
        {
            Dictionary<(Actor, Stat), int> before = Change.Snapshot(new[] { hero.Actor });

            Casting casting = new Incantation(resolver).Cast(hero.Caster, spell, Aim.At(hero.Actor), spell.Level);

            changed = Change.Since(before);
            return casting;
        }
    }
}
