using System.Collections.Generic;
using Godot;

namespace Game.Board
{
    public static class PaintedModel
    {
        public static IEnumerable<MeshInstance3D> Meshes(Node node)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null) yield return mesh;

            foreach (Node child in node.GetChildren())
                foreach (MeshInstance3D deeper in Meshes(child))
                    yield return deeper;
        }

        // THE PAINT GOES ON PER SURFACE, NOT PER MESH, AND IT KEEPS THE PACK'S COLOURS.
        //
        // MaterialOverride replaces every surface of a mesh with ONE material, which is right for a
        // model whose colour lives in a texture - the shader samples the atlas and the packs'
        // colours survive. The Quaternius character models have no texture at all: a figure is one
        // mesh of four or five surfaces, and each surface's colour is a flat albedo on its own
        // StandardMaterial3D. Overriding the mesh threw all of them away and every figure came out
        // the shader's default white, which is what the first pass of this looked like.
        //
        // So each surface gets its own copy of the paint with `tint` carrying that surface's
        // colour. The shader still does all the relighting - bands, liner, primer, brush, varnish -
        // and a goblin still comes out olive-skinned in brown trousers.
        //
        // A surface with no StandardMaterial3D to read (a textured pack, or a generated mesh) gets
        // the paint untouched, which is the atlas path and the behaviour this always had.
        public static void Paint(Node node, Material paint)
        {
            if (node == null || paint == null) return;

            foreach (MeshInstance3D mesh in Meshes(node))
            {
                if (paint is not ShaderMaterial shader || mesh.Mesh == null)
                {
                    mesh.MaterialOverride = paint;
                    continue;
                }

                // an override would win over the per-surface materials set below
                mesh.MaterialOverride = null;

                for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                    mesh.SetSurfaceOverrideMaterial(surface,
                        Tinted(shader, mesh.Mesh.SurfaceGetMaterial(surface)));
            }
        }

        // one copy of the paint per surface, wearing that surface's albedo. Duplicate(false) is a
        // shallow copy: the shader and its textures are shared, only the parameter block is new
        static Material Tinted(ShaderMaterial paint, Material wearing)
        {
            if (wearing is not BaseMaterial3D standard) return paint;

            var copy = (ShaderMaterial)paint.Duplicate(false);

            copy.SetShaderParameter(TintParameter, standard.AlbedoColor);

            return copy;
        }

        // matches the uniform in painted_miniature.gdshader
        const string TintParameter = "tint";

        public static Aabb Bounds(Node3D node)
        {
            Aabb whole = default;
            bool any = false;

            foreach (MeshInstance3D mesh in Meshes(node))
            {
                // relative to the node, not the world, so bounds work before it is placed
                Aabb box = Relative(node, mesh) * mesh.GetAabb();

                whole = any ? whole.Merge(box) : box;
                any = true;
            }

            return whole;
        }

        static Transform3D Relative(Node3D root, Node3D node)
        {
            var transform = Transform3D.Identity;

            for (Node3D at = node; at != null && at != root; at = at.GetParent() as Node3D)
                transform = at.Transform * transform;

            return transform;
        }

        // HOW TALL A RIGGED FIGURE ACTUALLY IS, which is not what its AABB says.
        //
        // A skinned MeshInstance3D reports the BIND pose's bounds and keeps reporting them however
        // the skeleton is posed - Godot never recomputes it from the skin, and a probe of these
        // models confirmed the number is identical before and after posing. The Quaternius rig
        // binds with the arms out and up, so the box is 3.24 x 3.16 units around a figure that
        // stands about 2.12. Scaling to that box made every mini two thirds of the height it was
        // asked for, standing in the middle of a base built for the height it wasn't.
        //
        // The posed skeleton knows better, so it is asked instead. This measures to the TOP BONE,
        // not the top of the head, so a figure stands a few per cent taller than FigureHeight -
        // which is the right error to have, since a mini's stated height is its body and not its
        // hat. Returns false when there is no skeleton, and then the AABB was telling the truth
        // all along.
        public static bool PosedHeight(Node node, out float height, out float bottom)
        {
            height = 0f;
            bottom = 0f;

            Skeleton3D skeleton = FirstSkeleton(node);

            if (skeleton == null || skeleton.GetBoneCount() == 0) return false;

            float low = float.MaxValue;
            float high = float.MinValue;

            for (int bone = 0; bone < skeleton.GetBoneCount(); bone++)
            {
                float y = skeleton.GetBoneGlobalPose(bone).Origin.Y;

                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
            }

            height = high - low;
            bottom = low;

            return height > 0f;
        }

        static Skeleton3D FirstSkeleton(Node node)
        {
            if (node is Skeleton3D skeleton) return skeleton;

            foreach (Node child in node.GetChildren())
                if (FirstSkeleton(child) is Skeleton3D deeper) return deeper;

            return null;
        }

        // fit the widest horizontal size; a tall model may stay tall
        public static float ToFitWidth(Aabb bounds, float metres)
        {
            float widest = Mathf.Max(bounds.Size.X, bounds.Size.Z);

            return widest <= 0f ? 1f : metres / widest;
        }

        public static float ToFitHeight(Aabb bounds, float metres) =>
            bounds.Size.Y <= 0f ? 1f : metres / bounds.Size.Y;

        // deterministic per-square yaw: same on reload, different from neighbours
        public static float SettledAngle(int x, int y)
        {
            int hash = (x * 73856093) ^ (y * 19349663);

            return (hash & 0x3FF) / 1024f * Mathf.Tau;
        }
    }
}
