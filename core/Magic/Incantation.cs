using System;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Resolution;

namespace Core.Magic
{
    // the one thing that casts a spell. it reads the primitives and does what they say; there is
    // no per-spell code anywhere in the engine, which is the whole point of the primitive list.
    //
    // one class in several files, one concern each (cc_task_dedupe-effects.md, Phase 5): this one
    // casts, answers and repeats; .Targeting finds who an effect reaches; .Apply makes the checks
    // every primitive shares and hands the rest to the primitive's handler (Primitives/); the
    // rest keep what a spell left behind (.Placements), what happens on turns and hurts
    // (.TurnHooks), zones, dispelling, concentration, and breaking free (.Escape).
    public sealed partial class Incantation
    {
        readonly IResolver _resolver;

        public Incantation(IResolver resolver) =>
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

        internal IResolver Resolver => _resolver;

        public Casting Cast(Caster caster, Spell spell, Aim aim, int castAt = -1,
                            Encounter fight = null, Turn turn = null)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));

            if (spell == null) return Casting.Refused(null, null, 0, "no spell");

            if (castAt < 0) castAt = spell.Level;

            if (!caster.Knows(spell.Id))
                return Casting.Refused(spell, caster.Actor, castAt, "not on the sheet");

            if (caster.Actor.IsShifted)
                return Casting.Refused(spell, caster.Actor, castAt, "in a borrowed shape");

            if (caster.Actor.Boons.Forbids(Forbid.Casting))
                return Casting.Refused(spell, caster.Actor, castAt, "can't cast in this form");

            // THE ONLY QUESTION ASKED OF THE RESOURCE, and it does not say which mode answered
            // it. A slots caster with no 3rd-level slot and a points caster who has already
            // cast their one 6th today are refused by the same line.
            if (!caster.CanCast(spell, castAt))
                return Casting.Refused(spell, caster.Actor, castAt,
                                       $"nothing left to cast a level {castAt} spell with " +
                                       $"({caster.Resource?.Describe() ?? "no spell resource"})");

            aim ??= Aim.Nothing;

            // a Shield on your own turn is not a Shield. the only way to cast one is to be offered
            // the moment it answers - Answer, below
            if (spell.Answers)
                return Casting.Refused(spell, caster.Actor, castAt,
                                       spell.IsReaction
                                           ? "a reaction spell is cast when its moment comes, not on a turn"
                                           : "cast right after the hit it rides on, not on its own");

            if (!caster.Actor.CanAct)
                return Casting.Refused(spell, caster.Actor, castAt, "cannot act");

            // pointing it is part of casting it: on a board, a line with no direction goes nowhere
            if (fight != null && spell.NeedsADirection && !aim.Facing.HasValue &&
                !aim.Square.HasValue)
                return Casting.Refused(spell, caster.Actor, castAt,
                                       "a line, a cone or a cube has to be pointed somewhere");

            // on a board, what it is aimed at has to be in its range and in sight. SRD's touch is
            // range 0 in the data and one square on the grid
            if (fight != null && OutOfReach(caster, spell, aim, fight) is string far)
                return Casting.Refused(spell, caster.Actor, castAt, far);

            // a choice the spell leaves to the caster has to have been made, from its list
            if (spell.Effects.Any(e => e.Boon.SkillChoices.Count > 0 && !e.Boon.SkillChoices.Contains(aim.Skill)))
                return Casting.Refused(spell, caster.Actor, castAt, "choose a skill for it");

            if (spell.Effects.Any(e => e.Boon.AbilityChoices.Count > 0) && !aim.Ability.HasValue)
                return Casting.Refused(spell, caster.Actor, castAt, "choose an ability for it");

            if (spell.Effects.Any(e => e.Boon.AbilityChoices.Count > 0 &&
                                       !e.Boon.AbilityChoices.Contains(aim.Ability.Value)))
                return Casting.Refused(spell, caster.Actor, castAt, "not an ability it can choose");

            // a minute's casting or an hour's is not a fight's (Identify, Raise Dead, Foresight)
            if (fight != null && spell.OutOfCombat)
                return Casting.Refused(spell, caster.Actor, castAt, "takes too long to cast in a fight");

            if (spell.Strikes && aim.Weapon == null)
                return Casting.Refused(spell, caster.Actor, castAt, "choose the weapon it strikes with");

            if (fight != null && UnderTheZone(caster, spell, aim, fight, null) is string outside)
                return Casting.Refused(spell, caster.Actor, castAt, outside);

            if (fight != null && spell.Effects.Any(e => e.Kind == Primitive.Zone && e.Unoccupied) &&
                aim.Square.HasValue && fight.Field.At(aim.Square.Value) != null)
                return Casting.Refused(spell, caster.Actor, castAt, "that square is taken");

            if (spell.Modes.Count > 0 && !spell.Modes.Contains(aim.Mode))
                return Casting.Refused(spell, caster.Actor, castAt,
                                       "choose how to cast it: " + string.Join(" or ", spell.Modes));

            foreach (SpellEffect choice in spell.Effects.Where(e => e.ChosenDamageType &&
                                                                    InMode(e, aim)))
                if (!choice.DamageChoices.Contains(aim.DamageType))
                    return Casting.Refused(spell, caster.Actor, castAt,
                                           "choose a damage type for it");

            // SRD 5.2.1 Charmed: no damaging or magical effect aimed at the charmer. v1 reads it
            // as "nothing hostile": a heal or a blessing still may, a curse with no save may not
            if (aim.Creatures.Any(t => caster.Actor.HasFrom(Condition.Charmed, t)) &&
                spell.Effects.Any(e => !(e.Kind == Primitive.Heal || e.Kind == Primitive.Ward ||
                                         e.Kind == Primitive.Relieve || e.Kind == Primitive.Stabilize ||
                                         e.Kind == Primitive.Sway && !e.Save.HasValue && e.OnlyHelps)))
                return Casting.Refused(spell, caster.Actor, castAt, "charmed by the target");

            bool bonus = spell.CastingTime == Spend.Bonus;

            if (turn != null && !turn.Take(bonus ? Spend.Bonus : Spend.Action))
                return Casting.Refused(spell, caster.Actor, castAt,
                                       bonus ? "no bonus action left" : "no action left");

            return Go(caster, spell, aim, castAt, fight, null);
        }
    }
}
