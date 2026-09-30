using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Board
{
    public static class PaintedModel
    {
        public static IEnumerable<MeshInstance3D> Meshes(Node node) =>
            Nodes.AndUnder<MeshInstance3D>(node).Where(m => m.Mesh != null);

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
        // `what` names the thing in a warning ("mini", "prop", "tile")
        public static void Paint(Node node, Material paint, string what = "paint")
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
                        Tinted(shader, mesh.Mesh.SurfaceGetMaterial(surface), $"{what}: {ModelName(mesh)}"));
            }
        }

        // one copy of the paint per surface, wearing that surface's albedo. Duplicate(false) is a
        // shallow copy: the shader and its textures are shared, only the parameter block is new
        static Material Tinted(ShaderMaterial paint, Material wearing, string whose)
        {
            if (wearing is not BaseMaterial3D standard) return paint;

            var copy = (ShaderMaterial)paint.Duplicate(false);

            string surface = wearing.ResourceName ?? "";

            copy.SetShaderParameter(TintParameter, Colour(standard, surface, whose));

            // eyes and faces painted a millimetre off the head stand a little further off it, so they
            // don't come and go with the camera (the shader's lift_distance); iron is painted as iron
            if (Lifted(surface)) copy.SetShaderParameter(LiftedParameter, 1f);
            if (Metal(surface)) copy.SetShaderParameter(MetalParameter, 1f);

            // a textured surface (the Fantasy Props kit, a KayKit atlas) keeps its picture: the
            // shader's atlas is that surface's own albedo, relit the same way as everything else
            if (standard.AlbedoTexture != null)
                copy.SetShaderParameter(AtlasParameter, standard.AlbedoTexture);

            return copy;
        }

        // matches the uniform in painted_miniature.gdshader
        const string TintParameter = "tint";

        const string AtlasParameter = "atlas";

        const string LiftedParameter = "lifted";

        const string MetalParameter = "metal";

        // BY THE MATERIAL'S NAME, because that is all a pack gives: the Quaternius figures call their
        // painted-on eyes "Face" (goblin, zombie, pirate, ninja) or "Eyes" (the rogue, the wolf's
        // "Eyes_Black"); the Fantasy Props MegaKit calls its iron "MI_Trim_Metal". Its metallic flag can't
        // be used instead - the kit's glTFs leave metallicFactor out, so the wood imports as metal too
        public static bool Lifted(string surface) =>
            surface.Equals("Face", System.StringComparison.OrdinalIgnoreCase) ||
            surface.StartsWith("Eyes", System.StringComparison.OrdinalIgnoreCase);

        public static bool Metal(string surface) =>
            surface.Contains("metal", System.StringComparison.OrdinalIgnoreCase);

        // A SURFACE WITH NO COLOUR OF ITS OWN - glTF's default white, no texture, no vertex colour - is an
        // export that dropped its baseColorFactor (rogue_v1 came out a white blob that way). It gets primer
        // grey rather than white, and says so. EXCEPT EYES: white is what eyes usually are, and glTF can't tell
        // "white on purpose" from "no colour" (rogue_v3's are white on purpose, Kathleen 2026-09-30)

        // the primer grey the shader shows in a crevice: an unpainted surface, which is what it is
        static readonly Color Unpainted = new Color(0.42f, 0.41f, 0.40f);

        static Color Colour(BaseMaterial3D standard, string surface, string whose)
        {
            bool uncoloured = standard.AlbedoColor == Colors.White && standard.AlbedoTexture == null &&
                              !standard.VertexColorUseAsAlbedo;

            if (!uncoloured || surface.StartsWith("Eyes", System.StringComparison.OrdinalIgnoreCase))
                return standard.AlbedoColor;

            GD.PushWarning($"{whose} surface '{surface}' has no colour");

            return Unpainted;
        }

        // the model's own file, for a warning: rogue_v2, not "Body"
        static string ModelName(Node mesh)
        {
            for (Node at = mesh; at != null; at = at.GetParent())
                if (!string.IsNullOrEmpty(at.SceneFilePath)) return System.IO.Path.GetFileNameWithoutExtension(at.SceneFilePath);

            return mesh.Name;
        }

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

        public static Transform3D Relative(Node3D root, Node3D node)
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
        //
        // THE HOOD COUNTS (cc_task_table-ui-minis-zoom-damage.md 4). A rigid piece riding a bone
        // (RigidParts: rogue_v2's hood and cloak) that stands above the body's own top adds what it
        // stands above it by. The body's top is its top bone plus the head above that bone, read off
        // the bind pose - so a hood is measured against the head it sits on, and a mini with no hood
        // measures exactly as it always did.
        public static bool PosedHeight(Node node, out float height, out float bottom)
        {
            height = 0f;
            bottom = 0f;

            Skeleton3D skeleton = FirstSkeleton(node);

            if (skeleton == null || skeleton.GetBoneCount() == 0) return false;

            Transform3D at = node is Node3D root ? Relative(root, skeleton) : Transform3D.Identity;

            float low = float.MaxValue;
            float high = float.MinValue;
            int top = 0;

            for (int bone = 0; bone < skeleton.GetBoneCount(); bone++)
            {
                float y = (at * skeleton.GetBoneGlobalPose(bone)).Origin.Y;

                low = Mathf.Min(low, y);

                if (y > high) { high = y; top = bone; }
            }

            height = high - low;
            bottom = low;

            if (node is Node3D figure) height += Overhang(figure, skeleton, at, top, high);

            return height > 0f;
        }

        // how far a piece riding a bone stands above the posed body's own top, or 0
        static float Overhang(Node3D figure, Skeleton3D skeleton, Transform3D at, int top, float posedTop)
        {
            float pieces = float.MinValue;
            float bindTop = float.MinValue;

            foreach (MeshInstance3D mesh in Meshes(figure))
            {
                float y = (Relative(figure, mesh) * mesh.GetAabb()).End.Y;

                if (mesh.GetParent() is BoneAttachment3D) pieces = Mathf.Max(pieces, y);
                else if (mesh.Skin != null || skeleton.IsAncestorOf(mesh)) bindTop = Mathf.Max(bindTop, y);
            }

            if (pieces == float.MinValue || bindTop == float.MinValue) return 0f;

            // the head above its bone, in the bind pose, carried up to where the pose put that bone
            float body = posedTop + (bindTop - (at * skeleton.GetBoneGlobalRest(top)).Origin.Y);

            return Mathf.Max(0f, pieces - body);
        }

        static Skeleton3D FirstSkeleton(Node node) => Nodes.AndUnder<Skeleton3D>(node).FirstOrDefault();

        // fit the widest horizontal size; a tall model may stay tall
        public static float ToFitWidth(Aabb bounds, float metres)
        {
            float widest = Mathf.Max(bounds.Size.X, bounds.Size.Z);

            return widest <= 0f ? 1f : metres / widest;
        }

        // deterministic per-square yaw: same on reload, different from neighbours
        public static float SettledAngle(int x, int y)
        {
            int hash = (x * 73856093) ^ (y * 19349663);

            return (hash & 0x3FF) / 1024f * Mathf.Tau;
        }
    }
}
