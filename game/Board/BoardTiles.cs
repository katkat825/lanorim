using System.Linq;
using Godot;
using Core.Space;

namespace Game.Board
{
    public sealed class BoardTiles
    {

        // lower than a mini is tall, so a piece is never hidden behind a wall at the table angle
        public const float WallHeight = 0.048f;

        // fraction of a square a wall eats, keeping both sides usable
        public const float WallThickness = 0.2f;

        // same line as a wall but lower, told apart by silhouette
        public const float DoorHeight = 0.036f;

        public const float RoughHeight = 0.0035f;

        // stops short of the grid line, so the difficult square still reads as one square
        public const float RoughInset = 0.86f;

        // fills its square: a mass, the other kind of barrier
        public const float RockHeight = 0.055f;

        public const float RubbleSpread = 0.92f;

        readonly BoardMetrics _metrics;

        public BoardTiles(BoardMetrics metrics) => _metrics = metrics;

        public Material Wall { get; set; }

        public Material Door { get; set; }

        public Material Rough { get; set; }

        // painted models, or null to fall back to the boxes above
        public PackedScene WallModel { get; set; }

        public PackedScene DoorwayModel { get; set; }

        public PackedScene RubbleModel { get; set; }

        public Material Paint { get; set; }

        public void Build(Node3D under, MapLayout map)
        {
            if (under == null || map == null) return;

            if (Wall == null || Door == null || Rough == null)
                GD.PushError("board: a tile kind has no material - that terrain will be untextured");

            foreach (Cell at in map.Cells) Raise(under, Piece(map, at));

            foreach (Border on in map.Borders) Raise(under, Piece(map, on));
        }

        // redraw one line from the map - a door opened mid-fight; the caller must have changed the
        // map first. squares never change in a fight, so there is no square to redraw
        public void Update(Node3D under, MapLayout map, Border on)
        {
            if (under == null || map == null) return;

            Clear(under, NameFor(on));
            Raise(under, Piece(map, on));
        }

        static void Raise(Node3D under, Node3D piece)
        {
            if (piece != null) under.AddChild(piece);
        }

        public void Clear(Node3D under, string named)
        {
            Node standing = under?.GetNodeOrNull(named);

            if (standing == null) return;

            // detached before free, so a same-frame rebuild cannot find it
            under.RemoveChild(standing);
            standing.QueueFree();
        }

        // named only for its square or line, so a lookup needs no memory of what kind it was
        public static string NameFor(Cell at) => $"Tile{at.X:00}x{at.Y:00}";

        public static string NameFor(Border on) =>
            $"Line{on.Cell.X:00}x{on.Cell.Y:00}{(on.Vertical ? "V" : "H")}";

        // what stands on a square or a line, or null for bare floor
        Node3D Piece(MapLayout map, Cell at) => map.At(at) switch
        {
            Tile.Void => WallModel != null ? Rock(at) : Block(at, RockHeight, _metrics.CellSize, Wall),
            Tile.Rough => RubbleModel != null ? Rubble(at) : Block(at, RoughHeight,
                                                                  _metrics.CellSize * RoughInset, Rough),
            _ => null,
        };

        Node3D Piece(MapLayout map, Border on) => map.At(on) switch
        {
            Edge.Wall => WallModel != null ? Panel(on) : Slab(on, WallHeight, Wall),
            Edge.Door => DoorwayModel != null ? Doorway(on) : Hang(on),
            _ => null,
        };

        Vector3 Centre(Border on) => _metrics.Centre(on);

        static float Facing(Border on) => on.Vertical ? Mathf.Pi * 0.5f : 0f;

        // a painted model scaled evenly to fit `width` across, standing on the board at `centre`: a
        // wall panel, a rock, rubble (Panel, Rock and Rubble each did this)
        Node3D Standing(PackedScene model, string name, Vector3 centre, float width, out Aabb bounds)
        {
            Node3D piece = Painted(model, name);
            bounds = PaintedModel.Bounds(piece);

            float scale = PaintedModel.ToFitWidth(bounds, width);

            piece.Scale = Vector3.One * scale;
            piece.Position = centre - new Vector3(0f, bounds.Position.Y * scale, 0f);

            return piece;
        }

        // uniform scale: a modular kit's wall already fits a line, so nothing is stretched
        Node3D Panel(Border on)
        {
            Node3D panel = Standing(WallModel, NameFor(on), Centre(on), _metrics.CellSize, out _);

            panel.Rotation = new Vector3(0f, Facing(on), 0f);

            return panel;
        }

        Node3D Rock(Cell at)
        {
            Node3D rock = Standing(WallModel, NameFor(at), _metrics.Centre(at), _metrics.CellSize, out Aabb bounds);

            float across = rock.Scale.X;

            // the one place a wall model is stretched: rock fills the square, depth pulled to full width, no rotation so no shear
            float deep = bounds.Size.Z > 0f ? _metrics.CellSize / bounds.Size.Z : across;

            rock.Scale = new Vector3(across, across, deep);

            return rock;
        }

        Node3D Rubble(Cell at)
        {
            Node3D rubble = Standing(RubbleModel, NameFor(at), _metrics.Centre(at),
                                     _metrics.CellSize * RubbleSpread, out _);

            // deterministic yaw from the square's coordinates: same on reload, not wallpaper
            rubble.Rotation = new Vector3(0f, PaintedModel.SettledAngle(at.X, at.Y), 0f);

            return rubble;
        }

        // one model, wall-with-hole plus leaf; the leaf is lifted onto its own hinge to swing
        Node3D Doorway(Border on)
        {
            Node3D model = Painted(DoorwayModel, "Frame");
            Aabb bounds = PaintedModel.Bounds(model);

            float scale = PaintedModel.ToFitWidth(bounds, _metrics.CellSize);

            var piece = new DoorPiece
            {
                Name = NameFor(on),
                Position = Centre(on) - new Vector3(0f, bounds.Position.Y * scale, 0f),
                Scale = Vector3.One * scale,
                Rotation = new Vector3(0f, Facing(on), 0f),
            };

            piece.AddChild(model);

            Node3D leaf = FindLeaf(model);

            if (leaf == null)
            {
                GD.PushError($"board: {DoorwayModel.ResourcePath} has no door in it - the doorway will not open");
                return piece;
            }

            // hinge at the leaf's outer edge; the leaf shifts back the same amount so reparenting does not move it
            Aabb leafBounds = leaf.Transform * PaintedModel.Bounds(leaf);
            var hinge = new Node3D { Name = "Hinge", Position = new Vector3(leafBounds.Position.X, 0f, 0f) };

            Transform3D was = leaf.Transform;

            // clear Owner before reparenting, or Godot complains the scene cannot be saved
            leaf.Owner = null;
            leaf.GetParent().RemoveChild(leaf);
            hinge.AddChild(leaf);
            leaf.Transform = new Transform3D(was.Basis, was.Origin - hinge.Position);

            piece.AddChild(hinge);
            piece.Leaf = hinge;
            piece.Drop = -leafBounds.Size.Y * 0.1f;

            return piece;
        }

        Node3D Painted(PackedScene model, string name)
        {
            var instance = model.Instantiate<Node3D>();

            instance.Name = name;
            PaintedModel.Paint(instance, Paint);

            return instance;
        }

        // the leaf is the child whose name ends in _door (KayKit's convention)
        static Node3D FindLeaf(Node node) =>
            Nodes.Under<Node3D>(node).FirstOrDefault(n => n.Name.ToString().ToLower().EndsWith("_door"));

        // sits on the surface, so height is added as a half-offset, never subtracted
        MeshInstance3D Block(Cell at, float height, float across, Material material) => new MeshInstance3D
        {
            Name = NameFor(at),
            Mesh = new BoxMesh { Size = new Vector3(across, height, across) },
            MaterialOverride = material,
            Position = _metrics.Centre(at) + new Vector3(0f, height * 0.5f, 0f),
        };

        MeshInstance3D Slab(Border on, float height, Material material)
        {
            float cell = _metrics.CellSize;
            float thick = cell * WallThickness;

            return new MeshInstance3D
            {
                Name = NameFor(on),
                Mesh = new BoxMesh
                {
                    Size = on.Vertical
                        ? new Vector3(thick, height, cell)
                        : new Vector3(cell, height, thick),
                },
                MaterialOverride = material,
                Position = Centre(on) + new Vector3(0f, height * 0.5f, 0f),
            };
        }

        DoorPiece Hang(Border on)
        {
            float cell = _metrics.CellSize;
            float thick = cell * WallThickness;
            float half = cell * 0.47f;

            var door = new DoorPiece
            {
                Name = NameFor(on),
                Position = Centre(on),
                Drop = -DoorHeight * 0.15f,
            };

            Vector3 hinge = on.Vertical ? new Vector3(0f, 0f, -half) : new Vector3(-half, 0f, 0f);

            var leaf = new Node3D { Name = "Hinge", Position = hinge };

            leaf.AddChild(new MeshInstance3D
            {
                Name = "Panel",
                Mesh = new BoxMesh
                {
                    Size = on.Vertical
                        ? new Vector3(thick, DoorHeight, cell * 0.94f)
                        : new Vector3(cell * 0.94f, DoorHeight, thick),
                },
                MaterialOverride = Door,
                Position = new Vector3(-hinge.X, DoorHeight * 0.5f, -hinge.Z),
            });

            door.AddChild(leaf);
            door.Leaf = leaf;

            return door;
        }
    }
}
