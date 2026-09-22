using System.Collections.Generic;
using Godot;

namespace Game.Board
{
    // THE SQUARES YOU MAY WALK TO, AND THE ONES A BURST WOULD CATCH.
    //
    // Different from CellFlash, which is one square pulsing once because something happened on it.
    // This is a set of squares HELD UP while the player decides, and it goes away when they do.
    //
    // A thin quad a whisker above the mat, unshaded and additive, so it reads as light on the mat
    // rather than as paint on it - and so a mini standing on a lit square still occludes it.
    public partial class CellLights : Node3D
    {
        // the quads are reused rather than freed: lighting a reach up and down every turn would
        // otherwise churn a hundred nodes a fight
        readonly List<MeshInstance3D> _quads = new();

        QuadMesh _square;
        StandardMaterial3D _ink;

        // a whisker above the mat: on it, and the depth fight shows as a shimmer
        public const float Lift = 0.0014f;

        // how much of a square it covers, so the lit squares read as separate rather than as one
        // shape with a grid drawn on it
        public const float Inset = 0.9f;

        public void Show(IReadOnlyList<Vector3> at, float cellSize, Color colour)
        {
            Build(cellSize, colour);

            for (int i = 0; i < at.Count; i++)
            {
                MeshInstance3D quad = Quad(i);

                quad.Position = at[i] + new Vector3(0f, Lift, 0f);
                quad.Visible = true;
            }

            for (int i = at.Count; i < _quads.Count; i++) _quads[i].Visible = false;
        }

        public void Clear()
        {
            foreach (MeshInstance3D quad in _quads) quad.Visible = false;
        }

        void Build(float cellSize, Color colour)
        {
            float side = cellSize * Inset;

            _square ??= new QuadMesh();

            // lying flat: a QuadMesh stands up in XY, and a lit square lies in the mat
            _square.Size = new Vector2(side, side);
            _square.Orientation = PlaneMesh.OrientationEnum.Y;

            _ink ??= new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };

            _ink.AlbedoColor = colour;
        }

        MeshInstance3D Quad(int i)
        {
            while (_quads.Count <= i)
            {
                var quad = new MeshInstance3D
                {
                    Name = $"Lit{_quads.Count:00}",
                    Mesh = _square,
                    MaterialOverride = _ink,
                    Visible = false,
                };

                AddChild(quad);
                _quads.Add(quad);
            }

            return _quads[i];
        }

        public int Lit
        {
            get
            {
                int lit = 0;

                foreach (MeshInstance3D quad in _quads) if (quad.Visible) lit++;

                return lit;
            }
        }

        public override string ToString() => $"{Lit} squares lit of {_quads.Count} quads";
    }
}
