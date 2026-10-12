using System;
using System.Collections.Generic;
using Content.Dialogue;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Rules;

namespace Content.Companions
{
    // THE COMPANION'S MIND, WITHOUT ITS BODY: ported from ../solo_ttrpg_game/game/Companion (Mood,
    // TableCues, Idling) so that what the creature reacts to, how long it holds a reaction and
    // which idle it plays next are testable without Godot. The table's Companion node only plays
    // what this decides (Tier 2.6 of the 2026-09-24 run).
    //
    // THE BARK NAMES ARE UNCHANGED (Kathleen's decision). They were named for the old dice pools;
    // under SRD they are read like this, and the run log suggests new names:
    //   Snag    - a natural 1 on an attack roll (a clumsy miss)
    //   Trouble - a natural 1 on a check or save (the consequence pool draws)
    //   Maxed   - a critical hit
    //   Perfect - a natural 20 on a check or save
    //   Nerve   - a death save (the SRD moment the old "Nerve spent" was)

    // "It reacts to the table, not the fiction" - what the creature is doing with its head
    public enum Mood
    {
        Calm,
        Watching,
        Alert,
        Quiet,
        Pleased,
    }

    public static class TableCues
    {
        // what a d20 roll the player made is to the companion, or null for an ordinary one
        public static Bark? For(Attempt roll)
        {
            if (roll == null) return null;

            switch (roll.Kind)
            {
                case RollKind.Attack:
                    if (roll.IsCritical) return Bark.Maxed;
                    if (roll.IsCriticalMiss) return Bark.Snag;
                    return null;

                case RollKind.Check:
                case RollKind.Save:
                    if (roll.Natural == 1) return Bark.Trouble;
                    if (roll.Natural == 20) return Bark.Perfect;
                    return null;

                case RollKind.Death:
                    return Bark.Nerve;
            }

            return null;
        }

        public static Mood MoodFor(Bark situation) => situation switch
        {
            Bark.Trouble => Mood.Alert,
            Bark.Secret => Mood.Alert,
            Bark.Nerve => Mood.Alert,
            Bark.Down => Mood.Quiet,
            Bark.Victory => Mood.Pleased,
            Bark.Perfect => Mood.Pleased,
            Bark.Maxed => Mood.Pleased,
            Bark.Camp => Mood.Calm,
            Bark.Overasked => Mood.Alert,
            Bark.Nudge => Mood.Alert,
            Bark.Nearby => Mood.Alert,
            _ => Mood.Watching,
        };

        // seconds it holds a reaction before settling back to its idles
        public static double HoldFor(Mood mood) => mood switch
        {
            Mood.Quiet => 6.0,
            Mood.Alert => 2.4,
            Mood.Pleased => 2.0,
            Mood.Watching => 1.6,
            _ => 0.0,
        };

        // Quiet outranks everything: a hero on the floor is not something a good throw talks you
        // out of
        public static bool Outranks(Mood coming, Mood standing) =>
            standing != Mood.Quiet && Rank(coming) >= Rank(standing);

        static int Rank(Mood mood) => mood switch
        {
            Mood.Quiet => 4,
            Mood.Alert => 3,
            Mood.Pleased => 2,
            Mood.Watching => 1,
            _ => 0,
        };
    }

    // the idles are DEALT like the barks: every one seen before any is seen twice, none after itself
    public sealed class Idling
    {
        readonly ShuffleBag _bag;
        readonly IRng _rng;

        public Idling(int idles, IRng rng, double hold = DefaultHold, double jitter = DefaultJitter)
        {
            if (idles < 0) throw new ArgumentOutOfRangeException(nameof(idles));

            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _bag = new ShuffleBag(idles, _rng);
            Count = idles;
            Hold = hold;
            Jitter = jitter;
            Change();
        }

        public const double DefaultHold = 3.2;
        public const double DefaultJitter = 1.4;

        public int Count { get; }

        public double Hold { get; }

        public double Jitter { get; }

        // 1-based; 0 when there are none
        public int Idle { get; private set; }

        public double Left { get; private set; }

        public int Changes { get; private set; }

        // true on the frame the idle changed
        public bool Tick(double delta)
        {
            if (Count == 0) return false;

            Left -= delta;

            if (Left > 0.0) return false;

            Change();
            return true;
        }

        public void Break() => Left = 0.0;

        void Change()
        {
            Idle = Count == 0 ? 0 : _bag.Next();
            Changes++;

            double spread = Jitter <= 0.0 ? 0.0 : Jitter * ((_rng.Roll(1000) - 1) / 999.0 - 0.5);
            Left = Math.Max(0.1, Hold + spread);
        }
    }

    // WHAT THE COMPANION SAYS AND FEELS, from what the table tells it: a roll, a fight event, a story's cue. It
    // speaks from its bark bank (a shuffle bag per pool); silence is a real answer when the bank has nothing for
    // the moment, and the Chatter (if it has one) keeps it from talking too much (cc_task_i Part 2).
    //
    // It hears from the rules thread (a fight, the story) and the main thread (a rattle, the clock), so what it
    // decides is decided under one lock.
    public sealed class CompanionMind : CombatObserver
    {
        readonly Speaking _speaking;
        readonly Actor _hero;
        readonly Chatter _chatter;
        readonly object _gate = new object();

        public CompanionMind(Speaking speaking, Actor hero = null, Chatter chatter = null)
        {
            _speaking = speaking;
            _hero = hero;
            _chatter = chatter;
        }

        public Mood Mood { get; private set; } = Mood.Calm;

        public double Holding { get; private set; }

        // seconds of play it has been ticked through: the chatter's clock
        public double Now { get; private set; }

        // every key it said, in order - the bubble shows them
        public IReadOnlyList<string> Said => _said;

        readonly List<string> _said = new List<string>();

        public event Action<string> Spoke;

        public event Action<Mood> Felt;

        // the hero is hurt (Bloodied): Danger keeps quiet, because its lines warn of something coming
        bool Hurt => _hero != null && _hero.Health.IsBloodied;

        // a moment at the table: the companion feels it, and says a line from its pool if it has one and may.
        // `fromStory` is a story's own cue (<<bark>>, a rest), which a fight's start keeps quiet after
        public string Say(Bark situation, bool fromStory = false)
        {
            string key;

            lock (_gate)
            {
                Feel(TableCues.MoodFor(situation));

                Bark pool = situation.Pool();

                if (!pool.Speaks() || _speaking?.Has(pool) != true) return null;

                if (pool == Bark.Danger && Hurt) return null;

                if (_chatter != null && !_chatter.Allows(pool, Now, fromStory)) return null;

                key = _speaking.Next(pool);

                if (key == null) return null;

                _said.Add(key);
            }

            Spoke?.Invoke(key);
            return key;
        }

        void Feel(Mood mood)
        {
            if (!TableCues.Outranks(mood, Mood)) return;

            Mood = mood;
            Holding = TableCues.HoldFor(mood);
            Felt?.Invoke(mood);
        }

        // time passes; a held reaction runs out and it settles
        public void Tick(double delta)
        {
            lock (_gate)
            {
                Now += delta;

                if (Holding <= 0) return;

                Holding -= delta;

                if (Holding <= 0) Settle();
            }
        }

        public void Settle()
        {
            Mood = Mood.Calm;
            Holding = 0;
            Felt?.Invoke(Mood);
        }

        // the dice are in the air
        public void Watches()
        {
            lock (_gate) Feel(Mood.Watching);
        }

        // one of the player's d20s has landed
        public string Sees(Attempt roll) =>
            TableCues.For(roll) is Bark situation ? Say(situation) : null;

        public string HearsASecretRoll() => Say(Bark.Secret);

        // the story asked for a line (<<bark nearby>>), or rested (Camp)
        public string Hears(Bark cue) => Say(cue, fromStory: true);

        // a fight is laid, before initiative: now and then, a warning (cc_task_i 2.3)
        public string FightStarts()
        {
            bool warns;

            lock (_gate) warns = _chatter != null && !Hurt && _chatter.DangerAhead(Now);

            return warns ? Say(Bark.Danger) : null;
        }

        // --- the fight, watched ---------------------------------------------------------------

        public override void Struck(Blow blow)
        {
            if (_hero != null && ReferenceEquals(blow?.Attacker, _hero)) Sees(blow.Attempt);
        }

        // a save the hero made against something: a natural 1 or 20 on it is a Snag or a Maxed
        public override void Judged(Actor by, Actor target, Attempt attempt)
        {
            if (_hero != null && ReferenceEquals(target, _hero) && attempt?.Kind == RollKind.Save) Sees(attempt);
        }

        public override void Downed(Actor actor)
        {
            if (_hero != null && ReferenceEquals(actor, _hero)) Say(Bark.Down);
        }

        public override void DeathSaved(Actor actor, Attempt attempt)
        {
            if (_hero != null && ReferenceEquals(actor, _hero)) Say(Bark.Nerve);
        }

        public override void Ended(Outcome outcome)
        {
            if (outcome == Outcome.HeroesWon) Say(Bark.Victory);
        }
    }
}
