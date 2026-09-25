using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Resolution;

namespace Core.Combat
{
    public sealed class InitiativeRoll
    {
        public InitiativeRoll(Actor actor, D20Roll roll)
        {
            Actor = actor;
            Roll = roll;
        }

        public Actor Actor { get; }

        public D20Roll Roll { get; }

        public int Total => Roll.Total;

        public override string ToString() => $"{Actor.Id} {Total}";
    }

    // d20 + Dex, highest first. the tie-break is the hero, then the higher Dex modifier, then the
    // id - all three so the same fight replays the same way from the same seed.
    public static class Initiative
    {
        public static IReadOnlyList<InitiativeRoll> Roll(IResolver resolver, IEnumerable<Actor> actors)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (actors == null) throw new ArgumentNullException(nameof(actors));

            var rolled = new List<InitiativeRoll>();

            foreach (Actor actor in actors)
            {
                // SRD 5.2.1: the Invisible roll initiative with advantage, the Incapacitated
                // (surprised, in effect) with disadvantage
                Attempt attempt = resolver.Resolve(RollKind.Check,
                                                   actor.AbilityModifier(Ability.Dexterity), 0,
                                                   Advantages.Of(actor.Has(Condition.Invisible),
                                                                 actor.IsIncapacitated), actor);

                rolled.Add(new InitiativeRoll(actor, attempt.Roll));
            }

            return rolled
                .OrderByDescending(r => r.Total)
                .ThenByDescending(r => r.Actor.Side == Allegiance.Hero)
                .ThenByDescending(r => r.Actor.AbilityModifier(Ability.Dexterity))
                .ThenBy(r => r.Actor.Id, StringComparer.Ordinal)
                .ToList();
        }
    }
}
