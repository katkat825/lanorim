using System.Linq;
using Content.Screens;
using Game.Screens;
using Godot;

namespace Game.Play
{
    public partial class PlayDirector
    {
        // --- the arena's roster (cc_task_cg Part 6) -------------------------------------------------------------

        void OpenRoster()
        {
            // a run that plays itself picks the first challenger; one that only reads on (--autostory, for a
            // screenshot) leaves the roster up, as it leaves a fight alone
            if (_auto)
            {
                string first = Run.Arena.Roster.FirstOrDefault()?.Id;
                GD.Print($"play    roster of {Run.Arena.Roster.Count}, picked {first ?? "nobody"}");
                Do(() => { if (first != null) Run.Pick(first); else Run.LeaveRoster(); });
                return;
            }

            var view = new RosterView(Run.Arena, Run.Find);

            Open(new RosterScreen(view, id => Do(() => Run.Pick(id)), () => Do(() => Run.LeaveRoster())));
        }
    }
}
