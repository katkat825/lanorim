using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Core.Space;
using Godot;

namespace Game.Board
{
    public sealed partial class BoardTiles
    {
        // HOW EACH SQUARE LOOKS (cc_task_l Part 2.3): the map's ground drawn as one mesh per kind of ground, a thin skin
        // just above the mat and under the grid's lines, so the squares still read. Stone is the bare mat on an all-stone
        // map, so a dungeon looks as it always did; among other grounds it draws its flags (GroundTextures.Draws). 64 x 64 squares is at most seven meshes, not four thousand nodes.
        //
        // AT THE MAP'S EDGE the ground turns down onto the mat in a short darker lip (EdgeLip), so a field with no wall
        // round it ends like a printed tile laid on the table rather than stopping dead (Part 2.4, a first pass)
        public MapLook Look { get; set; }

        // how high the ground stands, as a share of a square: under the grid's lines (Board.LineClearance, 0.006)
        public const float GroundTop = 0.004f;

        // how far past the map's edge the lip reaches, as a share of a square, and how dark it is
        public const float EdgeLip = 0.14f;

        const float LipShade = 0.55f;

        public const string GroundName = "Ground";

        void Grounds(Node3D under, MapLayout map)
        {
            if (Look == null) return;

            foreach (Ground ground in Look.Grounds.Where(g => GroundTextures.Draws(g, Look)))
            {
                List<Cell> cells = map.Cells.Where(c => Look.At(c) == ground && map.At(c) is Tile.Floor or Tile.Rough).ToList();

                if (cells.Count > 0) under.AddChild(GroundMesh(ground, cells, map));
            }
        }

        MeshInstance3D GroundMesh(Ground ground, List<Cell> cells, MapLayout map)
        {
            float size = _metrics.CellSize;
            float top = size * GroundTop;
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);

            foreach (Cell cell in cells)
            {
                Vector3 c = _metrics.Centre(cell);
                float h = size * 0.5f;

                // the picture repeats every few squares, each square its own part of it, so no repeat is needed
                float n = GroundTextures.SquaresPerPicture;
                var uv = new Vector2(Mod(cell.X, (int)n) / n, Mod(cell.Y, (int)n) / n);
                float step = 1f / n;

                Quad(surface, Colors.White,
                     new Vector3(c.X - h, top, c.Z - h), uv,
                     new Vector3(c.X + h, top, c.Z - h), uv + new Vector2(step, 0),
                     new Vector3(c.X + h, top, c.Z + h), uv + new Vector2(step, step),
                     new Vector3(c.X - h, top, c.Z + h), uv + new Vector2(0, step));

                Lips(surface, cell, c, h, top, uv, step, map);
            }

            surface.GenerateNormals();

            return new MeshInstance3D
            {
                Name = $"{GroundName}_{Core.Words.EnumWords.Name(ground)}",
                Mesh = surface.Commit(),
                MaterialOverride = GroundMaterial(ground),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
        }

        // a sloped strip off each side of the square that is the map's edge
        void Lips(SurfaceTool surface, Cell cell, Vector3 c, float h, float top, Vector2 uv, float step, MapLayout map)
        {
            float out_ = _metrics.CellSize * EdgeLip;
            float low = -Board.MatDrop * 0.5f;
            var shade = new Color(LipShade, LipShade, LipShade);

            if (cell.Y == 0)
                Quad(surface, shade,
                     new Vector3(c.X - h, low, c.Z - h - out_), uv, new Vector3(c.X + h, low, c.Z - h - out_), uv + new Vector2(step, 0),
                     new Vector3(c.X + h, top, c.Z - h), uv + new Vector2(step, 0), new Vector3(c.X - h, top, c.Z - h), uv);

            if (cell.Y == map.Rows - 1)
                Quad(surface, shade,
                     new Vector3(c.X - h, top, c.Z + h), uv + new Vector2(0, step), new Vector3(c.X + h, top, c.Z + h), uv + new Vector2(step, step),
                     new Vector3(c.X + h, low, c.Z + h + out_), uv + new Vector2(step, step), new Vector3(c.X - h, low, c.Z + h + out_), uv + new Vector2(0, step));

            if (cell.X == 0)
                Quad(surface, shade,
                     new Vector3(c.X - h - out_, low, c.Z + h), uv + new Vector2(0, step), new Vector3(c.X - h - out_, low, c.Z - h), uv,
                     new Vector3(c.X - h, top, c.Z - h), uv, new Vector3(c.X - h, top, c.Z + h), uv + new Vector2(0, step));

            if (cell.X == map.Columns - 1)
                Quad(surface, shade,
                     new Vector3(c.X + h, top, c.Z + h), uv + new Vector2(step, step), new Vector3(c.X + h, top, c.Z - h), uv + new Vector2(step, 0),
                     new Vector3(c.X + h + out_, low, c.Z - h), uv + new Vector2(step, 0), new Vector3(c.X + h + out_, low, c.Z + h), uv + new Vector2(step, step));
        }

        // four corners, clockwise seen from above, as two triangles facing up
        static void Quad(SurfaceTool surface, Color colour, Vector3 a, Vector2 ua, Vector3 b, Vector2 ub, Vector3 c, Vector2 uc,
                         Vector3 d, Vector2 ud)
        {
            foreach ((Vector3 at, Vector2 uv) in new[] { (a, ua), (b, ub), (c, uc), (a, ua), (c, uc), (d, ud) })
            {
                surface.SetColor(colour);
                surface.SetUV(uv);
                surface.AddVertex(at);
            }
        }

        static int Mod(int a, int n) => (a % n + n) % n;

        readonly Dictionary<Ground, Material> _groundMaterials = new();

        // the painted shader wearing the ground's picture, like every other surface on the board
        Material GroundMaterial(Ground ground)
        {
            if (_groundMaterials.TryGetValue(ground, out Material made)) return made;

            Texture2D picture = GroundTextures.For(ground);

            Material material;

            if (Paint is ShaderMaterial paint)
            {
                var copy = (ShaderMaterial)paint.Duplicate(false);
                copy.SetShaderParameter("atlas", picture);
                copy.SetShaderParameter("tint", Colors.White);
                material = copy;
            }
            else
            {
                material = new StandardMaterial3D { AlbedoTexture = picture, VertexColorUseAsAlbedo = true };
            }

            return _groundMaterials[ground] = material;
        }

        // A CHASM, or deep water: a dark square sunk to the mat, the colour of its ground's depth
        Node3D Pit(Cell at) => new MeshInstance3D
        {
            Name = NameFor(at),
            Mesh = new BoxMesh { Size = new Vector3(_metrics.CellSize, _metrics.CellSize * GroundTop, _metrics.CellSize) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = GroundTextures.Deep(Look?.At(at) ?? Ground.Stone),
                Roughness = 0.9f,
            },
            Position = _metrics.Centre(at) + new Vector3(0f, _metrics.CellSize * GroundTop * 0.5f, 0f),
        };
    }
}
