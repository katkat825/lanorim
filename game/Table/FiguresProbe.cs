using System;
using System.Collections.Generic;
using System.Linq;
using Game.Board;
using Godot;

namespace Game.Table
{
    // EVERY CLASS'S TWO FIGURES, BUILT AND POSED (cc_task_h Part 2, its test). `--figures-probe` on the table, headless:
    // each figure is stood up as a real Mini, and then asked - one head and no more, none of its body's headgear when
    // the head is borrowed, everything it should hold in its hands, and its class's pose the clip it was left on.
    // Prints "figures check passed" or "figures FAILED: ...", and quits. check-table runs it.
    public partial class FiguresProbe : Node
    {
        public const string Flag = "--figures-probe";

        public Game.Board.Board Board { get; set; }

        readonly List<(string Name, Mini Mini, MiniLook Look)> _made = new List<(string, Mini, MiniLook)>();

        int _frames;

        public static bool RequestedFrom(string[] args) => Array.IndexOf(args ?? Array.Empty<string>(), Flag) >= 0;

        public override void _Ready()
        {
            if (Board?.MiniScene == null)
            {
                Finish(new[] { "no board to stand the figures on" });
                return;
            }

            foreach (string cls in HeroMinis.Classes)
                for (int figure = 0; figure < HeroMinis.Figures; figure++)
                {
                    MiniLook look = HeroMinis.LookOf(cls, figure);
                    var model = MiniModels.Of(look);

                    var mini = Board.MiniScene.Instantiate<Mini>();
                    mini.Name = $"{cls}_{figure}";
                    mini.FigureModel = model?.Model;
                    mini.FigureHeight = look.Height;
                    mini.Look = look;
                    mini.Paint = Board.Paint;

                    AddChild(mini);
                    _made.Add((mini.Name, mini, look));
                }
        }

        public override void _Process(double delta)
        {
            if (++_frames < 3 || _made.Count == 0) return;

            var wrong = new List<string>();

            foreach ((string name, Mini mini, MiniLook look) in _made)
                wrong.AddRange(Asked(mini, look).Select(w => $"{name}: {w}"));

            GD.Print($"figures {_made.Count} figures of {HeroMinis.Classes.Count()} classes stood up");
            Finish(wrong);
        }

        static IEnumerable<string> Asked(Mini mini, MiniLook look)
        {
            List<MeshInstance3D> parts = Nodes.AndUnder<MeshInstance3D>(mini).ToList();

            int heads = parts.Count(p => p.Name.ToString().EndsWith("Head", StringComparison.OrdinalIgnoreCase));
            if (heads != 1) yield return $"{heads} heads";

            if (look.Head != null)
                foreach (string gear in look.Leave ?? Array.Empty<string>())
                    if (parts.Any(p => p.Name.ToString().EndsWith(gear, StringComparison.OrdinalIgnoreCase)))
                        yield return $"still wears its {gear}";

            int held = Nodes.AndUnder<BoneAttachment3D>(mini).Count(b => b.Name.ToString().Contains("_in_"));
            if (held != (look.Holds?.Count ?? 0)) yield return $"holds {held} things, not {look.Holds?.Count ?? 0}";

            AnimationPlayer player = Nodes.AndUnder<AnimationPlayer>(mini).FirstOrDefault();
            string clip = player?.AssignedAnimation.ToString() ?? "";

            if (look.Pose != null && ClipMatch.Unqualified(clip) != look.Pose)
                yield return $"posed in '{clip}', not '{look.Pose}'";
        }

        void Finish(IReadOnlyCollection<string> wrong)
        {
            foreach (string line in wrong) GD.Print($"figures   {line}");

            GD.Print(wrong.Count == 0 ? "figures check passed" : $"figures FAILED: {wrong.Count} problems");
            GetTree().Quit(wrong.Count == 0 ? 0 : 1);
        }
    }
}
