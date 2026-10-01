using System.Collections.Generic;
using System.Linq;
using Core.Space;
using Godot;

namespace Game.Board
{
    public sealed partial class BoardTiles
    {
        // A PILLAR WHERE WALLS TURN, END OR MEET (cc_task_working-notes-10-01.md 2.5: "a corner or pillar so wall
        // joins look right"). A modular wall is as long as a square and as thick as a slab, so two meeting at a
        // corner leave a notch, and one that stops leaves a cut end; the pack's own rooms stand a column on
        // every such place (Quaternius's Updated Modular Dungeon). A straight run needs none. Width is a share of
        // a square; the height is the walls' (Cutaway), so a pillar comes down with the walls it joins
        public const float PillarWidth = 0.42f;

        void Pillars(Node3D under, MapLayout map)
        {
            if (PillarModel == null) return;

            var corners = new Dictionary<Vector2I, List<Border>>();

            foreach (Border on in map.Borders.Where(b => map.At(b) is Edge.Wall or Edge.Door))
                foreach (Vector2I end in Ends(on))
                {
                    if (!corners.TryGetValue(end, out List<Border> lines)) corners[end] = lines = new List<Border>();
                    lines.Add(on);
                }

            foreach ((Vector2I corner, List<Border> lines) in corners)
            {
                // two in a line is a straight run of wall, which joins itself
                if (lines.Count == 2 && lines[0].Vertical == lines[1].Vertical) continue;

                Node3D post = Standing(PillarModel, NameFor(corner), Point(corner), _metrics.CellSize * PillarWidth,
                                       out Aabb bounds);
                Cutaway?.Corner(post, bounds, corner);
                under.AddChild(post);
            }
        }

        // a line up a column runs from its square's top-left corner down one; along a row, across one
        static IEnumerable<Vector2I> Ends(Border on)
        {
            yield return new Vector2I(on.Cell.X, on.Cell.Y);
            yield return on.Vertical ? new Vector2I(on.Cell.X, on.Cell.Y + 1) : new Vector2I(on.Cell.X + 1, on.Cell.Y);
        }

        // the grid corner at the top-left of square (x, y), on the board
        Vector3 Point(Vector2I corner) =>
            _metrics.Centre(new Cell(corner.X, corner.Y)) - new Vector3(_metrics.CellSize * 0.5f, 0f, _metrics.CellSize * 0.5f);

        public static string NameFor(Vector2I corner) => $"Post{corner.X:00}x{corner.Y:00}";
    }
}
