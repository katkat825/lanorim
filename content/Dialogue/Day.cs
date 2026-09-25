using System.Collections.Generic;
using Core.Characters;
using Core.Combat;
using Core.Rules;

namespace Content.Dialogue
{
    // WHAT HAPPENED TODAY, which is what camp is about. A camp conversation that opens with "so,
    // nothing much" on the night you killed the boss is the moment the companion stops being a
    // character - so the night READS THE DAY rather than a menu.
    //
    // It is a CombatObserver, so it learns the day by watching the same fights the table does. No
    // second source of truth, and nothing in core/ has to know a companion exists.
    //
    // REWRITTEN for SRD. The shape is the old build's exactly - watch, tally, and answer one
    // question (About) worst-first - and every number it watched is gone. Three changes are worth
    // knowing about, because each is a thing this can no longer see:
    //
    //   Vigor -> hit points.       Health.Current against Health.Maximum, same low-water mark.
    //   Nerve spent -> death saves. There is no Nerve to spend. What "the day pushed you" looks
    //                               like in SRD is a hero rolling to get off the floor, and
    //                               DeathSaved is the observer call that says so.
    //   Tier.Dread -> challenge.    A boss was a TIER on the actor, so the old Day could read it off
    //                               the thing it just watched fall over. SRD's equivalent is
    //                               challenge rating, which lives on the STATBLOCK - so the fight
    //                               tells the day what it was fighting (Knows, set by the Battle,
    //                               2026-09-24), and a fallen foe of challenge at or above the
    //                               hero's level, or one its campaign tags "boss", is a boss.
    //                               Topic.Boss is offered again.
    public sealed class Day : CombatObserver
    {
        readonly Actor _hero;

        public Day(Actor hero) => _hero = hero;

        // what each creature in the fight is, as far as a boss goes: its challenge rating and
        // whether its campaign calls it a boss. the Battle hands this in; without it no fallen foe
        // is a boss (the old behaviour)
        public void Knows(System.Func<Actor, (double challenge, bool boss)> what) => _what = what;

        System.Func<Actor, (double challenge, bool boss)> _what;

        // a boss went down today
        public bool KilledABoss { get; private set; }

        public int Fights { get; private set; }

        public int Won { get; private set; }

        public int Felled { get; private set; }

        // the hero hit the floor
        public bool WentDown { get; private set; }

        // and had to roll to get up again
        public int DeathSaves { get; private set; }

        // conditions taken today, in the order they landed
        public IReadOnlyList<Condition> Took => _took;

        readonly List<Condition> _took = new List<Condition>();

        // the worst the hero's hit points got, as a fraction of maximum; 1.0 for an untouched day
        public double Lowest { get; private set; } = 1.0;

        public bool Bloodied => Lowest <= Half;

        public const double Half = 0.5;

        public bool Untouched => Fights > 0 && Lowest >= 1.0 && _took.Count == 0;

        public bool Quiet => Fights == 0;

        public override void Struck(Blow blow)
        {
            // read AFTER the blow lands, so the low-water mark is the number that frightened you
            if (blow != null && ReferenceEquals(blow.Target, _hero)) Mark();
        }

        public override void ConditionChanged(Actor actor, Condition condition, bool applied)
        {
            if (applied && ReferenceEquals(actor, _hero)) _took.Add(condition);
        }

        public override void Downed(Actor actor)
        {
            if (actor == null) return;

            if (ReferenceEquals(actor, _hero))
            {
                WentDown = true;
                Mark();
                return;
            }

            Felled++;

            if (_what != null && _hero != null)
            {
                (double challenge, bool boss) = _what(actor);

                if (boss || challenge >= System.Math.Max(1, _hero.Level)) KilledABoss = true;
            }
        }

        public override void DeathSaved(Actor actor, Core.Resolution.Attempt attempt)
        {
            if (ReferenceEquals(actor, _hero)) DeathSaves++;
        }

        public override void Ended(Outcome outcome)
        {
            Fights++;

            if (outcome == Outcome.HeroesWon) Won++;

            Mark();
        }

        void Mark()
        {
            if (_hero?.Health == null || _hero.Health.Maximum <= 0) return;

            double now = (double)_hero.Health.Current / _hero.Health.Maximum;

            if (now < Lowest) Lowest = now;
        }

        // the night starts over; a day's damage follows you but a day's conversation does not
        public void Slept()
        {
            Fights = 0;
            Won = 0;
            Felled = 0;
            WentDown = false;
            DeathSaves = 0;
            KilledABoss = false;
            _took.Clear();
            Lowest = 1.0;
        }

        // worst first, so a night with several answers gets the one worth talking about
        public Topic About()
        {
            if (WentDown) return Topic.Loss;
            if (KilledABoss) return Topic.Boss;
            if (Bloodied) return Topic.Bloodied;
            if (DeathSaves > 0) return Topic.Trouble;
            if (_took.Count > 0 || (_hero != null && _hero.Conditions.Count > 0)) return Topic.Wounded;
            if (Untouched) return Topic.Untouched;

            return Topic.Quiet;
        }

        // EVERY TOPIC THIS DAY COULD HONESTLY OPEN WITH, best first. There are only ever two: what
        // happened, and Quiet.
        //
        // The tempting version walks the whole ladder downwards, so a campaign missing a 'loss'
        // scene falls through to 'bloodied' - and then the companion says "you are still bleeding"
        // on a night you took nothing. Every topic but Quiet ASSERTS something about the day, so
        // Quiet is the only safe thing to fall back to, and a campaign that wrote no quiet scene
        // gets silence. Silence is better than a companion who was not paying attention.
        public IEnumerable<Topic> Offers()
        {
            Topic best = About();

            yield return best;

            if (best != Topic.Quiet) yield return Topic.Quiet;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"{Fights} fights, {Won} won, {Felled} felled" +
            (WentDown ? ", and the hero on the floor" : "") +
            (KilledABoss ? ", a boss among them" : "") +
            $"; hp down to {Lowest:0%}, {DeathSaves} death saves, " +
            $"{_took.Count} conditions - {About().Word()}";
    }
}
