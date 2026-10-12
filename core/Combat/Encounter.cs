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
    //
    // WHAT IT HOLDS, each with its own books (cc_task_j-split-classes-dev-hooks.md Part 1): the turn order and the
    // round (TurnClock), the reaction window (ReactionDesk), the zones on the board (BoardZones), the grapples
    // (Grapples), who is off the board (OffTheBoard), the enemies' morale (MoraleWatch) and the conditions that
    // last until a creature's next turn (UntilNextTurn). What they set off is still said from here
    public sealed partial class Encounter
    {
        readonly IResolver _resolver;
        readonly Dictionary<Actor, ActionBudget> _budgets = new();
        readonly TurnClock _clock = new();
        readonly ReactionDesk _reactions;
        readonly BoardZones _zones = new();
        readonly Grapples _grapples = new();
        readonly OffTheBoard _off = new();
        readonly MoraleWatch _morale = new();
        readonly UntilNextTurn _untilNextTurn = new();

        public Encounter(IResolver resolver, Battlefield field, ICombatObserver observer = null)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            Field = field ?? throw new ArgumentNullException(nameof(field));
            Observer = observer ?? new Observers();
            _reactions = new ReactionDesk(this);

            // what only the fight knows about a step, put where every route asks (Battlefield.StepCost): a spell's
            // difficult ground, and dragging a grappled creature - one more square unless it is Tiny or two sizes
            // smaller (SRD 5.2.1 Grappled)
            Field.MadeRough = IsRough;
            Field.Drags = mover => Held(mover).Any(h => h.CurrentSize != Size.Tiny &&
                                                       (int)h.CurrentSize > (int)mover.CurrentSize - 2);
        }

        public Battlefield Field { get; }

        // what the fight is fought in, as tags a spell can read: "outdoors", "storm". set by
        // whoever starts the fight (the campaign's <<fight>>); empty is indoors and calm
        public ISet<string> Setting { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ICombatObserver Observer { get; }

        // the one resolver every roll in this fight goes through - a reaction rolls with it too,
        // so a scripted fight stays scripted all the way down
        public IResolver Resolver => _resolver;

        public int Round => _clock.Round;

        public Turn Current { get; private set; }

        public Outcome Outcome { get; private set; } = Outcome.Open;

        public bool Over => Outcome != Outcome.Open;

        public IReadOnlyList<InitiativeRoll> Order => _clock.Order;

        public IEnumerable<Actor> Actors => _clock.Order.Select(r => r.Actor);

        // WHERE A CREATURE IS IN THE TURN ORDER, the explicit last word wherever creatures tie (who a monster goes for,
        // who a spell picks): an id used to settle those, so renaming one changed who got hit
        // (cc_task_open-questions-answers.md 2.7). One not in the fight comes last
        public int InitiativeRank(Actor actor) => _clock.Rank(actor);

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
            if (_clock.Started) throw new InvalidOperationException("the fight already started");

            _clock.Start(Initiative.Roll(_resolver, Field.Pieces));

            Observer.Began(_clock.Order);

            BeginRound();
        }

        void BeginRound()
        {
            _clock.NextRound();

            // a reaction is spent between your turns, so it is the round that hands it back
            foreach (Actor actor in Actors)
                _reactions.HandBack(actor, BudgetFor(actor).ReactionsFor(Round));

            Observer.RoundBegan(Round);
        }

        // a corpse is rising as a statblock on its master's side: Finger of Death's Zombie. the
        // content layer knows statblocks, so it answers with Join
        public event Action<Actor, string, Actor> Rising;

        public void Raise(Actor corpse, string statblock, Actor master) =>
            Rising?.Invoke(corpse, statblock, master);

        // A CREATURE JOINING A FIGHT ALREADY UNDER WAY: it rolls initiative and takes its place in
        // the order - after the creature it belongs to, when it belongs to one
        public bool Join(Actor actor, Cell cell, ActionBudget budget = null, Actor after = null)
        {
            if (actor == null || !Field.Place(actor, cell)) return false;

            _budgets[actor] = budget ?? new ActionBudget();
            _reactions.HandBack(actor, _budgets[actor].ReactionsFor(Round));

            _clock.Insert(Initiative.Roll(_resolver, new[] { actor }).First(), after);

            Observer.Moved(actor, new[] { cell });
            return true;
        }

        // the next actor that can still take a turn, or null when the fight is over. an actor that
        // is down is skipped, but it still gets its death save first.
        public Turn Next()
        {
            if (Over) return null;

            if (Current != null && !Current.Ended) Close(Current);

            while (true)
            {
                if (Judge() != Outcome.Open) return null;

                if (_clock.RoundDone)
                {
                    BeginRound();

                    if (Judge() != Outcome.Open) return null;
                }

                Actor actor = _clock.Advance();

                // sent away for good (Banishment held for its full minute): no more turns
                if (_off.IsGone(actor)) continue;

                if (actor.IsDown && StaysDown(actor)) continue;

                if (Starts(actor)) return Current;
            }
        }

        // down as its turn comes round: the hero gets the single d20 >= 10; a monster at 0 is simply out.
        // true when it is still down, and so has no turn
        bool StaysDown(Actor actor)
        {
            // a Stable hero rolls no death save: it lies there until it is healed
            if (actor.Side == Allegiance.Hero && !actor.Stable)
            {
                Attempt save = Checks.DeathSave(_resolver, actor);
                Observer.DeathSaved(actor, save);

                if (!save.Succeeded) Field.Remove(actor);
            }

            // the save is thrown at the start of the turn (SRD 5.2.1), so a hero it brings
            // back up takes that turn - skipping it left a revived hero at 1 hit point with
            // nothing to do but be knocked down again, forever
            return actor.IsDown;
        }

        // A TURN STARTS: what lasts until it ends, the zones' and the spells' moments, and then it is the creature's.
        // true when it is the creature's to play; false when something already spent it or took the creature out
        // (cc_task_j Part 3 split Next along these steps)
        bool Starts(Actor actor)
        {
            // SRD's "until the start of your next turn" - a Shield ends here, not at the end
            // of the round and not at the end of the fight - and whatever lasts until the end
            // of this actor's next turn starts counting from now. on every actor, because a
            // glimmer on a goblin can be the caster's to end
            foreach (Actor any in Actors) any.Boons.TurnStarting(actor, any);

            // a zone's "once per turn" is per turn, so every turn starts with a clean slate
            _zones.NewTurn();
            TickZones(actor, starting: true);

            Current = new Turn(actor, BudgetFor(actor), Round);

            // the spell books first - a blinding that lasted until this creature's turn ends
            // here, a burning bites here - then a zone that bites at the start of a turn. any
            // of them can drop the creature before it acts
            TurnStarting?.Invoke(Current);

            PulseWhereItStands(actor, Pulses.StartTurn);

            // sent away for good as its turn began (Banishment's full minute)
            if (actor.IsDown || _off.IsGone(actor))
            {
                Current.End();
                Lapsing(actor);
                return false;
            }

            // a turn a spell decided for it and has already spent - Command's Grovel or Halt
            if (Current.Ended)
            {
                Lapsing(actor);
                Observer.TurnBegan(Current);
                Observer.TurnEnded(Current);
                return false;
            }

            // a dropped weapon within reach is picked up for free
            PickUp(actor);

            Observer.TurnBegan(Current);

            return true;
        }

        public void EndTurn()
        {
            if (Current == null || Current.Ended) return;

            Close(Current);
            Observer.TurnEnded(Current);
        }

        // a turn spent for the creature before it could act: the end-of-turn rules still run
        public void Forfeit(Turn turn)
        {
            if (turn == null || turn.Ended || !ReferenceEquals(turn, Current)) return;

            Close(turn);
        }

        // SRD's one free object interaction a turn: a dropped weapon on the creature's square or
        // beside it is back in hand
        bool PickUp(Actor actor)
        {
            if (!actor.Disarmed || !actor.DroppedAt.HasValue) return false;

            if (!(Field.Where(actor) is Cell here) ||
                Battlefield.Distance(here, actor.DroppedAt.Value) > 1) return false;

            return actor.Rearm();
        }

        // FORCED MOVEMENT: a creature put somewhere by something else - Telekinesis. no
        // movement is spent and nobody gets an opportunity attack (SRD: those are for a creature
        // that moves itself), but stepping into a zone is stepping into it
        public bool Shove(Actor actor, Cell to)
        {
            if (actor == null || !(Field.Where(actor) is Cell from)) return false;

            if (from == to) return true;

            if (Field.Occupies(to, actor) || !Field.Map.IsPassable(to)) return false;

            if (!Field.Place(actor, to)) return false;

            PulseStep(actor, from, to);

            Observer.Moved(actor, new[] { from, to });
            Moved?.Invoke(actor);

            LetGo();

            return true;
        }

        // a statblock's condition that lasts "until the end of its next turn" (UntilNextTurn)
        void Lapsing(Actor actor)
        {
            foreach (Condition condition in _untilNextTurn.Ending(actor)) actor.Remove(condition);
        }

        void Close(Turn turn)
        {
            turn.End();

            Lapsing(turn.Actor);

            PulseWhereItStands(turn.Actor, Pulses.EndTurn);

            TurnEnding?.Invoke(turn);

            foreach (Actor any in Actors) any.Boons.TurnEnding(turn.Actor, any);

            TickZones(turn.Actor, starting: false);
        }

        // for whatever keeps its own books by the turn - the spell engine's repeat saves. the
        // fight says when; what happens is theirs
        public event Action<Turn> TurnStarting;

        public event Action<Turn> TurnEnding;

        // somebody changed squares - what an aura has to hear to know who is inside it
        public event Action<Actor> Moved;


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
                // the one step cost the route was found by (Battlefield.StepCost): difficult ground, a spell's
                // included, crawling and dragging
                List<Actor> dragged = Held(turn.Actor);
                int cost = Field.StepCost(turn.Actor, route[i]) * Turn.FeetPerSquare;

                Cell from = walked[walked.Count - 1];

                // Frightened: not one step closer to what it fears (the route already keeps to this)
                if (Field.Forbids(turn.Actor, from, route[i])) break;

                if (!turn.Take(Spend.Movement, cost)) break;

                // opportunity attacks resolve against the square being left, before the step, so a
                // hit that drops the mover stops it where it stood. a Disengage means nobody is
                // offered one at all
                if (!turn.Disengaged) Provoke(turn.Actor, from, route[i]);

                if (turn.Actor.IsDown) break;

                IReadOnlyDictionary<IZone, List<Actor>> carried = CaughtByZonesOf(turn.Actor);

                int speedBefore = turn.Actor.Moves;

                Field.Place(turn.Actor, route[i]);
                walked.Add(route[i]);

                // the grappled come along, into the square it left
                foreach (Actor held in dragged)
                    if (Field.Distance(turn.Actor, held) > 1) Shove(held, from);

                // walking past a dropped weapon picks it up on the way
                PickUp(turn.Actor);

                // stepping into a zone, or across one that bites per square; and an emanation the
                // mover carries washing over whoever it now reaches
                PulseStep(turn.Actor, from, route[i]);
                PulseCarried(carried);

                // an aura that changes speed (Spirit Guardians' halving) is asked after every step,
                // and SRD's rule for a speed that changes mid-move applies: what is left is the new
                // speed less what has been used
                Stepped?.Invoke(turn.Actor);
                turn.SpeedChanged(speedBefore, turn.Actor.Moves);

                if (turn.Actor.IsDown) break;
            }

            if (walked.Count > 1)
            {
                Observer.Moved(turn.Actor, walked);
                Moved?.Invoke(turn.Actor);
            }

            LetGo();

            return walked;
        }
    }
}
