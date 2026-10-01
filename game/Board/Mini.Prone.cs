using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Board
{
    public partial class Mini
    {
        // --- Prone, and standing up (cc_task_working-notes-10-01.md 1.2) ---------------------------------------
        //
        // Kathleen: "More Actions -> Stand Up: all the mechanics are there, but my mini is still on its back, and
        // possibly through a wall." The board only knew one way down, the death topple, which lays the whole piece
        // over about its feet so the figure runs a square and a half out of its own square (into a wall) and
        // never gets up. Prone is a second state: the FIGURE (not the piece) lies on its back across its own
        // square's diagonal, centred, shrunk if it must be to stay inside it; standing up puts back exactly the
        // standing pose Stand measured. The piece itself never moves, so a walk, a lean or a save still finds it
        // on its square. Death stays Topple, which nothing stands back up.

        // how much of its square a lying figure may cover, edge to edge
        [Export(PropertyHint.Range, "0.5,1,0.01")] public float ProneFit { get; set; } = 0.92f;

        // lying across the diagonal fits a tall figure without shrinking it as much; 0 lies it along the square
        [Export] public float ProneYawDegrees { get; set; } = 45f;

        // one square edge to edge; the board says, at Make
        public float CellSize { get; set; } = 0.125f;

        public bool IsProne { get; private set; }

        Transform3D _standing;

        // the top of the base, where the figure node stood before Stand moved it to put the feet there
        float _onBase;

        Node3D Figure => GetNodeOrNull<Node3D>(FigurePath);

        public void LieDown()
        {
            if (_toppled || IsProne || Figure is not Node3D figure) return;

            _standing = figure.Transform;
            IsProne = true;
            figure.Transform = Lying(figure, _standing);
        }

        public void GetUp()
        {
            if (!IsProne) return;

            IsProne = false;

            // dead is dead: the topple put the standing pose back already and stays
            if (_toppled || Figure is not Node3D figure) return;

            figure.Transform = _standing;
        }

        // on its back (the face up, the head away from the camera's side), turned across the square, centred on
        // the piece, its lowest point on the base, and no wider than ProneFit of the square
        Transform3D Lying(Node3D figure, Transform3D standing)
        {
            var turn = new Basis(Vector3.Up, Mathf.DegToRad(ProneYawDegrees)) * new Basis(Vector3.Right, -Mathf.Pi * 0.5f);
            var laid = new Transform3D(turn, Vector3.Zero) * standing;

            Aabb box = Footprint(figure, laid);

            float widest = Mathf.Max(box.Size.X, box.Size.Z);
            float shrink = widest <= 0f ? 1f : Mathf.Min(1f, ProneFit * CellSize / widest);

            Vector3 centre = box.GetCenter() * shrink;
            float bottom = box.Position.Y * shrink;

            // the base's top, which is where Stand put the standing figure's feet
            float onBase = _onBase;

            var scaled = new Transform3D(Basis.Identity.Scaled(Vector3.One * shrink), Vector3.Zero) * laid;

            return new Transform3D(Basis.Identity, new Vector3(-centre.X, onBase - bottom, -centre.Z)) * scaled;
        }

        // the figure's extent under a pose, in the piece's space: a rigged figure by its posed bones (its mesh
        // box is stuck in the bind pose, PaintedModel.PosedHeight), padded for the flesh round them; anything
        // else by its mesh box's corners
        internal static Aabb Footprint(Node3D figure, Transform3D pose)
        {
            var points = new List<Vector3>();
            float pad = 0f;

            if (Nodes.AndUnder<Skeleton3D>(figure).FirstOrDefault() is Skeleton3D skeleton && skeleton.GetBoneCount() > 0)
            {
                Transform3D at = PaintedModel.Relative(figure, skeleton);

                for (int bone = 0; bone < skeleton.GetBoneCount(); bone++)
                    points.Add(pose * (at * skeleton.GetBoneGlobalPose(bone)).Origin);

                // a head's half-width either side of the bones, as a share of the figure's length
                pad = 0.09f;
            }
            else
            {
                Aabb mesh = PaintedModel.Bounds(figure);

                for (int corner = 0; corner < 8; corner++) points.Add(pose * mesh.GetEndpoint(corner));
            }

            if (points.Count == 0) return new Aabb(pose.Origin, Vector3.Zero);

            var box = new Aabb(points[0], Vector3.Zero);

            foreach (Vector3 point in points) box = box.Expand(point);

            float grow = Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z)) * pad;

            return box.Grow(grow);
        }
    }
}
