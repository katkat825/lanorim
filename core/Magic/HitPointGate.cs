using Core.Characters;

namespace Core.Magic
{
    // only a creature on one side of a hit point line is touched: Power Word Stun stuns at or below
    // 150 and only slows above it. one record where there were two numbers - only_at_or_below and
    // only_above (cc_task_dedupe-effects.md, 3a #14)
    public sealed record HitPointGate(int Line, bool Above)
    {
        public bool Lets(Actor target) =>
            Above ? target.Health.Current > Line : target.Health.Current <= Line;
    }
}
