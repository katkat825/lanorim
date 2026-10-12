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

            // the enemies broke (Encounter.Morale.cs): with nowhere to run they give up, and it's won
            if (Breaks() && Surrendered)
            {
                Outcome = Outcome.HeroesWon;
                Observer.Ended(Outcome);
                return Outcome;
            }

            // a hero on the floor has not lost yet - the death save is still to come, and judging
            // the fight before that die lands would skip the whole mechanic
            bool heroesStanding = Actors.Any(a => a.Side == Allegiance.Hero && !a.IsDead && !(a.IsDown && a.Stable) &&
                                                  !_off.IsGone(a));
            bool enemiesStanding = Actors.Any(a => a.Side == Allegiance.Enemy && !a.IsDown && !_off.IsGone(a));

            if (!heroesStanding) Outcome = Outcome.HeroesLost;
            else if (!enemiesStanding) Outcome = Outcome.HeroesWon;

            if (Over) Observer.Ended(Outcome);

            return Outcome;
        }

        public override string ToString() =>
            $"round {Round}, {Actors.Count(a => !a.IsDown)} of {Order.Count} still up, " +
            Outcome;
    }
}
