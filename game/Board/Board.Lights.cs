using System.Collections.Generic;
using System.Linq;
using Core.Space;
using Godot;

namespace Game.Board
{
    public partial class Board
    {
        // --- lighting squares up ----------------------------------------------------------------

        public void Flash(IEnumerable<Cell> cells, Color colour)
        {
            _lights?.Show(cells.Select(Metrics.Centre).ToList(), Metrics.CellSize, colour);
        }

        public void Unflash() => _lights?.Clear();

        // one square, pulsing once, because something happened on it
        public void Pulse(Cell at) => _flash?.Show(Metrics.Centre(at), Metrics.CellSize);


        // --- what the mouse is over ---------------------------------------------------------------

        // the square under a screen point, or null when the ray misses the mat entirely. a plane,
        // not a raycast against the tiles: the point is the SQUARE, and a square with nothing on it
        // still has to be clickable
        public Cell? CellUnder(Vector2 at)
        {
            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return null;

            Vector3 from = camera.ProjectRayOrigin(at);
            Vector3 along = camera.ProjectRayNormal(at);

            var mat = new Plane(GlobalTransform.Basis.Y.Normalized(), GlobalTransform.Origin);

            Vector3? hit = mat.IntersectsRay(from, along);

            if (!hit.HasValue) return null;

            Cell cell = Metrics.At(ToLocal(hit.Value));

            return Map != null && Map.Contains(cell) ? cell : null;
        }

        public Vector3 Where(Cell cell) => Metrics.Centre(cell);
    }
}
