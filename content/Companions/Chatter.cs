using System;
using Content.Dialogue;
using Content.Play;
using Core.Dice;

namespace Content.Companions
{
    // DON'T LET IT CHATTER (cc_task_i 2.5): one bark at a time, a global cooldown, and a priority order - a higher
    // pool cuts in on the cooldown, a lower one is dropped, never queued. Also whether a fight's start is worth a
    // Danger line. The clock is the mind's (seconds of play), so this is testable without one
    public sealed class Chatter
    {
        // seconds between barks, unless a higher pool cuts in
        public const double CooldownSeconds = 20;

        // a fight's start keeps quiet when the story barked this recently (its own <<bark danger>>, say)
        public const double StoryQuietSeconds = 10;

        // a fight's start says a Danger line one time in this many
        public const int DangerOneIn = 3;

        readonly IRng _rng;
        readonly Func<CompanionChatter> _level;
        readonly Cooldown _cooldown = new Cooldown(CooldownSeconds);

        Bark _last;
        double _storyAt = double.NegativeInfinity;

        public Chatter(IRng rng, Func<CompanionChatter> level = null)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _level = level ?? (() => CompanionChatter.All);
        }

        public CompanionChatter Level => _level();

        // whether a bark from this pool may be said now; if it may, it is the one being said
        public bool Allows(Bark pool, double now, bool fromStory = false)
        {
            if (Level == CompanionChatter.Off || !pool.Speaks()) return false;

            if (Level == CompanionChatter.Some && pool.Frequent() && _rng.Roll(2) == 1) return false;

            if (!_cooldown.Ready(now) && pool.Priority() <= _last.Priority()) return false;

            _cooldown.Mark(now);
            _last = pool.Pool();

            if (fromStory) _storyAt = now;

            return true;
        }

        // a fight is starting, before initiative: a Danger line one time in three, unless the story just barked
        public bool DangerAhead(double now) =>
            Level != CompanionChatter.Off && now - _storyAt >= StoryQuietSeconds && _rng.Roll(DangerOneIn) == 1;

        public override string ToString() => $"{Level}, {_cooldown}, last {_last}";
    }
}
