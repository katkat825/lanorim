using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Combat
{
    // the fight tells, it does not draw. the table scene, the log, the accessibility narrator and
    // the sim are all just observers - which is what lets a whole fight be played headless.
    public interface ICombatObserver
    {
        void Began(IReadOnlyList<InitiativeRoll> order);

        void RoundBegan(int round);

        void TurnBegan(Turn turn);

        void Moved(Actor actor, IReadOnlyList<Cell> route);

        void Struck(Blow blow);

        void Opportunity(Actor attacker, Actor fleeing);

        // somebody spent their reaction, on what, and at what moment. told before the reaction
        // does anything, so the table can show the interruption before its dice
        void Reacted(Actor reactor, string reaction, Moment moment);

        void ConditionChanged(Actor actor, Condition condition, bool applied);

        void Downed(Actor actor);

        // taken off the board by a spell (Banishment, Maze), or put back on it. the table stands
        // the mini beside the map while it is away
        void Away(Actor actor, bool away) { }

        void DeathSaved(Actor actor, Attempt attempt);

        void TurnEnded(Turn turn);

        void Ended(Outcome outcome);
    }

    public enum Outcome
    {
        // still going
        Open,

        HeroesWon,

        HeroesLost,

        // the hero left the fight rather than finishing it
        Fled,
    }

    // implement one method, ignore the rest
    public abstract class CombatObserver : ICombatObserver
    {
        public virtual void Began(IReadOnlyList<InitiativeRoll> order) { }

        public virtual void RoundBegan(int round) { }

        public virtual void TurnBegan(Turn turn) { }

        public virtual void Moved(Actor actor, IReadOnlyList<Cell> route) { }

        public virtual void Struck(Blow blow) { }

        public virtual void Opportunity(Actor attacker, Actor fleeing) { }

        public virtual void Reacted(Actor reactor, string reaction, Moment moment) { }

        public virtual void ConditionChanged(Actor actor, Condition condition, bool applied) { }

        public virtual void Downed(Actor actor) { }

        public virtual void Away(Actor actor, bool away) { }

        public virtual void DeathSaved(Actor actor, Attempt attempt) { }

        public virtual void TurnEnded(Turn turn) { }

        public virtual void Ended(Outcome outcome) { }
    }

    // several at once, in the order they were added, and one that throws doesn't take the fight
    // down with it - a broken narrator must not lose the player's combat
    public sealed class Observers : ICombatObserver
    {
        readonly List<ICombatObserver> _watchers = new List<ICombatObserver>();

        public Observers(params ICombatObserver[] watchers)
        {
            foreach (ICombatObserver watcher in watchers ?? Array.Empty<ICombatObserver>())
                Add(watcher);
        }

        public void Add(ICombatObserver watcher)
        {
            if (watcher != null) _watchers.Add(watcher);
        }

        public IReadOnlyList<Exception> Failures => _failures;

        readonly List<Exception> _failures = new List<Exception>();

        void Each(Action<ICombatObserver> tell)
        {
            foreach (ICombatObserver watcher in _watchers)
            {
                try
                {
                    tell(watcher);
                }
                catch (Exception problem)
                {
                    _failures.Add(problem);
                }
            }
        }

        public void Began(IReadOnlyList<InitiativeRoll> order) => Each(w => w.Began(order));

        public void RoundBegan(int round) => Each(w => w.RoundBegan(round));

        public void TurnBegan(Turn turn) => Each(w => w.TurnBegan(turn));

        public void Moved(Actor actor, IReadOnlyList<Cell> route) => Each(w => w.Moved(actor, route));

        public void Struck(Blow blow) => Each(w => w.Struck(blow));

        public void Opportunity(Actor attacker, Actor fleeing) =>
            Each(w => w.Opportunity(attacker, fleeing));

        public void Reacted(Actor reactor, string reaction, Moment moment) =>
            Each(w => w.Reacted(reactor, reaction, moment));

        public void ConditionChanged(Actor actor, Condition condition, bool applied) =>
            Each(w => w.ConditionChanged(actor, condition, applied));

        public void Downed(Actor actor) => Each(w => w.Downed(actor));

        public void Away(Actor actor, bool away) => Each(w => w.Away(actor, away));

        public void DeathSaved(Actor actor, Attempt attempt) => Each(w => w.DeathSaved(actor, attempt));

        public void TurnEnded(Turn turn) => Each(w => w.TurnEnded(turn));

        public void Ended(Outcome outcome) => Each(w => w.Ended(outcome));
    }

    // every event, in order, as debug text. the fight check reads this; it is never shown to a
    // player, so it is not localized
    public sealed class CombatLog : CombatObserver
    {
        readonly List<string> _lines = new List<string>();

        public IReadOnlyList<string> Lines => _lines;

        public override void Began(IReadOnlyList<InitiativeRoll> order) =>
            _lines.Add("initiative: " + string.Join(", ", order));

        public override void RoundBegan(int round) => _lines.Add($"-- round {round}");

        public override void TurnBegan(Turn turn) => _lines.Add($"{turn.Actor.Id} steps up");

        public override void Moved(Actor actor, IReadOnlyList<Cell> route) =>
            _lines.Add($"{actor.Id} moves to {route[route.Count - 1]} " +
                       $"({route.Count - 1} squares)");

        public override void Struck(Blow blow) => _lines.Add(blow.ToString());

        public override void Opportunity(Actor attacker, Actor fleeing) =>
            _lines.Add($"{attacker.Id} takes a swing at {fleeing.Id} leaving its reach");

        public override void Reacted(Actor reactor, string reaction, Moment moment) =>
            _lines.Add($"{reactor.Id} reacts with {reaction} to {moment}");

        public override void ConditionChanged(Actor actor, Condition condition, bool applied) =>
            _lines.Add($"{actor.Id} is {(applied ? "now" : "no longer")} {condition.Id()}");

        public override void Downed(Actor actor) => _lines.Add($"{actor.Id} goes down");

        public override void Away(Actor actor, bool away) =>
            _lines.Add($"{actor.Id} {(away ? "is taken off the board" : "comes back")}");

        public override void DeathSaved(Actor actor, Attempt attempt) =>
            _lines.Add($"{actor.Id} death save {attempt.Total}: " +
                       (attempt.Succeeded ? "back up" : "dead"));

        public override void Ended(Outcome outcome) => _lines.Add($"== {outcome}");

        public override string ToString() => string.Join("\n", _lines);
    }
}
