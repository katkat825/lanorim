using System;
using System.Linq;
using Game.Audio;
using Godot;

namespace Game.Table
{
    // THE GM SCREEN (Tier 3d; decisions_checklist.md "table layout", updated_decisions.md). It stands on
    // the far side of the table, beyond the board's far edge, and the GM's hidden rolls rattle behind it.
    //
    // WHERE, AND WHY THERE. The camera looks down at 60 degrees, so a thing of height h between it and
    // the board hides a strip of board about 0.58 h deep. At the default camera the far side is behind
    // the board and hides nothing; after a half turn it is the near side, so the screen stands back
    // from the edge by more than that strip (Gap) and never covers a square. It is placed after each
    // map is laid, because a map's depth decides where its far edge is.
    //
    // Which screen: blank by default; a campaign's manifest can name another (cave, dead-forest,
    // plains, snowy-mountains - game/models/gm_screen/).
    public partial class GmScreen : Node3D
    {
        public static System.Collections.Generic.IReadOnlyList<string> Skins => Content.Campaigns.Manifest.GmScreens;

        const string Folder = "res://models/gm_screen/gm_screen_";

        [Export] public string Skin { get; set; } = "blank";

        // metres from the board's far edge to the screen's face
        [Export] public float Gap { get; set; } = 0.14f;

        // how wide the screen stands, as a share of the board's width
        [Export] public float WidthShare { get; set; } = 0.7f;

        // the tallest it may stand, whatever the model's proportions
        [Export] public float MostHeight { get; set; } = 0.2f;

        Node3D _model;
        AudioStreamPlayer3D _rattle;

        public string Wearing { get; private set; } = "";

        public override void _Ready()
        {
            Wear(Skin);

            // the GM's dice, heard and not seen: the tray's own wood impacts, quieter and from here
            _rattle = new AudioStreamPlayer3D
            {
                Name = "Rattle",
                Stream = ImpactPool.For(ImpactPool.Default)?.Stream,
                VolumeDb = -9f,
                MaxPolyphony = 3,
                Position = new Vector3(0, 0.05f, -0.1f),
            };

            AddChild(_rattle);
        }

        public bool Wear(string skin)
        {
            if (string.IsNullOrWhiteSpace(skin) || !Skins.Contains(skin)) skin = "blank";

            var scene = GD.Load<PackedScene>($"{Folder}{skin}.glb");

            if (scene == null)
            {
                GD.PushError($"gm screen: no model for '{skin}'");
                return false;
            }

            _model?.QueueFree();

            _model = scene.Instantiate<Node3D>();
            _model.Name = "Model";
            AddChild(_model);

            Wearing = skin;
            return true;
        }

        // stand beyond the board's far edge, centred on it, sized to it
        public void StandBehind(Game.Board.Board board)
        {
            if (board?.Map == null || _model == null) return;

            float width = board.Metrics.Width * WidthShare;

            // measured at its own size, so laying a second map doesn't scale the scaled model
            _model.Scale = Vector3.One;
            Aabb box = Bounds(_model);

            if (box.Size.X > 0.0001f)
            {
                float scale = Mathf.Min(width / box.Size.X, MostHeight / Mathf.Max(0.0001f, box.Size.Y));
                _model.Scale = Vector3.One * scale;
                box = new Aabb(box.Position * scale, box.Size * scale);
            }

            // board-local far edge (-Z), then into the table's space; the model's own front face is
            // put on that line, its base on the mat
            Vector3 far = board.Position + new Vector3(0f, 0f, -board.Metrics.HalfDepth - Gap);

            Position = new Vector3(far.X - (box.Position.X + box.Size.X * 0.5f), far.Y - box.Position.Y,
                                   far.Z - (box.Position.Z + box.Size.Z));
        }

        // a hidden roll behind the screen
        public void Rattle()
        {
            if (_rattle?.Stream != null) _rattle.Play();
        }

        // the model's bounds in this node's space, from every mesh under it
        static Aabb Bounds(Node3D root)
        {
            Aabb? all = null;

            foreach (MeshInstance3D mesh in Meshes(root))
            {
                Transform3D toRoot = root.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
                Aabb local = toRoot * mesh.GetAabb();
                all = all.HasValue ? all.Value.Merge(local) : local;
            }

            return all ?? new Aabb(Vector3.Zero, Vector3.Zero);
        }

        static System.Collections.Generic.IEnumerable<MeshInstance3D> Meshes(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh) yield return mesh;

                foreach (MeshInstance3D deeper in Meshes(child)) yield return deeper;
            }
        }
    }
}
