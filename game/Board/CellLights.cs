using System.Collections.Generic;
using Godot;

namespace Game.Board
{
    // THE SQUARES YOU MAY WALK TO, AND THE ONES A BURST WOULD CATCH.
    //
    // Different from CellFlash, which is one square pulsing once because something happened on it.
    // This is a set of squares HELD UP while the player decides, and it goes away when they do.
    //
    // A thin quad a whisker above the mat and above the grid drawn on it, unshaded so it reads the
    // same in any light - and so a mini standing on a lit square still occludes it.
    public partial class CellLights : Node3D
    {
        // the quads are reused rather than freed: lighting a reach up and down every turn would
        // otherwise churn a hundred nodes a fight
        readonly List<MeshInstance3D> _quads = new();

        QuadMesh _square;

        float _lift;
        StandardMaterial3D _ink;

        // ABOVE THE GRID, NOT JUST ABOVE THE MAT, and a share of a square rather than a fixed
        // number of millimetres. It used to be 1.4 mm flat, which was above the mat on any map
        // and below the grid lines on a fine one - so the lit squares and the lines fought over
        // the same pixels. Board.GridTop is what this has to clear, and both are shares of a
        // cell so they keep their order whatever the map is drawn at.
        public const float Lift = 0.02f;

        // how much of a square it covers, so the lit squares read as separate rather than as one
        // shape with a grid drawn on it
        public const float Inset = 0.9f;

        public void Show(IReadOnlyList<Vector3> at, float cellSize, Color colour)
        {
            _lift = cellSize * Lift;

            Build(cellSize, colour);

            for (int i = 0; i < at.Count; i++)
            {
                MeshInstance3D quad = Quad(i);

                quad.Position = at[i] + new Vector3(0f, _lift, 0f);
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

                // MIX, NOT ADD, AND THE MAT IS WHY. Additive was right when the map was a
                // near-black placeholder: adding light to almost nothing reads as a square
                // lighting up. The map is parchment now - already bright - and adding to a
                // bright surface only ever walks it toward white, so two thirds of the map
                // washed out and the only squares showing their real colour were the ones
                // nobody could walk to. A mix blend tints instead of adding, which is what
                // reads as ink on paper rather than as a light shining on it.
                BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
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
