using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Space;

namespace Core.Magic
{
    public sealed partial class Incantation
    {
        // a reaction spell, cast because the fight offered its moment and the caster's chooser
        // took it. the reaction itself was spent by the window that offered it; what is left is
        // the resource and the spell. aimed at whoever caused the moment - the one who hit you,
        // the one casting - and an effect that reaches only the caster ignores that.
        public Casting Answer(Caster caster, Spell spell, Moment moment, Encounter fight = null)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));

            if (spell == null || moment == null)
                return Casting.Refused(spell, caster.Actor, 0, "nothing to answer");

            bool targeted = moment.Trigger == Trigger.Targeted && spell.AnswersSpell.Length > 0 &&
                            moment.Spell == spell.AnswersSpell;

            if (!spell.Answers || spell.Trigger != moment.Trigger && !targeted)
                return Casting.Refused(spell, caster.Actor, spell.Level,
                                       "that spell does not answer that moment");

            if (!caster.Knows(spell.Id))
                return Casting.Refused(spell, caster.Actor, spell.Level, "not on the sheet");

            // a Shield is still a spell, and a bear does not cast it
            if (caster.Actor.IsShifted || caster.Actor.Boons.Forbids(Forbid.Casting))
                return Casting.Refused(spell, caster.Actor, spell.Level, "in a borrowed shape");

            if (!caster.CanCast(spell, spell.Level))
                return Casting.Refused(spell, caster.Actor, spell.Level,
                                       "nothing left to cast it with");

            // a smite lands on the one the caster just hit; everything else on whoever caused it
            Actor at = moment.Trigger == Trigger.Struck ? moment.Target : moment.Source;

            return Go(caster, spell, Aim.At(at), spell.Level, fight, moment);
        }

        // the part every cast shares, once the turn has paid for it
        Casting Go(Caster caster, Spell spell, Aim aim, int castAt, Encounter fight,
                   Moment answering)
        {
            // THE CAST WINDOW. before the resource is paid and before anything resolves, every
            // creature hostile to the caster is offered the chance to stop it. a Counterspell can
            // itself be countered - it comes through here like everything else - and one reaction
            // a round each is what keeps that from going on forever.
            if (fight != null)
            {
                Moment casting = Moment.Cast(caster.Actor, spell.Id, castAt);

                fight.Offer(casting, fight.Field.Enemies(caster.Actor));

                if (casting.Stopped) return Casting.Stopped(spell, caster.Actor, castAt);
            }

            // paid after the action is taken and before anything resolves, so a cast that is
            // refused downstream has still cost what it cost - which is the table's rule. a
            // countered cast is the exception, above, because SRD 5.2.1 says so in as many words
            if (!caster.Pay(spell, castAt))
                return Casting.Refused(spell, caster.Actor, castAt,
                                       "the spell resource would not pay");

            // casting gives an unseen caster away (SRD's Invisible) - an Invisibility ends here
            Unveil(caster.Actor, fight);

            // Foresight: casting it again ends the one already cast
            if (spell.EndsPrevious)
                foreach (Actor was in _placed.Where(p => p.Spell == spell.Id && p.Caster != null &&
                                                         ReferenceEquals(p.Caster.Actor, caster.Actor))
                                             .Select(p => p.Target).Distinct().ToList())
                    Lift(was, spell.Id, fight);

            _offered.Clear();
            EmpoweredThisCast = false;
            BlessedThisCast = false;

            // Major Image at 4+: no concentration at all
            bool holds = spell.Concentration &&
                         !(spell.ConcentrationBelow > 0 && castAt >= spell.ConcentrationBelow);

            // taking up a new concentration drops whatever was being held, and its boons with it
            if (holds)
            {
                Hold(caster.Actor, spell.Id);
                _heldAt[caster.Actor] = castAt;
            }
            else if (spell.Repeat.HasValue && spell.Duration != Duration.Instant)
            {
                // Produce Flame: no concentration, and still repeatable for as long as it lasts
                _lasting.Add((caster.Actor, spell.Id));
            }

            // the damage type picked at the cast, kept for the repeats
            if (aim.DamageType != DamageType.None) _chosenType[(caster.Actor, spell.Id)] = aim.DamageType;

            var landings = new List<Landing>();
            var squares = new List<Cell>();
            var covered = new List<Cell>();

            Run(caster, spell, spell.Effects, aim, castAt, fight, answering,
                landings, squares, covered);

            return new Casting(spell, caster.Actor, castAt, true, null, landings, squares,
                               covered.Distinct().ToList());
        }

        void Run(Caster caster, Spell spell, IEnumerable<SpellEffect> effects, Aim aim, int castAt,
                 Encounter fight, Moment answering, List<Landing> landings, List<Cell> squares,
                 List<Cell> covered, bool again = false)
        {
            // Produce Flame's hurl is its later actions, not its casting
            if (!again) effects = effects.Where(e => e.Lands != Lands.OnRepeat);
            // the last saving throw each creature made against this casting, for an effect that
            // shares it rather than asking for another
            var saves = new Dictionary<Actor, Attempt>();

            int previous = landings.Count;

            // what lands when the spell ends (Haste's lethargy) waits for the ending
            foreach (SpellEffect effect in effects.Where(e => InMode(e, aim) && e.Lands != Lands.OnEnd))
            {
                // who the effect just before this one actually landed on, for one that follows it
                HashSet<Actor> hit = new HashSet<Actor>(
                    landings.Skip(previous).Where(l => l.Landed && l.Target != null)
                            .Select(l => l.Target));

                previous = landings.Count;

                Resolve(caster, spell, effect, aim, castAt, fight, answering,
                        landings, squares, covered, saves, effect.Follows ? hit : null);
            }
        }

        static bool InMode(SpellEffect effect, Aim aim) =>
            effect.Mode.Length == 0 || effect.Mode == aim.Mode;
    }
}
