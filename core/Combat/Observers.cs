using System.Collections.Generic;
using Core.Characters;
using Core.Resolution;
using Core.Rules;
using Core.Space;
using Core.Words;

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

        // an attack roll or a saving throw is settled: after every reaction that could turn it (a
        // Shield, a Mirror Image), before anything it does lands. So the table says hit or miss
        // before the damage dice are thrown. A weapon's swing and a spell's alike; for a save, the
        // target rolled it and 'by' is the caster
        void Judged(Actor by, Actor target, Attempt attempt) { }

        // hit points came off a creature from a weapon or a spell, with the dice that did it
        void Dealt(Harm harm) { }

        // a spell is cast: past the window where it could be countered and paid for, before anything
        // it does resolves. the spell's name key, and whoever it was aimed at (none for an area)
        void Casts(Actor caster, string spellKey, IReadOnlyList<Actor> at) { }

        // the same spell, resolved: what it changed that the damage line doesn't say (an armor
        // class, hit points regained, temporary hit points, speed)
        void Cast(Actor caster, string spellKey, IReadOnlyList<Change> changes) { }

        void Opportunity(Actor attacker, Actor fleeing);

        // somebody spent their reaction, on what, and at what moment. told before the reaction
        // does anything, so the table can show the interruption before its dice
        void Reacted(Actor reactor, string reaction, Moment moment);

        void ConditionChanged(Actor actor, Condition condition, bool applied);

        // a shut door opened (Encounter.Doors.cs)
        void DoorOpened(Actor actor, Core.Space.Border door);

        void Downed(Actor actor);

        // taken off the board by a spell (Banishment, Maze), or put back on it. the table stands
        // the mini beside the map while it is away
        void Away(Actor actor, bool away) { }

        void DeathSaved(Actor actor, Attempt attempt);

        void TurnEnded(Turn turn);

        void Ended(Outcome outcome);
    }

    // the words are what a .yarn file reads back in $fight: "won", "lost", "fled" - words rather
    // than numbers, so the story reads as English
    public enum Outcome
    {
        // still going
        [Word(""), Unread] Open,

        [Word("won")] HeroesWon,

        [Word("lost")] HeroesLost,

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

        public virtual void Judged(Actor by, Actor target, Attempt attempt) { }

        public virtual void Dealt(Harm harm) { }

        public virtual void Casts(Actor caster, string spellKey, IReadOnlyList<Actor> at) { }

        public virtual void Cast(Actor caster, string spellKey, IReadOnlyList<Change> changes) { }

        public virtual void Opportunity(Actor attacker, Actor fleeing) { }

        public virtual void Reacted(Actor reactor, string reaction, Moment moment) { }

        public virtual void ConditionChanged(Actor actor, Condition condition, bool applied) { }

        public virtual void DoorOpened(Actor actor, Core.Space.Border door) { }

        public virtual void Downed(Actor actor) { }

        public virtual void Away(Actor actor, bool away) { }

        public virtual void DeathSaved(Actor actor, Attempt attempt) { }

        public virtual void TurnEnded(Turn turn) { }

        public virtual void Ended(Outcome outcome) { }
    }

    // several at once (ObserverList): a broken narrator must not lose the player's combat
    public sealed class Observers : ObserverList<ICombatObserver>, ICombatObserver
    {
        public Observers(params ICombatObserver[] watchers) : base(watchers)
        {
        }

        public void Began(IReadOnlyList<InitiativeRoll> order) => Each(w => w.Began(order));

        public void RoundBegan(int round) => Each(w => w.RoundBegan(round));

        public void TurnBegan(Turn turn) => Each(w => w.TurnBegan(turn));

        public void Moved(Actor actor, IReadOnlyList<Cell> route) => Each(w => w.Moved(actor, route));

        public void Struck(Blow blow) => Each(w => w.Struck(blow));

        public void Judged(Actor by, Actor target, Attempt attempt) => Each(w => w.Judged(by, target, attempt));

        public void Dealt(Harm harm) => Each(w => w.Dealt(harm));

        public void Casts(Actor caster, string spellKey, IReadOnlyList<Actor> at) => Each(w => w.Casts(caster, spellKey, at));

        public void Cast(Actor caster, string spellKey, IReadOnlyList<Change> changes) => Each(w => w.Cast(caster, spellKey, changes));

        public void Opportunity(Actor attacker, Actor fleeing) =>
            Each(w => w.Opportunity(attacker, fleeing));

        public void Reacted(Actor reactor, string reaction, Moment moment) =>
            Each(w => w.Reacted(reactor, reaction, moment));

        public void ConditionChanged(Actor actor, Condition condition, bool applied) =>
            Each(w => w.ConditionChanged(actor, condition, applied));

        public void DoorOpened(Actor actor, Core.Space.Border door) => Each(w => w.DoorOpened(actor, door));

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
        readonly LineLog<string> _log = new LineLog<string>();

        public IReadOnlyList<string> Lines => _log.Lines;

        public override void Began(IReadOnlyList<InitiativeRoll> order) =>
            _log.Add("initiative: " + string.Join(", ", order));

        public override void RoundBegan(int round) => _log.Add($"-- round {round}");

        public override void TurnBegan(Turn turn) => _log.Add($"{turn.Actor.Id} steps up");

        public override void Moved(Actor actor, IReadOnlyList<Cell> route) =>
            _log.Add($"{actor.Id} moves to {route[route.Count - 1]} " +
                       $"({route.Count - 1} squares)");

        public override void Struck(Blow blow) => _log.Add(blow.ToString());

        public override void Opportunity(Actor attacker, Actor fleeing) =>
            _log.Add($"{attacker.Id} takes a swing at {fleeing.Id} leaving its reach");

        public override void Reacted(Actor reactor, string reaction, Moment moment) =>
            _log.Add($"{reactor.Id} reacts with {reaction} to {moment}");

        public override void ConditionChanged(Actor actor, Condition condition, bool applied) =>
            _log.Add($"{actor.Id} is {(applied ? "now" : "no longer")} {condition.Id()}");

        public override void Downed(Actor actor) => _log.Add($"{actor.Id} goes down");

        public override void DoorOpened(Actor actor, Core.Space.Border door) => _log.Add($"{actor.Id} opens the door at {door}");

        public override void Away(Actor actor, bool away) =>
            _log.Add($"{actor.Id} {(away ? "is taken off the board" : "comes back")}");

        public override void DeathSaved(Actor actor, Attempt attempt) =>
            _log.Add($"{actor.Id} death save {attempt.Total}: " +
                       (attempt.Succeeded ? "back up" : "dead"));

        public override void Ended(Outcome outcome) => _log.Add($"== {outcome}");

        public override string ToString() => _log.ToString();
    }
}
