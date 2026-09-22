using System;

namespace Core.Resolution
{
    public enum RollKind
    {
        // an ability or skill check against a DC
        Check,

        // a saving throw against a DC
        Save,

        // an attack roll against an AC
        Attack,

        // the single d20 >= 10, no modifiers, that decides whether a downed hero gets back up
        Death,
    }

    // what one roll against a number came to. says what happened; doing something about it -
    // damage, a condition, a consequence - is the caller's, exactly as the old AbilityOutcome did.
    public sealed class Attempt
    {
        public Attempt(RollKind kind, D20Roll roll, int against)
        {
            Kind = kind;
            Roll = roll ?? throw new ArgumentNullException(nameof(roll));
            Against = against;
        }

        public RollKind Kind { get; }

        public D20Roll Roll { get; }

        // the DC, or the AC for an attack
        public int Against { get; }

        public int Total => Roll.Total;

        public int Natural => Roll.Natural;

        // SRD: a natural 20 on an attack always hits and a natural 1 always misses. a *check* is
        // the deliberate delta - the numbers alone decide, and the natural 1 or 20 buys a
        // consequence instead (updated_decisions.md, skill checks). a save follows the check rule:
        // no auto-pass, no auto-fail, because a save that auto-fails on a 1 undoes the same loop.
        public bool Succeeded =>
            Kind == RollKind.Attack
                ? Roll.IsNaturalTwenty || (!Roll.IsNaturalOne && Roll.Beats(Against))
                : Roll.Beats(Against);

        public bool Failed => !Succeeded;

        // only an attack crits; a check's natural 20 buys a consequence, not double anything
        public bool IsCritical => Kind == RollKind.Attack && Roll.IsNaturalTwenty;

        public bool IsCriticalMiss => Kind == RollKind.Attack && Roll.IsNaturalOne;

        // a natural 1 or 20 on a check or a crit in combat draws from the consequence pool -
        // decisions_checklist.md section 1, both bullets
        public bool DrawsConsequence =>
            Kind == RollKind.Check
                ? Roll.IsNaturalOne || Roll.IsNaturalTwenty
                : IsCritical;

        // how far over or under - a campaign can key a degree of success off it
        public int Margin => Total - Against;

        public override string ToString() =>
            $"{Kind.ToString().ToLowerInvariant()}: {Roll} vs {Against} - " +
            (Succeeded ? "success" : "failure") +
            (IsCritical ? ", critical" : IsCriticalMiss ? ", critical miss" : "");
    }
}
