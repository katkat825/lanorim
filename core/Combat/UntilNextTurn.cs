using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Core.Combat
{
    // A STATBLOCK'S CONDITION THAT LASTS "UNTIL THE END OF ITS NEXT TURN": the Ghoul's Paralyzed (SRD 5.2.1 p.288). One
    // put on during the creature's own turn waits a turn more. Was Encounter's private list (cc_task_j Part 1)
    internal sealed class UntilNextTurn
    {
        sealed class Lapse
        {
            public Lapse(Actor target, Condition condition, bool waiting)
            {
                Target = target;
                Condition = condition;
                Waiting = waiting;
            }

            public Actor Target { get; }

            public Condition Condition { get; }

            public bool Waiting { get; set; }
        }

        readonly List<Lapse> _lapses = new List<Lapse>();

        public void Add(Actor target, Condition condition, bool waiting) =>
            _lapses.Add(new Lapse(target, condition, waiting));

        // a turn of this creature's is over: the conditions that end now (taken off the books); one still waiting
        // waits no longer
        public List<Condition> Ending(Actor actor)
        {
            var ending = new List<Condition>();

            foreach (Lapse lapse in _lapses.Where(l => ReferenceEquals(l.Target, actor)).ToList())
            {
                if (lapse.Waiting)
                {
                    lapse.Waiting = false;
                    continue;
                }

                _lapses.Remove(lapse);
                ending.Add(lapse.Condition);
            }

            return ending;
        }
    }
}
