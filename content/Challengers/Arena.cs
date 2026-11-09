using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Challengers
{
    // THE ARENA, AS THIS RUN HAS IT (cc_task_cg Parts 2 and 3): the hero's reputation, the challengers on offer, the
    // one picked, the special fights already beaten, and where each list's shuffle has got to. Ids and numbers only:
    // the save keeps it whole (SaveGame.Arena), so a reload neither rerolls the roster nor deals a name again early
    public sealed class Arena
    {
        // never below 0 (ArenaRules.Moved)
        public int Reputation { get; set; }

        // the roster has been drawn at least once: an empty roster before that is one still to draw
        public bool Drawn { get; set; }

        public List<Challenger> Roster { get; } = new List<Challenger>();

        // who the story's <<challengers>> picked last, kept after the bout so the story can still name them; null
        // when the player walked away from the roster
        public Challenger Picked { get; set; }

        // special fights the hero has beaten: they don't come back
        public SortedSet<string> Beaten { get; } = new SortedSet<string>(StringComparer.Ordinal);

        // each list's shuffle (a type's names, its descriptions), by "<type>.names" and "<type>.descriptions"
        public SortedDictionary<string, Bag> Bags { get; } = new SortedDictionary<string, Bag>(StringComparer.Ordinal);

        public sealed class Bag
        {
            public int Last { get; set; }

            public List<int> Left { get; } = new List<int>();
        }

        public Challenger Find(string id) =>
            Roster.FirstOrDefault(c => c.Id == id) ?? (Picked?.Id == id ? Picked : null);

        // a copy, for a save: nothing the run changes afterwards reaches it
        public Arena Copy()
        {
            var copy = new Arena { Reputation = Reputation, Drawn = Drawn, Picked = Picked };

            copy.Roster.AddRange(Roster);
            copy.Beaten.UnionWith(Beaten);

            foreach (KeyValuePair<string, Bag> bag in Bags)
            {
                var kept = new Bag { Last = bag.Value.Last };
                kept.Left.AddRange(bag.Value.Left);
                copy.Bags[bag.Key] = kept;
            }

            return copy;
        }

        public override string ToString() =>
            $"reputation {Reputation}, {Roster.Count} on the roster" + (Picked != null ? $", picked {Picked.Id}" : "");
    }
}
