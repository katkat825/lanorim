using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): off the board
    public sealed partial class Encounter
    {
        // --- off the board: Banishment, Maze ---------------------------------------------------

        // SRD 5.2.1 Banishment and Maze send a creature to a demiplane. v1 takes it off the board
        // for the duration (the table stands its mini beside the map) and puts it back where it
        // left, or on the nearest free square, when the spell ends. who is where is OffTheBoard's (cc_task_j Part 1)
        public bool IsAway(Actor actor) => _off.IsAway(actor);

        // gone for good - Banishment on a creature of another plane, held for the full minute
        public bool IsGone(Actor actor) => _off.IsGone(actor);

        public IEnumerable<Actor> Away => _off.Away;

        public bool Banish(Actor actor, string source)
        {
            if (actor == null || IsAway(actor) || !(Field.Where(actor) is Cell from)) return false;

            Field.Remove(actor);
            _off.SendAway(actor, from, source);
            Observer.Away(actor, true);
            return true;
        }

        // back from wherever it was sent. a source narrows it to the spell that sent it
        public bool Recall(Actor actor, string source = null)
        {
            if (actor == null || !_off.TryAway(actor, out (Cell From, string Source) was)) return false;

            if (source != null && was.Source != source) return false;

            Cell? to = Free(was.From, actor);

            _off.Back(actor);

            if (!to.HasValue) return false;

            Field.Place(actor, to.Value);
            Observer.Away(actor, false);
            Observer.Moved(actor, new[] { to.Value });
            return true;
        }

        // it doesn't come back
        public void Dismiss(Actor actor)
        {
            if (actor == null) return;

            _off.Back(actor);
            Field.Remove(actor);
            _off.Gone(actor);
            Judge();
        }

        // the square it left, or the nearest one it can stand in
        Cell? Free(Cell from, Actor actor) =>
            Field.Map.Cells.Where(c => Field.Map.IsPassable(c) && !Field.Occupies(c, actor))
                 .OrderBy(c => Battlefield.Distance(from, c))
                 .ThenBy(c => c.Y).ThenBy(c => c.X)
                 .Select(c => (Cell?)c)
                 .FirstOrDefault();
    }
}
