using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Combat
{
    // one fight. owns the turn order, the round counter, the action economy and the reactions -
    // everything that needs to be true *between* two actors rather than inside one.
    //
    // it does not decide what anybody does. the UI drives the hero's turn and an ITactics drives
    // everyone else's; this is the referee, not a player.
    public sealed class Encounter
    {
        readonly IResolver _resolver;
        readonly Dictionary<Actor, ActionBudget> _budgets = new();
        readonly Dictionary<Actor, int> _reactions = new();
        readonly List<InitiativeRoll> _order = new();

        public Encounter(IResolver resolver, Battlefield field, ICombatObserver observer = null)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            Field = field ?? throw new ArgumentNullException(nameof(field));
            Observer = observer ?? new Observers();
        }

        public Battlefield Field { get; }

        public ICombatObserver Observer { get; }

        public int Round { get; private set; }

        public Turn Current { get; private set; }

        public Outcome Outcome { get; private set; } = Outcome.Open;

        public bool Over => Outcome != Outcome.Open;

        public IReadOnlyList<InitiativeRoll> Order => _order;

        public IEnumerable<Actor> Actors => _order.Select(r => r.Actor);

        int _next;

        public ActionBudget BudgetFor(Actor actor) =>
            _budgets.TryGetValue(actor, out ActionBudget budget) ? budget : null;

        public void Enlist(Actor actor, Cell cell, ActionBudget budget = null)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));

            if (!Field.Place(actor, cell))
                throw new ArgumentException(
                    $"{actor.Id} cannot stand on {cell} - it is off the map, solid, or taken",
                    nameof(cell));

            _budgets[actor] = budget ?? new ActionBudget();
        }

        public void Begin()
        {
            if (_order.Count > 0) throw new InvalidOperationException("the fight already started");

            foreach (InitiativeRoll roll in Initiative.Roll(_resolver, Field.Pieces))
                _order.Add(roll);

            Round = 0;
            _next = 0;

            Observer.Began(_order);

            BeginRound();
        }

        void BeginRound()
        {
            Round++;
            _next = 0;

            // a reaction is spent between your turns, so it is the round that hands it back
            foreach (Actor actor in Actors)
                _reactions[actor] = BudgetFor(actor).ReactionsFor(Round);

            Observer.RoundBegan(Round);
        }

        // the next actor that can still take a turn, or null when the fight is over. an actor that
        // is down is skipped, but it still gets its death save first.
        public Turn Next()
        {
            if (Over) return null;

            if (Current != null && !Current.Ended) Current.End();

            while (true)
            {
                if (Judge() != Outcome.Open) return null;

                if (_next >= _order.Count)
                {
                    BeginRound();

                    if (Judge() != Outcome.Open) return null;
                }

                Actor actor = _order[_next].Actor;
                _next++;

                if (actor.IsDown)
                {
                    // the hero gets the single d20 >= 10; a monster at 0 is simply out
                    if (actor.Side == Allegiance.Hero)
                    {
                        Attempt save = Checks.DeathSave(_resolver, actor);
                        Observer.DeathSaved(actor, save);

                        if (!save.Succeeded) Field.Remove(actor);
                    }

                    continue;
                }

                Current = new Turn(actor, BudgetFor(actor), Round);
                Observer.TurnBegan(Current);

                return Current;
            }
        }

        public void EndTurn()
        {
            if (Current == null) return;

            Current.End();
            Observer.TurnEnded(Current);
        }


        // --- what an actor can do on its turn ---------------------------------------------------

        // walks as far along the route as the turn's movement pays for, taking an opportunity
        // attack from every enemy whose reach it steps out of. returns the squares actually
        // covered, which is what the mini animates along.
        public IReadOnlyList<Cell> Walk(Turn turn, Cell destination)
        {
            if (turn == null || turn.Ended) return Array.Empty<Cell>();

            IReadOnlyList<Cell> route = Field.RouteFor(turn.Actor, destination);

            if (route == null || route.Count < 2) return Array.Empty<Cell>();

            if (!turn.Can(Spend.Movement, Turn.FeetPerSquare)) return Array.Empty<Cell>();

            var walked = new List<Cell> { route[0] };

            for (int i = 1; i < route.Count; i++)
            {
                int cost = Field.Map.At(route[i]).MoveCost() * Turn.FeetPerSquare;

                if (!turn.Take(Spend.Movement, cost)) break;

                Cell from = walked[walked.Count - 1];

                // opportunity attacks resolve against the square being left, before the step, so a
                // hit that drops the mover stops it where it stood
                Provoke(turn.Actor, from, route[i]);

                Field.Place(turn.Actor, route[i]);
                walked.Add(route[i]);

                if (turn.Actor.IsDown) break;
            }

            if (walked.Count > 1) Observer.Moved(turn.Actor, walked);

            return walked;
        }

        // SRD, trimmed to v1: leaving a hostile's reach hands it one attack, and nothing else in
        // the game is a reaction (decisions_checklist.md section 6).
        void Provoke(Actor mover, Cell from, Cell to)
        {
            foreach (Actor watcher in Field.Enemies(mover).ToList())
            {
                Cell? standing = Field.Where(watcher);

                if (!standing.HasValue) continue;

                Attack swing = OpportunityAttackOf(watcher);

                if (swing == null) continue;

                int reach = swing.Reach;

                bool wasInReach = Battlefield.Distance(standing.Value, from) <= reach;
                bool stillInReach = Battlefield.Distance(standing.Value, to) <= reach;

                if (!wasInReach || stillInReach) continue;

                if (!TakeReaction(watcher)) continue;

                Observer.Opportunity(watcher, mover);

                Blow blow = Strike.Make(_resolver, watcher, mover, swing);
                Observer.Struck(blow);

                if (blow.Downed) Observer.Downed(mover);
            }
        }

        // what an actor swings with when something runs past. set by the content layer; without
        // one, an actor simply doesn't take opportunity attacks
        readonly Dictionary<Actor, Attack> _opportunity = new();

        public void ArmOpportunity(Actor actor, Attack attack)
        {
            if (actor == null) return;

            _opportunity[actor] = attack;
        }

        public Attack OpportunityAttackOf(Actor actor) =>
            actor != null && !actor.IsDown && actor.CanAct &&
            _opportunity.TryGetValue(actor, out Attack attack)
                ? attack
                : null;

        public int ReactionsLeft(Actor actor) =>
            _reactions.TryGetValue(actor, out int left) ? left : 0;

        public bool TakeReaction(Actor actor)
        {
            if (actor == null || ReactionsLeft(actor) <= 0) return false;

            _reactions[actor]--;
            return true;
        }

        public Blow Hit(Turn turn, Actor target, Attack attack,
                        Spend spend = Spend.Action,
                        Advantage extra = Advantage.Flat,
                        IReadOnlyList<Rider> riders = null)
        {
            if (turn == null || turn.Ended || target == null || attack == null) return null;

            if (!Field.InRange(turn.Actor, target, attack.Reaches)) return null;

            if (!turn.Take(spend)) return null;

            // a shot past its normal range is at disadvantage - the only range band v1 keeps
            Advantage band = attack.IsRanged &&
                             Field.Distance(turn.Actor, target) > attack.Range
                ? Advantage.Disadvantage
                : Advantage.Flat;

            // SRD: shooting into melee is a hindrance, and so is a prone target you cannot reach
            if (attack.IsRanged && Field.Adjacent(Field.Where(turn.Actor).Value)
                                        .Any(a => a.Side != turn.Actor.Side && !a.IsDown))
                band = band.And(Advantage.Disadvantage);

            if (target.Has(Condition.Prone) &&
                Field.Distance(turn.Actor, target) > 1)
                band = band.And(Advantage.Disadvantage);

            Blow blow = Strike.Make(_resolver, turn.Actor, target, attack, band.And(extra), riders);

            Observer.Struck(blow);

            if (blow.Downed) Observer.Downed(target);

            return blow;
        }

        public bool Afflict(Actor actor, Condition condition)
        {
            if (actor == null || !actor.Apply(condition)) return false;

            Observer.ConditionChanged(actor, condition, true);

            return true;
        }

        public bool Relieve(Actor actor, Condition condition)
        {
            if (actor == null || !actor.Remove(condition)) return false;

            Observer.ConditionChanged(actor, condition, false);

            return true;
        }

        public void Flee()
        {
            Outcome = Outcome.Fled;
            Observer.Ended(Outcome);
        }


        // --- who won ----------------------------------------------------------------------------

        public Outcome Judge()
        {
            if (Over) return Outcome;

            // a hero on the floor has not lost yet - the death save is still to come, and judging
            // the fight before that die lands would skip the whole mechanic
            bool heroesStanding = _order.Any(r => r.Actor.Side == Allegiance.Hero && !r.Actor.IsDead);
            bool enemiesStanding = _order.Any(r => r.Actor.Side == Allegiance.Enemy && !r.Actor.IsDown);

            if (!heroesStanding) Outcome = Outcome.HeroesLost;
            else if (!enemiesStanding) Outcome = Outcome.HeroesWon;

            if (Over) Observer.Ended(Outcome);

            return Outcome;
        }

        public override string ToString() =>
            $"round {Round}, {_order.Count(r => !r.Actor.IsDown)} of {_order.Count} still up, " +
            Outcome;
    }
}
