using Godot;

namespace Game.Board
{
    public partial class Board
    {
        // --- the near walls come down (Cutaway.cs) -------------------------------------------------------

        Cutaway _cutaway;

        public Cutaway Cutaway => _cutaway;

        // which side of the board the camera is on, as a step on the grid: the board's own axes, so it is right
        // whichever way the camera has been turned round it
        Vector2I TowardCamera()
        {
            Camera3D camera = IsInsideTree() ? GetViewport()?.GetCamera3D() : null;
            if (camera == null) return new Vector2I(0, 1);

            Vector3 to = ToLocal(camera.GlobalPosition);

            return Mathf.Abs(to.X) > Mathf.Abs(to.Z)
                ? new Vector2I(to.X > 0f ? 1 : -1, 0)
                : new Vector2I(0, to.Z >= 0f ? 1 : -1);
        }

        public override void _Process(double delta)
        {
            if (_cutaway == null) return;

            Vector2I toward = TowardCamera();
            if (toward != _cutaway.Facing) _cutaway.Cut(toward);
        }
    }
}
