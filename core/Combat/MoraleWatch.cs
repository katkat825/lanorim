using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Core.Combat
{
    // WHETHER A FIGHT'S ENEMIES HAVE BROKEN (cc_task_h 3.4): the morale the campaign gave it (Morale.Leader, Morale.Half),
    // the leader whose fall breaks it, how many enemies it started with, and whether it has broken and with nowhere
    // to run. Per fight, with its own trigger. Was Encounter's private state (cc_task_j-split-classes-dev-hooks.md
    // Part 1); a broken enemy's turn is still the fight's (Encounter.Rout)
    internal sealed class MoraleWatch
    {
        int _enemies;

        public Morale Morale { get; private set; }

        public Actor Leader { get; private set; }

        public bool Broken { get; private set; }

        public bool Surrendered { get; private set; }

        public void Hold(Morale morale, Actor leader, int enemies)
        {
            Morale = morale;
            Leader = leader;
            _enemies = enemies;
        }

        // has it broken, just now? true once, the moment it does. `gone` is a creature off the board for good. the
        // enemies are counted, and `nowhereToRun` asked, only when there is a morale to break (Judge asks often)
        public bool Breaks(Func<IReadOnlyList<Actor>> enemiesNow, Func<Actor, bool> gone, Func<bool> nowhereToRun)
        {
            if (Broken || Morale == Morale.None || _enemies == 0) return false;

            IReadOnlyList<Actor> enemies = enemiesNow();

            // the last of them falling is a win like any other, not a rout
            if (enemies.All(a => a.IsDown || gone(a))) return false;

            int down = enemies.Count(a => a.IsDown);

            Broken = Morale switch
            {
                Morale.Leader => Leader != null && (Leader.IsDown || gone(Leader)),
                Morale.Half => down >= (_enemies + 1) / 2,
                _ => false,
            };

            if (!Broken) return false;

            Surrendered = nowhereToRun();

            return true;
        }
    }
}
