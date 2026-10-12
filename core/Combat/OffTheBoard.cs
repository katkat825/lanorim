using System.Collections.Generic;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // WHO IS OFF THE BOARD: sent away for a while (SRD 5.2.1 Banishment, Maze: the square it left and the spell that
    // sent it), and gone for good (Banishment held for its full minute, a hero who fled, an enemy that ran off the
    // edge). Was Encounter's private state (cc_task_j-split-classes-dev-hooks.md Part 1); taking a creature off the
    // board and putting it back are still the fight's (Encounter.OffBoard.cs)
    internal sealed class OffTheBoard
    {
        readonly Dictionary<Actor, (Cell From, string Source)> _away = new();
        readonly HashSet<Actor> _gone = new();

        public bool IsAway(Actor actor) => actor != null && _away.ContainsKey(actor);

        public bool IsGone(Actor actor) => actor != null && _gone.Contains(actor);

        public IEnumerable<Actor> Away => _away.Keys;

        public void SendAway(Actor actor, Cell from, string source) => _away[actor] = (from, source ?? "");

        public bool TryAway(Actor actor, out (Cell From, string Source) was) => _away.TryGetValue(actor, out was);

        // back, or never coming back: either way no longer away
        public void Back(Actor actor) => _away.Remove(actor);

        public void Gone(Actor actor)
        {
            _away.Remove(actor);
            _gone.Add(actor);
        }
    }
}
