using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Rules;

namespace Core.Magic
{
    // BREAKING FREE: Web's Athletics check, Maze's Study action, and shaking a sleeper awake
    public sealed partial class Incantation
    {
        // SRD's "a creature restrained by the webs can take an action to make a Strength
        // (Athletics) check against your spell save DC". the action is spent whatever the check
        // says; null when nothing it is under can be broken this way
        public Attempt BreakFree(Encounter fight, Turn turn, Condition condition)
        {
            if (turn == null || turn.Ended) return null;

            Placement hold = _placed.FirstOrDefault(p => ReferenceEquals(p.Target, turn.Actor) &&
                                                         p.Condition == condition &&
                                                         p.Spec.Escape != null);

            return Escape(fight, turn, hold);
        }

        // SRD 5.2.1 Maze: a Study action and a DC 20 Intelligence (Investigation) check; a success
        // escapes, and the spell ends. null when nothing it is under can be escaped this way
        public Attempt Study(Encounter fight, Turn turn)
        {
            if (turn == null || turn.Ended) return null;

            Placement hold = _placed.FirstOrDefault(p => ReferenceEquals(p.Target, turn.Actor) &&
                                                         p.Condition == Condition.None &&
                                                         p.Spec.Escape != null);

            return Escape(fight, turn, hold);
        }

        public bool CanStudy(Actor creature) =>
            _placed.Any(p => ReferenceEquals(p.Target, creature) && p.Condition == Condition.None &&
                             p.Spec.Escape != null);

        Attempt Escape(Encounter fight, Turn turn, Placement hold)
        {
            if (hold == null || !turn.Take(Spend.Action)) return null;

            Escape escape = hold.Spec.Escape;

            Attempt attempt = Checks.Check(_resolver, turn.Actor, escape.Check,
                                           escape.Dc > 0 ? escape.Dc : hold.Dc);

            if (!attempt.Succeeded) return attempt;

            if (hold.Condition == Condition.None)
            {
                // out of the maze: the spell ends on it, and it comes back
                Lift(turn.Actor, hold.Spell, fight);

                if (hold.Caster != null && hold.Caster.Actor.Concentrating == hold.Spell &&
                    !_placed.Any(p => p.Spell == hold.Spell && ReferenceEquals(p.Caster, hold.Caster)))
                    Release(hold.Caster.Actor);
            }
            else
            {
                turn.Actor.Remove(hold.Condition);
                _placed.Remove(hold);

                fight?.Observer.ConditionChanged(turn.Actor, hold.Condition, false);
            }

            return attempt;
        }

        // SRD 5.2.1 Sleep and Hypnotic Pattern: someone within 5 feet takes an action to shake
        // the creature out of it. true when something was shaken off
        public bool Shake(Encounter fight, Turn turn, Actor sleeper)
        {
            if (turn == null || turn.Ended || sleeper == null || fight == null) return false;

            if (fight.Field.Distance(turn.Actor, sleeper) > 1) return false;

            List<Placement> shaken = _placed.Where(p => ReferenceEquals(p.Target, sleeper) &&
                                                        p.Spec.Shakeable).ToList();

            if (shaken.Count == 0 || !turn.Take(Spend.Action)) return false;

            foreach (string spell in shaken.Select(p => p.Spell).Distinct().ToList())
                Lift(sleeper, spell, fight);

            return true;
        }

        // whether anything on this creature could be shaken off - what a "wake them" button asks
        public bool CanBeShaken(Actor creature) =>
            _placed.Any(p => ReferenceEquals(p.Target, creature) && p.Spec.Shakeable);
    }
}
