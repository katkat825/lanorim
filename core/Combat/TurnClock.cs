using System.Collections.Generic;
using Core.Characters;

namespace Core.Combat
{
    // THE TURN ORDER AND THE ROUND: who goes when, whose turn is next, and which round it is. Was Encounter's private
    // state (cc_task_j-split-classes-dev-hooks.md Part 1); what a turn starting or ending does is still the fight's
    internal sealed class TurnClock
    {
        readonly List<InitiativeRoll> _order = new();

        // the place in the order of the next creature to go
        int _next;

        public int Round { get; private set; }

        public IReadOnlyList<InitiativeRoll> Order => _order;

        public bool Started => _order.Count > 0;

        // the whole order, rolled once, at round 0
        public void Start(IEnumerable<InitiativeRoll> rolls)
        {
            _order.AddRange(rolls);

            Round = 0;
            _next = 0;
        }

        // everyone has had this round's turn
        public bool RoundDone => _next >= _order.Count;

        public void NextRound()
        {
            Round++;
            _next = 0;
        }

        // whose turn it is now, and the order moves on past them
        public Actor Advance() => _order[_next++].Actor;

        // where a creature is in the order, the explicit last word wherever creatures tie; one not in it comes last
        public int Rank(Actor actor)
        {
            int at = _order.FindIndex(r => ReferenceEquals(r.Actor, actor));
            return at < 0 ? int.MaxValue : at;
        }

        // a creature joining under way: after the one it belongs to, or last. one put in before the next to go
        // pushes the next along, so nobody's turn comes twice or not at all
        public void Insert(InitiativeRoll roll, Actor after)
        {
            int at = after != null ? _order.FindIndex(r => ReferenceEquals(r.Actor, after)) + 1 : _order.Count;

            if (at <= 0) at = _order.Count;

            _order.Insert(at, roll);

            if (at < _next) _next++;
        }
    }
}
