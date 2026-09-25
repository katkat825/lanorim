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

        // what the fight is fought in, as tags a spell can read: "outdoors", "storm". set by
        // whoever starts the fight (the campaign's <<fight>>); empty is indoors and calm
        public ISet<string> Setting { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ICombatObserver Observer { get; }

        // the one resolver every roll in this fight goes through - a reaction rolls with it too,
        // so a scripted fight stays scripted all the way down
        public IResolver Resolver => _resolver;

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

            if (Current != null && !Current.Ended) Close(Current);

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
                    if (actor.IsDown) continue;
                }

                // SRD's "until the start of your next turn" - a Shield ends here, not at the end
                // of the round and not at the end of the fight - and whatever lasts until the end
                // of this actor's next turn starts counting from now. on every actor, because a
                // glimmer on a goblin can be the caster's to end
                foreach (Actor any in Actors) any.Boons.TurnStarting(actor, any);

                // a zone's "once per turn" is per turn, so every turn starts with a clean slate
                _pulsedThisTurn.Clear();
                TickZones(actor, starting: true);

                Current = new Turn(actor, BudgetFor(actor), Round);

                // the spell books first - a blinding that lasted until this creature's turn ends
                // here, a burning bites here - then a zone that bites at the start of a turn. any
                // of them can drop the creature before it acts
                TurnStarting?.Invoke(Current);

                PulseWhereItStands(actor, Pulse.StartTurn);

                if (actor.IsDown)
                {
                    Current.End();
                    continue;
                }

                // a turn a spell decided for it and has already spent - Command's Grovel or Halt
                if (Current.Ended)
                {
                    Observer.TurnBegan(Current);
                    Observer.TurnEnded(Current);
                    continue;
                }

                // a dropped weapon within reach is picked up for free
                PickUp(actor);

                Observer.TurnBegan(Current);

                return Current;
            }
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

            return true;
        }

        void Close(Turn turn)
        {
            turn.End();

            PulseWhereItStands(turn.Actor, Pulse.EndTurn);

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
                // difficult terrain is double, whether the map made it or a spell did, and the two
                // do not stack - SRD's difficult terrain is a yes or a no
                bool rough = Field.Map.At(route[i]).MoveCost() > 1 || IsRough(route[i], turn.Actor);
                int cost = (rough ? 2 : 1) * Turn.FeetPerSquare;

                if (!turn.Take(Spend.Movement, cost)) break;

                Cell from = walked[walked.Count - 1];

                // opportunity attacks resolve against the square being left, before the step, so a
                // hit that drops the mover stops it where it stood. a Disengage means nobody is
                // offered one at all
                if (!turn.Disengaged) Provoke(turn.Actor, from, route[i]);

                if (turn.Actor.IsDown) break;

                IReadOnlyDictionary<IZone, List<Actor>> carried = CaughtByZonesOf(turn.Actor);

                Field.Place(turn.Actor, route[i]);
                walked.Add(route[i]);

                // walking past a dropped weapon picks it up on the way
                PickUp(turn.Actor);

                // stepping into a zone, or across one that bites per square; and an emanation the
                // mover carries washing over whoever it now reaches
                PulseStep(turn.Actor, from, route[i]);
                PulseCarried(carried);

                if (turn.Actor.IsDown) break;
            }

            if (walked.Count > 1)
            {
                Observer.Moved(turn.Actor, walked);
                Moved?.Invoke(turn.Actor);
            }

            return walked;
        }

        // --- zones ---------------------------------------------------------------------------------

        sealed class Placed
        {
            public Placed(IZone zone, Duration duration)
            {
                Zone = zone;
                Duration = duration;
            }

            public IZone Zone { get; }

            public Duration Duration { get; }

            public bool Armed { get; set; }
        }

        readonly List<Placed> _zones = new();

        // who a zone has already done its once-a-turn thing to this turn
        readonly HashSet<(IZone, Actor)> _pulsedThisTurn = new();

        public IReadOnlyList<IZone> Zones => _zones.Select(p => p.Zone).ToList();

        // a zone arrives. whoever is already inside and does not get a pass is hit by the
        // appearing, if the zone is one that does that
        public void AddZone(IZone zone, Duration duration = Duration.Concentration)
        {
            if (zone == null) return;

            _zones.Add(new Placed(zone, duration));

            if (!zone.Pulses.Has(Pulse.Appear)) return;

            foreach (Actor creature in CaughtIn(zone).ToList())
                Once(zone, creature, Pulse.Appear);
        }

        // one zone gone, whatever made it - a Sunburst burning away a Darkness
        public bool EndZone(IZone zone)
        {
            if (_zones.RemoveAll(p => ReferenceEquals(p.Zone, zone)) == 0) return false;

            zone.Removed(this);
            return true;
        }

        // ends every zone from one source that one owner made - a concentration dropped
        public int EndZones(string source, Actor owner)
        {
            List<Placed> going = _zones.Where(p => p.Zone.Source == source &&
                                                   ReferenceEquals(p.Zone.Owner, owner)).ToList();

            foreach (Placed placed in going)
            {
                _zones.Remove(placed);
                placed.Zone.Removed(this);
            }

            return going.Count;
        }

        public bool Affects(IZone zone, Actor creature) =>
            creature != null && !creature.IsDown &&
            !(zone.SparesAllies && zone.Owner != null && creature.Side == zone.Owner.Side) &&
            !(zone.AlliesOnly && zone.Owner != null && creature.Side != zone.Owner.Side);

        public IEnumerable<Actor> CaughtIn(IZone zone) =>
            Field.Pieces.Where(a => Field.Where(a) is Cell c && zone.Covers(Field, c) &&
                                    Affects(zone, a));

        public bool IsRough(Cell cell, Actor mover) =>
            _zones.Any(p => p.Zone.Rough && p.Zone.Core(Field, cell) && Affects(p.Zone, mover));

        // a zone that was picked up and put down somewhere else - Moonbeam's beam, a Flaming
        // Sphere rolled across the floor - washes over whoever it now covers that it did not
        public void ZoneMoved(IZone zone, IEnumerable<Actor> wereIn)
        {
            if (zone == null) return;

            var before = new HashSet<Actor>(wereIn ?? Enumerable.Empty<Actor>());

            foreach (Actor creature in CaughtIn(zone).Where(a => !before.Contains(a)).ToList())
                if (zone.Pulses.Has(Pulse.Enter)) Once(zone, creature, Pulse.Enter);
        }

        // SRD's "only once per turn": Appear, Enter, StartTurn and EndTurn share it. EachSquare
        // does not, because Spike Growth's whole point is that every step hurts
        void Once(IZone zone, Actor creature, Pulse pulse)
        {
            if (!_pulsedThisTurn.Add((zone, creature)) && !zone.EachTime) return;

            zone.Act(this, creature, pulse);
        }

        void PulseWhereItStands(Actor creature, Pulse pulse)
        {
            if (!(Field.Where(creature) is Cell here)) return;

            foreach (Placed placed in _zones.ToList())
                if (placed.Zone.Pulses.Has(pulse) && placed.Zone.Covers(Field, here) &&
                    Affects(placed.Zone, creature))
                    Once(placed.Zone, creature, pulse);
        }

        void PulseStep(Actor mover, Cell from, Cell to)
        {
            foreach (Placed placed in _zones.ToList())
            {
                IZone zone = placed.Zone;

                if (!zone.Covers(Field, to) || !Affects(zone, mover)) continue;

                if (zone.Pulses.Has(Pulse.EachSquare)) zone.Act(this, mover, Pulse.EachSquare);

                if (zone.Pulses.Has(Pulse.Enter) && !zone.Covers(Field, from))
                    Once(zone, mover, Pulse.Enter);

                if (mover.IsDown) return;
            }
        }

        // the zones the mover carries (an emanation around its owner), with who each covers now
        IReadOnlyDictionary<IZone, List<Actor>> CaughtByZonesOf(Actor mover) =>
            _zones.Where(p => ReferenceEquals(p.Zone.Owner, mover))
                  .ToDictionary(p => p.Zone, p => CaughtIn(p.Zone).ToList());

        void PulseCarried(IReadOnlyDictionary<IZone, List<Actor>> before)
        {
            foreach (KeyValuePair<IZone, List<Actor>> carried in before)
                ZoneMoved(carried.Key, carried.Value);
        }

        // a zone that lasts until its owner's next turn starts or ends, counted the way boons are
        void TickZones(Actor whose, bool starting)
        {
            foreach (Placed placed in _zones.Where(p => ReferenceEquals(p.Zone.Owner, whose)).ToList())
            {
                if (starting && placed.Duration == Duration.NextTurn)
                {
                    _zones.Remove(placed);
                    placed.Zone.Removed(this);
                }
                else if (starting && placed.Duration == Duration.NextTurnEnd) placed.Armed = true;
                else if (!starting && placed.Duration == Duration.NextTurnEnd && placed.Armed)
                {
                    _zones.Remove(placed);
                    placed.Zone.Removed(this);
                }
            }
        }


        // leaving a hostile's reach offers it a reaction. the opportunity attack is one of the
        // things it might answer with, and no longer the only one
        void Provoke(Actor mover, Cell from, Cell to)
        {
            foreach (Actor watcher in Field.Enemies(mover).ToList())
                Offer(Moment.Leaving(mover, watcher, from, to), watcher);
        }


        // --- the reaction window ---------------------------------------------------------------

        // what each actor can answer a moment with, and who decides whether it does. the content
        // layer arms these; an actor with none simply never reacts
        readonly Dictionary<Actor, List<IReaction>> _answers = new();
        readonly Dictionary<Actor, IReactionChooser> _choosers = new();

        public void Arm(Actor actor, IReaction reaction)
        {
            if (actor == null || reaction == null) return;

            if (!_answers.TryGetValue(actor, out List<IReaction> list))
                _answers[actor] = list = new List<IReaction>();

            // one of each: arming the same opportunity attack twice must not offer it twice
            list.RemoveAll(r => r.Id == reaction.Id);
            list.Add(reaction);
        }

        public IReadOnlyList<IReaction> ReactionsOf(Actor actor) =>
            actor != null && _answers.TryGetValue(actor, out List<IReaction> list)
                ? list
                : (IReadOnlyList<IReaction>)Array.Empty<IReaction>();

        // without one, an actor takes the first reaction offered - what the opportunity attack
        // always did, so a fight nobody configured plays the way it used to
        public void ChooseReactionsWith(Actor actor, IReactionChooser chooser)
        {
            if (actor == null) return;

            _choosers[actor] = chooser ?? ReactionChoosers.Always;
        }

        IReactionChooser ChooserOf(Actor actor) =>
            _choosers.TryGetValue(actor, out IReactionChooser chooser)
                ? chooser
                : ReactionChoosers.Always;

        // THE WINDOW. offered to each of `who` in turn: the reactions it has that answer this
        // trigger and can answer this particular moment, and its chooser picks one or none.
        // choosing spends the reaction - one per creature per round, whatever it was spent on -
        // and the engine does what was chosen without second-guessing it. stops early once the
        // moment is stopped, because there is nothing left to react to.
        public void Offer(Moment moment, params Actor[] who) =>
            Offer(moment, (IEnumerable<Actor>)who);

        public void Offer(Moment moment, IEnumerable<Actor> who)
        {
            if (moment == null || who == null) return;

            foreach (Actor reactor in who.Where(a => a != null).ToList())
            {
                if (moment.Stopped) return;

                if (!reactor.CanAct) continue;

                // SRD 5.2.1's smites are bonus actions taken in answer to a moment rather than
                // reactions: they are paid from the reactor's own turn, and only on it
                List<IReaction> options = ReactionsOf(reactor)
                    .Where(r => r.Trigger == moment.Trigger && CanPay(reactor, r) &&
                                r.CanAnswer(this, reactor, moment))
                    .ToList();

                if (options.Count == 0) continue;

                IReaction chosen = ChooserOf(reactor).Choose(this, reactor, moment, options);

                // a chooser may only pick from what it was offered
                if (chosen == null || !options.Contains(chosen)) continue;

                if (!Pay(reactor, chosen)) continue;

                Observer.Reacted(reactor, chosen.Id, moment);

                chosen.Answer(this, reactor, moment);
            }
        }

        // what an actor swings with when something runs past. set by the content layer; without
        // one, an actor simply doesn't take opportunity attacks
        public void ArmOpportunity(Actor actor, Attack attack)
        {
            if (actor == null || attack == null) return;

            Arm(actor, new OpportunityAttack(attack));
        }

        public Attack OpportunityAttackOf(Actor actor) =>
            actor != null && !actor.IsDown && actor.CanAct
                ? ReactionsOf(actor).OfType<OpportunityAttack>().FirstOrDefault()?.Attack
                : null;

        // a reaction is the round's; a bonus action taken in answer is the reactor's own turn's
        bool CanPay(Actor reactor, IReaction reaction) =>
            reaction.Cost == Spend.Bonus
                ? Current != null && !Current.Ended && ReferenceEquals(Current.Actor, reactor) &&
                  Current.Can(Spend.Bonus)
                : ReactionsLeft(reactor) > 0 && !reactor.Boons.NoReactions;

        bool Pay(Actor reactor, IReaction reaction) =>
            reaction.Cost == Spend.Bonus ? Current.Take(Spend.Bonus) : TakeReaction(reactor);

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

            // a dropped weapon is not in hand
            if (!turn.Actor.CanUse(attack)) return null;

            // SRD 5.2.1 Charmed: it can't attack the charmer
            if (turn.Actor.HasFrom(Condition.Charmed, target)) return null;

            // Slow's "only one attack", and Haste's extra action buying a single weapon attack
            if (!turn.TakeAttack(spend)) return null;

            // attacking gives away where you were hiding - Strike.Roll spends the hidden boon after
            // its advantage has been read, not before
            return Swing(turn.Actor, target, attack, Band(turn.Actor, target, attack).And(extra),
                         riders);
        }

        // what the range does to an attack roll: a shot past its normal range is at disadvantage
        // (the only range band v1 keeps), and so is a shot with an enemy beside you
        public Advantage Band(Actor attacker, Actor target, Attack attack)
        {
            if (attack == null || !attack.IsRanged) return Advantage.Flat;

            bool far = Field.Distance(attacker, target) > attack.Range;

            bool crowded = Field.Where(attacker) is Cell here &&
                           Field.Adjacent(here).Any(a => a.Side != attacker.Side && !a.IsDown);

            return far || crowded ? Advantage.Disadvantage : Advantage.Flat;
        }

        // one attack, start to finish, with both reaction windows in it: the target may answer the
        // hit before it lands, and answer the damage after. everything that swings in a fight goes
        // through here - a turn's attack and an opportunity attack alike
        public Blow Swing(Actor attacker, Actor target, Attack attack,
                          Advantage extra = Advantage.Flat, IReadOnlyList<Rider> riders = null)
        {
            bool close = Field.Distance(attacker, target) <= 1;
            int cover = Cover(attacker, target);

            Attempt attempt = Strike.Roll(_resolver, attacker, target, attack, extra, close,
                                          Sees(attacker, target), Sees(target, attacker), cover);

            if (attempt.Succeeded)
            {
                Offer(Moment.Hit(attacker, target, attempt), target);

                // the roll stays; the armor class it has to beat may not have
                attempt = attempt.Rejudged(target.ArmorClass + cover);
            }

            attempt = Strike.Decoyed(_resolver, attacker, target, attempt);

            // the statblock's own riders ride with whatever the swing brought
            if (attack.OnHit.Count > 0)
                riders = (riders ?? Array.Empty<Rider>()).Concat(attack.OnHit).ToList();

            Blow blow = Strike.Land(_resolver, attacker, target, attack, attempt, riders);

            Observer.Struck(blow);

            if (blow.Downed) Observer.Downed(target);

            if (blow.Hit && blow.Suffered > 0) Hurt(attacker, target, blow.Suffered);

            // SRD 5.2.1's smites: a bonus action taken right after the attacker's own hit
            if (blow.Hit) Offer(Moment.Struck(attacker, target, blow), attacker);

            return blow;
        }

        // hit points came off a creature. whatever keeps books on damage hears it first (a Sleep
        // ends, a Hideous Laughter saves again), then the creature is offered its reaction to
        // being hurt if it is still standing to take one
        public void Hurt(Actor attacker, Actor target, int suffered)
        {
            if (target == null || suffered <= 0) return;

            Damaged?.Invoke(attacker, target, suffered);

            if (!target.IsDown) Offer(Moment.Damaged(attacker, target, suffered), target);
        }

        // (attacker, target, hit points lost). the attacker may be null - a zone with nobody's
        // hand on it, a fall
        public event Action<Actor, Actor, int> Damaged;

        // somebody at 0 hit points, and not dead, is made Stable
        public bool Stabilize(Actor creature) => creature != null && creature.Stabilize();

        // a creature's condition changed outside a turn's own actions - a spell's expiry, a save
        public void Changed(Actor creature, Condition condition, bool gained) =>
            Observer.ConditionChanged(creature, condition, gained);


        // --- sight ------------------------------------------------------------------------------

        // SRD 5.2.1 sight on a board: the creature's own eyes (Blinded, Invisible, Truesight) and
        // a clear line between the two squares. heavily obscured squares on the line - fog,
        // magical darkness - block it the way a wall does, except to Truesight in magical
        // darkness. obscurers are the zones that say so
        public bool Sees(Actor looker, Actor seen)
        {
            if (looker == null || seen == null) return false;

            if (ReferenceEquals(looker, seen)) return true;

            if (!looker.CanSee(seen)) return false;

            Cell? from = Field.Where(looker);
            Cell? to = Field.Where(seen);

            if (!from.HasValue || !to.HasValue) return true;

            if (!Field.CanSee(from.Value, to.Value)) return false;

            return !Obscured(from.Value, to.Value, looker);
        }

        // a heavily obscured square anywhere on the line, the two ends included
        public bool Obscured(Cell from, Cell to, Actor looker = null)
        {
            List<IZone> blinding = _zones.Select(p => p.Zone)
                                         .Where(z => z.Obscures == Obscurement.Heavy)
                                         .Where(z => !(z.Magical && looker != null &&
                                                       looker.Boons.Truesight))
                                         .ToList();

            if (blinding.Count == 0) return false;

            foreach (Cell cell in Sight.Between(from, to))
                if (blinding.Any(z => z.Core(Field, cell)))
                    return true;

            return false;
        }

        // SRD cover from a zone standing between the two: Blade Barrier's three-quarters, +5.
        // the squares the attacker and the target stand in do not count - it has to be between
        public int Cover(Actor attacker, Actor target)
        {
            if (!(Field.Where(attacker) is Cell from) || !(Field.Where(target) is Cell to))
                return 0;

            List<IZone> covering = _zones.Select(p => p.Zone).Where(z => z.Cover > 0).ToList();

            if (covering.Count == 0) return 0;

            int best = 0;

            foreach (Cell cell in Sight.Between(from, to))
            {
                if (cell == from || cell == to) continue;

                foreach (IZone zone in covering)
                    if (zone.Core(Field, cell))
                        best = Math.Max(best, zone.Cover);
            }

            return best;
        }


        // --- the other things a turn can be spent on -------------------------------------------

        // SRD: Dash doubles your movement for the turn - another turn's worth of speed on top
        public bool Dash(Turn turn, Spend spend = Spend.Action)
        {
            if (!CanSpendOn(turn, spend, Manoeuvre.Dash)) return false;

            turn.Hasten();
            return true;
        }

        // SRD: Disengage - your movement provokes no opportunity attacks for the rest of the turn
        public bool Disengage(Turn turn, Spend spend = Spend.Action)
        {
            if (!CanSpendOn(turn, spend, Manoeuvre.Disengage)) return false;

            turn.Disengage();
            return true;
        }

        public const string Hidden = "hidden";

        // SRD 5.2.1 Hide: a DC 15 Dexterity (Stealth) check. on a success you are unseen, which
        // v1 reads as the Invisible condition's two halves - advantage on your attacks, and
        // disadvantage on attacks against you - until you attack or the fight ends. v1 has no
        // Invisible condition (the six-condition subset), so it rides on a boon the fight lifts.
        // being seen by a searcher is campaign narration; the fight does not model searching.
        public Attempt Hide(Turn turn, Spend spend = Spend.Action)
        {
            if (!CanSpendOn(turn, spend, Manoeuvre.Hide)) return null;

            Attempt attempt = Checks.Check(_resolver, turn.Actor, Skill.Stealth, HideDc);

            if (attempt.Succeeded)
                turn.Actor.Boons.Add(new Boon(Hidden, Hidden, Duration.Encounter,
                                              advantageOnAttacks: true,
                                              disadvantageAgainst: true)
                                     { EndsOnAttack = true });

            return attempt;
        }

        public const int HideDc = 15;

        // Dash, Disengage and Hide are actions anyone can take. a bonus action may only be spent
        // on one when a feature says so - the Rogue's Cunning Action
        bool CanSpendOn(Turn turn, Spend spend, Manoeuvre manoeuvre)
        {
            if (turn == null || turn.Ended || Over) return false;

            if (spend == Spend.Bonus && !turn.Actor.QuickOnBonus.HasFlag(manoeuvre)) return false;

            if (spend != Spend.Action && spend != Spend.Bonus) return false;

            // Haste's extra action may be a Dash, a Disengage or a Hide
            return turn.Take(spend) || spend == Spend.Action && turn.TakeLimited();
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

        // the campaign or the narrator ending the fight as a flight - "you run, and they let you"
        public void Flee()
        {
            Outcome = Outcome.Fled;
            Observer.Ended(Outcome);
        }

        // FLEEING, THE WAY A TABLE EXPECTS IT: get to an open edge of the map and step off it
        // (decisions_checklist.md section 3, "whatever is more expected and is easier"). there is
        // no roll and no special action - the cost is the run to the edge, which walks out of
        // everybody's reach and hands each of them an opportunity attack on the way, unless the
        // hero spent an action on Disengage first. only the hero flees; a monster that runs is
        // the campaign's business, not the rules'.
        public bool CanFlee(Turn turn) =>
            turn != null && !turn.Ended && !Over &&
            turn.Actor.Side == Allegiance.Hero && !turn.Actor.IsDown &&
            Field.Where(turn.Actor) is Cell here && Field.IsExit(here);

        public bool Flee(Turn turn)
        {
            if (!CanFlee(turn)) return false;

            turn.End();
            Flee();
            return true;
        }


        // --- who won ----------------------------------------------------------------------------

        public Outcome Judge()
        {
            if (Over) return Outcome;

            // a hero on the floor has not lost yet - the death save is still to come, and judging
            // the fight before that die lands would skip the whole mechanic
            bool heroesStanding = _order.Any(r => r.Actor.Side == Allegiance.Hero && !r.Actor.IsDead &&
                                                  !(r.Actor.IsDown && r.Actor.Stable));
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
