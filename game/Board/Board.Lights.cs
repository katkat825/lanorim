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

            if (MiniUnder(from, along) is Cell standing) return standing;

            var mat = new Plane(GlobalTransform.Basis.Y.Normalized(), GlobalTransform.Origin);

            Vector3? hit = mat.IntersectsRay(from, along);

            if (!hit.HasValue) return null;

            Cell cell = Metrics.At(ToLocal(hit.Value));

            return Map != null && Map.Contains(cell) ? cell : null;
        }

        // A CLICK ON A STANDING MINI IS A CLICK ON ITS SQUARE. The ray meets the figure long before the
        // floor, and the floor behind a figure is the square behind it (off the map, for a hero in the
        // corner): a Mage Armor aimed by clicking your own mini never landed (cc_task_ui-issues-9-30.md
        // 1.2). Each mini is a box as wide as MiniHitWidth of a square and as tall as its figure; the
        // nearest one the ray passes through wins
        [Export] public float MiniHitWidth { get; set; } = 0.7f;

        Cell? MiniUnder(Vector3 from, Vector3 along)
        {
            if (Map == null) return null;

            Vector3 origin = ToLocal(from);
            Vector3 direction = (ToLocal(from + along) - origin).Normalized();
            float half = Metrics.CellSize * MiniHitWidth * 0.5f;

            Cell? nearest = null;
            float best = float.MaxValue;

            foreach (Mini mini in _minis.Values)
            {
                if (!mini.IsInsideTree() || !mini.Visible || mini.BeingCleared) continue;

                Vector3 foot = ToLocal(mini.GlobalPosition);
                float tall = (ToLocal(mini.GlobalPosition + mini.GlobalBasis.Y * mini.FigureHeight) - foot).Length();
                var box = new Aabb(foot - new Vector3(half, 0f, half), new Vector3(half * 2f, Mathf.Max(tall, half), half * 2f));

                if (!box.IntersectsSegment(origin, origin + direction * 1000f)) continue;

                float distance = origin.DistanceTo(box.GetCenter());
                Cell cell = Metrics.At(foot);

                if (distance < best && Map.Contains(cell))
                {
                    best = distance;
                    nearest = cell;
                }
            }

            return nearest;
        }

        public Vector3 Where(Cell cell) => Metrics.Centre(cell);
    }
}
