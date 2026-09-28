using Game.Dice;
using Godot;

namespace Game.Tray
{
    public partial class DiceTray
    {
        // --- measuring the scene ------------------------------------------------------------

        // measured off the scene in this node's space, so the tray can be parented anywhere and
        // still know its own shape. walls contribute only their thickness - they lean outward, so
        // the usable felt is the floor they stand on, not the gap between them
        TrayBounds Measure()
        {
            _floorBody ??= GetNodeOrNull<StaticBody3D>(TrayFloorPath);
            _wallsBody ??= GetNodeOrNull<StaticBody3D>(TrayWallsPath);

            (CollisionShape3D shape, BoxShape3D box) = FirstBox(_floorBody);

            if (box == null)
            {
                GD.PushError($"dice tray: no box-shaped floor under '{TrayFloorPath}' to measure - " +
                             $"falling back to the authored tray, {TrayBounds.Shipped}");
                return TrayBounds.Shipped;
            }

            // the top of the floor box, not its centre: dice rest on the felt, not inside it
            float feltY = ToLocal(shape.GlobalPosition).Y + box.Size.Y * 0.5f;

            var measured = new TrayBounds(box.Size.X * 0.5f, box.Size.Z * 0.5f, feltY,
                                          WallThickness());

            if (measured.IsUsable) return measured;

            GD.PushError($"dice tray: measured a tray with no felt inside it ({measured}) - " +
                         $"falling back to the authored tray, {TrayBounds.Shipped}");

            return TrayBounds.Shipped;
        }

        // the thinnest horizontal wall dimension: the thin side is the one that eats into the felt
        float WallThickness()
        {
            float thinnest = float.MaxValue;

            foreach (CollisionShape3D shape in Nodes.Under<CollisionShape3D>(_wallsBody))
                if (shape.Shape is BoxShape3D box)
                    thinnest = Mathf.Min(thinnest, Mathf.Min(box.Size.X, box.Size.Z));

            if (thinnest < float.MaxValue) return thinnest;

            GD.PushError($"dice tray: no box-shaped walls under '{TrayWallsPath}' to measure - " +
                         "taking the authored thickness");

            return TrayBounds.Shipped.WallThickness;
        }

        static (CollisionShape3D Shape, BoxShape3D Box) FirstBox(Node body)
        {
            foreach (CollisionShape3D shape in Nodes.Under<CollisionShape3D>(body))
                if (shape.Shape is BoxShape3D box) return (shape, box);

            return (null, null);
        }

        void Bind(DieBody die)
        {
            die.TraySpace = this;
            die.LostBelowY = _bounds.LostBelowY;
            die.LostRadius = _bounds.LostRadius;
        }
    }
}
