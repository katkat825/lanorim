using System.Linq;
using Godot;

namespace Game.Tray
{
    // THE RIM'S GRAIN RUNS ALONG THE RAIL (Kathleen, 2026-10-01: "rotate the wood texture on the dice tray rim so what is
    // currently vertical wood grain texture becomes horizontal"; cc_task_e-shop-species-and-ui-notes.md 2.3). The tray
    // model's own UVs run U up every wall, and the wood's grain runs along U, so it stood on end. Rather than edit the
    // model, its wood is given new UVs when the tray loads: on a wall, U runs along the wall and V up it; on top of a
    // rail, U runs along that rail. The texel size is the model's own, so the wood is no bigger or smaller, and the
    // felt (a surface that only faces up) is left alone. Render only: no collision shape is touched
    public static class TrayGrain
    {
        public static void Apply(Node model)
        {
            if (model == null) return;

            foreach (MeshInstance3D instance in Game.Nodes.Under<MeshInstance3D>(model))
                if (instance.Mesh is ArrayMesh mesh && Regrained(mesh) is ArrayMesh grained)
                    instance.Mesh = grained;
        }

        // a copy with the walled surfaces' UVs laid along their rails; null when nothing in it has a wall
        static ArrayMesh Regrained(ArrayMesh mesh)
        {
            var copy = new ArrayMesh();
            bool changed = false;
            Aabb box = mesh.GetAabb();

            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                Godot.Collections.Array arrays = mesh.SurfaceGetArrays(s);
                Material material = mesh.SurfaceGetMaterial(s);

                Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                Vector3[] normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
                Vector2[] uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();

                bool walled = normals.Length == vertices.Length && uvs.Length == vertices.Length &&
                              normals.Any(n => Mathf.Abs(n.Y) < 0.5f);

                if (walled)
                {
                    float perUnit = TexelsPerUnit(arrays, vertices, uvs);

                    for (int i = 0; i < vertices.Length; i++)
                        uvs[i] = AlongTheRail(vertices[i], normals[i], box) * perUnit;

                    arrays[(int)Mesh.ArrayType.TexUV] = uvs;

                    // the wood has a normal map and a height map, both read through the tangents the old UVs made
                    var st = new SurfaceTool();
                    st.CreateFromArrays(arrays);
                    st.GenerateTangents();
                    arrays = st.CommitToArrays();
                    changed = true;
                }

                copy.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                copy.SurfaceSetMaterial(copy.GetSurfaceCount() - 1, material);
            }

            return changed ? copy : null;
        }

        // a wall facing X runs along Z, one facing Z along X; the top of a rail runs along whichever way it is long,
        // which is the side of the tray it sits on
        static Vector2 AlongTheRail(Vector3 at, Vector3 normal, Aabb box)
        {
            Vector3 n = normal.Abs();

            if (n.X >= n.Y && n.X >= n.Z) return new Vector2(at.Z, -at.Y);
            if (n.Z >= n.Y) return new Vector2(at.X, -at.Y);

            Vector3 centre = box.GetCenter();
            float acrossX = Mathf.Abs(at.X - centre.X) / Mathf.Max(box.Size.X * 0.5f, 1e-6f);
            float acrossZ = Mathf.Abs(at.Z - centre.Z) / Mathf.Max(box.Size.Z * 0.5f, 1e-6f);

            // nearer an X edge: a rail along Z
            return acrossX > acrossZ ? new Vector2(at.Z, at.X) : new Vector2(at.X, at.Z);
        }

        // the model's own texel size: UV units per model unit, the mean over its triangles
        static float TexelsPerUnit(Godot.Collections.Array arrays, Vector3[] vertices, Vector2[] uvs)
        {
            int[] indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            if (indices.Length == 0) indices = Enumerable.Range(0, vertices.Length).ToArray();

            float uv = 0f, world = 0f;

            for (int t = 0; t + 2 < indices.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = indices[t + k], b = indices[t + (k + 1) % 3];

                    uv += (uvs[b] - uvs[a]).Length();
                    world += (vertices[b] - vertices[a]).Length();
                }

            return world > 0f ? uv / world : 1f;
        }
    }
}
