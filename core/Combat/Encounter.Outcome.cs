using System;
using System.Linq;
using Core.Characters;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): who won
    public sealed partial class Encounter
    {
        // --- who won ----------------------------------------------------------------------------

        public Outcome Judge()
        {
            if (Over) return Outcome;

            // a hero on the floor has not lost yet - the death save is still to come, and judging
            // the fight before that die lands would skip the whole mechanic
            bool heroesStanding = _order.Any(r => r.Actor.Side == Allegiance.Hero && !r.Actor.IsDead &&
                                                  !(r.Actor.IsDown && r.Actor.Stable) &&
                                                  !_gone.Contains(r.Actor));
            bool enemiesStanding = _order.Any(r => r.Actor.Side == Allegiance.Enemy && !r.Actor.IsDown &&
                                                   !_gone.Contains(r.Actor));

            if (!heroesStanding) Outcome = Outcome.HeroesLost;
            else if (!enemiesStanding) Outcome = Outcome.HeroesWon;

            if (Over) Observer.Ended(Outcome);

            return Outcome;
        }

        public override string ToString() =>
            $"round {Round}, {_order.Count(r => !r.Actor.IsDown)} of {_order.Count} still up, " +
            Outcome;
    }
}
