using System.Collections.Generic;
using Godot;

namespace Game.Dev
{
    // WHAT A PROBE EXPECTED, AND WHAT IT FOUND: each expectation said on its own line ("mapprobe ok   ..." or
    // "mapprobe NOT  ..."), the ones that didn't hold kept for the verdict, which the check script greps for ("mapprobe
    // passed" / "mapprobe FAILED - ..."). The map builder's and the Workshop's probes each had this written out (cc_task_m)
    public sealed class ProbeLog
    {
        readonly string _name;
        readonly List<string> _failed = new();

        public ProbeLog(string name) => _name = name;

        public bool Passed => _failed.Count == 0;

        public void Expect(bool held, string what)
        {
            GD.Print($"{_name} {(held ? "ok  " : "NOT ")} {what}");
            if (!held) _failed.Add(what);
        }

        public void Verdict()
        {
            if (Passed) GD.Print($"{_name} passed");
            else GD.PrintErr($"{_name} FAILED - " + string.Join("; ", _failed));
        }
    }
}
