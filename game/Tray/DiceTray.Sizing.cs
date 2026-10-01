using System;
using System.Linq;
using Game.Dice;
using Godot;

namespace Game.Tray
{
    public partial class DiceTray
    {
        // --- a smaller tray round the same dice (cc_task_ui-issues-10-01.md 6) --------------------------
        //
        // Kathleen: "make the tray smaller so that when it's brought to you the dice are easier to see". The
        // lift fills ScreenShare of the screen with the tray (TrayLift), so a smaller tray round dice of the
        // same size brings the dice nearer the camera. NOT a scale on this node: scaling a rigid body's parent
        // scales the dice and their collision but not gravity (the warning in table.tscn). Instead the MODEL
        // and the COLLISION are resized here, before the tray measures itself, and the dice keep their size:
        //
        //   Footprint   how wide and deep the tray is, against the model as authored (1 = 0.494 x 0.617 felt)
        //   WallHeight  how tall its rim stands, against the model as authored (1 = 0.057 m above the felt)
        //
        // A uniform shrink is both at the same number; that lowers the walls with the footprint, which is
        // the thing to measure (escapes). The throw softens with the footprint (a hand over a small tray). `--tray-footprint 0.65 --tray-walls 1` on the command line tries a
        // size without touching the scene (check-dice, a screenshot).
        [Export(PropertyHint.Range, "0.4,1,0.01")] public float Footprint { get; set; } = 1f;

        [Export(PropertyHint.Range, "0.4,3,0.01")] public float WallHeight { get; set; } = 1f;

        // at most this many dice on the felt at once, the rest thrown as the next handful (0: every seat). The
        // small tray holds four without them landing on each other: eight d20s in it cocked on another die
        // five times as often as in the authored tray, four as seldom
        [Export(PropertyHint.Range, "0,8,1")] public int MostAtOnce { get; set; }

        [Export] public NodePath TrayModelPath { get; set; } = "TrayModel";

        void Resize()
        {
            string[] args = OS.GetCmdlineUserArgs();
            Footprint = Number(args, "--tray-footprint", Footprint);
            WallHeight = Number(args, "--tray-walls", WallHeight);

            if (Mathf.IsEqualApprox(Footprint, 1f) && Mathf.IsEqualApprox(WallHeight, 1f)) return;

            Vector3 by = new Vector3(Footprint, WallHeight, Footprint);

            // the model's underside stays on the table (its offset grows with its height), and the felt
            // rises with it: the floor box's top follows the liner
            if (GetNodeOrNull<Node3D>(TrayModelPath) is Node3D model)
            {
                model.Scale *= by;
                model.Position *= by;
            }

            Node floorBody = GetNodeOrNull(TrayFloorPath);
            Node wallsBody = GetNodeOrNull(TrayWallsPath);

            // the tray's own boxes only: a d6 is a box too
            foreach (CollisionShape3D shape in Nodes.Under<CollisionShape3D>(floorBody).Concat(Nodes.Under<CollisionShape3D>(wallsBody)).ToList())
            {
                if (shape.Shape is not BoxShape3D box) continue;

                bool floor = shape.GetParent() == floorBody;
                float top = shape.Position.Y + box.Size.Y * 0.5f;

                // the walls share their shapes in the scene; each gets its own before it changes
                var resized = (BoxShape3D)box.Duplicate();
                resized.Size = floor ? new Vector3(box.Size.X * Footprint, box.Size.Y, box.Size.Z * Footprint) : box.Size * by;
                shape.Shape = resized;

                shape.Position = floor
                    ? new Vector3(0f, top * WallHeight - box.Size.Y * 0.5f, 0f)
                    : shape.Position * by;
            }

            // the hand lets go over the smaller felt, as high as before, and throws softer: how far a die
            // carries goes with the square of its speed, so the speed goes with the root of the footprint
            float softer = Mathf.Sqrt(Footprint);

            foreach (Node3D at in _throwPoints.Concat(_dice.Cast<Node3D>()))
                at.Position = new Vector3(at.Position.X * Footprint, at.Position.Y + FeltRise(), at.Position.Z * Footprint);

            foreach (DieBody die in _dice)
            {
                die.ThrowSpeedMin *= softer;
                die.ThrowSpeedMax *= softer;
            }

            GD.Print($"tray    resized to {Footprint:0.00} of its footprint, walls {WallHeight:0.00} of their height");
        }

        // how far the felt rose (or fell) with the walls, so a release point keeps its height above it
        float FeltRise() => TrayBounds.Shipped.FeltY * (WallHeight - 1f);

        static float Number(string[] args, string flag, float fallback)
        {
            int at = Array.IndexOf(args ?? Array.Empty<string>(), flag);

            return at >= 0 && at + 1 < args.Length &&
                   float.TryParse(args[at + 1], System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out float value)
                ? Mathf.Clamp(value, 0.4f, 3f)
                : fallback;
        }
    }
}
