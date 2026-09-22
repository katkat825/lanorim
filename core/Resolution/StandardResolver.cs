using System;
using System.Collections.Generic;
using Core.Dice;

namespace Core.Resolution
{
    public sealed class StandardResolver : IResolver
    {
        readonly IRng _rng;

        public StandardResolver(IRng rng) =>
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        public Attempt Resolve(RollKind kind, int modifier, int against,
                               Advantage advantage = Advantage.Flat)
        {
            // the death save is a bare d20 against 10 - no modifiers, no advantage, ever. it is
            // enforced here rather than trusted to every caller, because an ability that quietly
            // added +2 to it would change how often a campaign restarts.
            if (kind == RollKind.Death)
                return new Attempt(kind, D20Roll.Make(_rng, 0), DeathSave.Dc);

            return new Attempt(kind, D20Roll.Make(_rng, modifier, advantage), against);
        }

        public int Roll(DiceRoll dice) => dice.Roll(_rng);
    }

    // wraps another resolver and keeps every attempt it made, in order. the fight check and the
    // sim both read this instead of re-rolling to find out what happened.
    public sealed class LoggingResolver : IResolver
    {
        readonly IResolver _inner;
        readonly List<Attempt> _attempts = new List<Attempt>();

        public LoggingResolver(IResolver inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public IReadOnlyList<Attempt> Attempts => _attempts;

        public Attempt Resolve(RollKind kind, int modifier, int against,
                               Advantage advantage = Advantage.Flat)
        {
            Attempt attempt = _inner.Resolve(kind, modifier, against, advantage);
            _attempts.Add(attempt);
            return attempt;
        }

        public int Roll(DiceRoll dice) => _inner.Roll(dice);
    }

    public static class DeathSave
    {
        // single d20 >= 10, no mods - an intentional solo delta from SRD's three-successes ladder
        // (decisions_checklist.md section 1)
        public const int Dc = 10;

        // what a hero is on when the save succeeds
        public const int RevivedAt = 1;
    }
}
