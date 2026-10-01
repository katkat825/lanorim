using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Space;

using static Core.Magic.SpellReach;
using static Core.Magic.SpellShapes;

namespace Core.Magic
{
    public sealed partial class Incantation
    {
        // SPIRITUAL WEAPON'S LATER TURNS. the spell is already cast and already paid for; while
        // the caster still holds it, a later turn can spend the spell's repeat - a bonus action
        // for the weapon - to do the effects marked as repeating again. nothing else of the spell
        // happens twice, and nothing is paid twice.
        public Casting Again(Caster caster, Spell spell, Aim aim, Turn turn = null,
                             Encounter fight = null)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));

            if (spell == null || !spell.Repeat.HasValue)
                return Casting.Refused(spell, caster.Actor, spell?.Level ?? 0,
                                       "nothing about that spell repeats");

            if (caster.Actor.Concentrating != spell.Id && !_lasting.Contains((caster.Actor, spell.Id)))
                return Casting.Refused(spell, caster.Actor, spell.Level,
                                       "not holding that spell any more");

            if (spell.MovesWhenDown && MarkStillStanding(caster, spell))
                return Casting.Refused(spell, caster.Actor, spell.Level,
                                       "it moves only once the creature it is on drops to 0 hit points");

            aim ??= Aim.Nothing;

            if (aim.DamageType == DamageType.None &&
                _chosenType.TryGetValue((caster.Actor, spell.Id), out DamageType chosen))
                aim = aim.Choosing(chosen);

            if (fight != null && OutOfReach(caster, spell, aim, fight) is string far)
                return Casting.Refused(spell, caster.Actor, spell.Level, far);

            if (fight != null && UnderTheZone(caster, spell, aim, fight, ZonesOf(caster.Actor)
                    .FirstOrDefault(z => z.Source == spell.Id)) is string outside)
                return Casting.Refused(spell, caster.Actor, spell.Level, outside);

            if (!caster.Actor.CanAct)
                return Casting.Refused(spell, caster.Actor, spell.Level, "cannot act");

            bool bonus = spell.Repeat == Spend.Bonus;

            if (turn != null && !turn.Take(bonus ? Spend.Bonus : Spend.Action))
                return Casting.Refused(spell, caster.Actor, spell.Level,
                                       bonus ? "no bonus action left" : "no action left");

            // the level it was cast at is the level it keeps swinging at
            int castAt = _heldAt.TryGetValue(caster.Actor, out int held) ? held : spell.Level;

            var landings = new List<Landing>();
            var squares = new List<Cell>();
            var covered = new List<Cell>();

            aim ??= Aim.Nothing;

            // a repeating shift that reaches the zone MOVES the zone - Moonbeam's beam, a Flaming
            // Sphere rolled - and whoever it now covers is washed over as if they had walked in
            foreach (SpellEffect move in spell.Effects.Where(e => e.Lands.Repeats() && e.Handler.MovesTheZone(e)))
                if (MoveZone(caster, spell, move, aim, fight) is string refused)
                    return Casting.Refused(spell, caster.Actor, castAt, refused);

            // an aura on the moved zone, and Moonbeam's hold on a creature it turned back, follow it
            if (fight != null) Refresh(fight);

            // Hex, Hunter's Mark: the mark leaves the fallen creature for the new one
            if (spell.MovesWhenDown)
                foreach (Actor was in _placed.Where(p => p.Spell == spell.Id && p.Caster != null &&
                                                         ReferenceEquals(p.Caster.Actor, caster.Actor))
                                             .Select(p => p.Target).Distinct().ToList())
                    Lift(was, spell.Id, fight);

            // Telekinesis: one target at a time - a new one ends the spell on whoever it was on
            if (spell.Effects.Any(e => e.Lands.Repeats() && e.Switches))
                foreach (Actor was in _placed.Where(p => p.Spell == spell.Id &&
                                                         p.Caster != null &&
                                                         ReferenceEquals(p.Caster.Actor, caster.Actor) &&
                                                         !aim.Creatures.Contains(p.Target))
                                             .Select(p => p.Target).Distinct().ToList())
                    Lift(was, spell.Id, fight);

            Run(caster, spell,
                spell.Effects.Where(e => e.Lands.Repeats() &&
                                         !e.Handler.MovesTheZone(e)),
                aim, castAt, fight, null, landings, squares, covered, again: true);

            return new Casting(spell, caster.Actor, castAt, true, null, landings, squares,
                               covered.Distinct().ToList());
        }

        readonly Dictionary<Actor, int> _heldAt = new();

        // whether a repeating spell can be done again now: held in concentration, or lasting -
        // and for a mark, only once the creature it was on has dropped
        public bool CanRepeat(Caster caster, Spell spell) =>
            caster != null && spell != null && spell.Repeat.HasValue &&
            (caster.Actor.Concentrating == spell.Id || _lasting.Contains((caster.Actor, spell.Id))) &&
            !(spell.MovesWhenDown && MarkStillStanding(caster, spell));

        bool MarkStillStanding(Caster caster, Spell spell) =>
            _placed.Any(p => p.Spell == spell.Id && p.Caster != null &&
                             ReferenceEquals(p.Caster.Actor, caster.Actor) && !p.Target.IsDown);

        // who has already been offered the chance to answer being targeted by this cast
        readonly HashSet<Actor> _offered = new();

        // once a casting: Empowered Evocation's one damage roll, Blessed Healer's one heal
        internal bool EmpoweredThisCast { get; set; }

        internal bool BlessedThisCast { get; set; }

        // spells that repeat without being held: who cast them, and which
        readonly HashSet<(Actor, string)> _lasting = new();

        // the damage type picked at the cast, for the repeats: Wyrmbreath Boon's breath
        readonly Dictionary<(Actor, string), DamageType> _chosenType = new();
    }
}
