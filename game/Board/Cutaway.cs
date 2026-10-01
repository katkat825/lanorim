using System.Collections.Generic;
using System.Linq;
using Core.Space;
using Godot;

namespace Game.Board
{
    // THE NEAR WALLS COME DOWN (cc_task_working-notes-10-01.md 2.5). Kathleen: "wire in the dungeon walls and
    // assets", and the task: walls about mini height, never blocking the camera's view of the board. A wall as
    // tall as a mini, between the camera and the room, hides a mini-height strip of floor behind it (the camera
    // looks down at 45 degrees, so a thing h tall hides about h of board), and that is more than a square. So a
    // modelled wall, doorway or block of rock stands Tall, unless there is floor in the squares it would hide
    // from where the camera is now; then it stands Low, a cut-away lip, the way a dollhouse is drawn. Walls
    // that run toward the camera hide nothing beside them and always stand Tall. Asked again whenever the
    // camera comes round to another side of the board (Board.Cut), so every quarter turn has its own near side.
    //
    // A pillar where walls meet comes down with them: Low if any wall it joins is Low. A DOORWAY NEVER COMES
    // DOWN: cut to a lip its arch and door are a sliver nobody could find or click (the 2026-10-03 shots), and a
    // door is the one thing on a wall line the player has to see.
    public sealed class Cutaway
    {
        readonly BoardMetrics _metrics;
        MapLayout _map;

        // in metres: a wall or a rock as it stands, and cut away
        public float Tall { get; set; } = 0.16f;

        public float Low { get; set; } = 0.03f;

        // what a piece is: a line, a square, or a pillar on a corner of the grid
        sealed class Piece
        {
            public Node3D Node;
            public float Bottom;      // its model's lowest point, in its own units
            public float ModelTall;   // its model's height, in its own units
            public float Over;        // where it stands on the board (y) before its bottom is lifted onto it
            public Border? Line;
            public Cell? Square;
            public Vector2I? Corner;
            public bool IsLow;
            public bool Stays;
        }

        readonly Dictionary<string, Piece> _pieces = new();

        public Cutaway(BoardMetrics metrics) => _metrics = metrics;

        public void Map(MapLayout map) => _map = map;

        public void Line(Node3D node, Aabb bounds, Border on) => Add(node, bounds, p => p.Line = on);

        // a doorway: on its line like a wall, and always Tall
        public void Doorway(Node3D node, Aabb bounds, Border on) => Add(node, bounds, p => { p.Line = on; p.Stays = true; });

        public void Square(Node3D node, Aabb bounds, Cell at) => Add(node, bounds, p => p.Square = at);

        public void Corner(Node3D node, Aabb bounds, Vector2I at) => Add(node, bounds, p => p.Corner = at);

        public void Forget(string name) => _pieces.Remove(name);

        public void Clear() => _pieces.Clear();

        void Add(Node3D node, Aabb bounds, System.Action<Piece> where)
        {
            var piece = new Piece
            {
                Node = node,
                Bottom = bounds.Position.Y,
                ModelTall = bounds.Size.Y,
                Over = node.Position.Y + bounds.Position.Y * node.Scale.Y,
            };

            where(piece);
            _pieces[node.Name] = piece;
        }

        // which way the camera is from the board, as a step on the grid: (0, 1) is toward higher rows
        public Vector2I Facing { get; private set; } = new Vector2I(0, 1);

        // stand every piece for a camera on this side of the board
        public void Cut(Vector2I toward)
        {
            Facing = toward;

            // rock first: a wall that is the face of a rock mass follows the rock
            foreach (Piece rock in _pieces.Values.Where(p => p.Square != null)) Stand(rock, Hides(rock, toward));

            foreach (Piece line in _pieces.Values.Where(p => p.Line != null)) Stand(line, Hides(line, toward) || FacesLowRock(line, toward));

            foreach (Piece pillar in _pieces.Values.Where(p => p.Corner != null)) Stand(pillar, Joins(pillar).Any(p => p.IsLow));
        }

        // how many squares a Tall piece hides most of: at 45 degrees a thing h tall hides h of board behind it, so a
        // 0.16 m wall hides all of the next 0.125 m square and a quarter of the one after, which is one square
        int Reach => Mathf.Max(1, Mathf.RoundToInt(Tall / Mathf.Max(0.001f, _metrics.CellSize)));

        // a wall running toward the camera with rock on one side is that rock's face, and stands as the rock does,
        // so a block of rock and the walls round it come down together
        bool FacesLowRock(Piece piece, Vector2I toward)
        {
            if (piece.Line is not Border line || piece.Stays) return false;

            Cell here = line.Cell;
            Cell other = line.Vertical ? new Cell(here.X - 1, here.Y) : new Cell(here.X, here.Y - 1);

            return new[] { here, other }.Any(c => _pieces.TryGetValue(BoardTiles.NameFor(c), out Piece rock) && rock.IsLow);
        }

        // the squares behind a piece, seen from `toward`, as far as it would hide them; null when it runs toward
        // the camera and hides nothing
        IEnumerable<Cell> Behind(Piece piece, Vector2I toward)
        {
            Cell? first = null;

            if (piece.Square is Cell at) first = Step(at, -toward);

            if (piece.Line is Border line)
            {
                // a line up a column (Vertical) is the west side of its square; along a row, the north side
                bool across = line.Vertical ? toward.X != 0 : toward.Y != 0;
                if (!across) yield break;

                Cell here = line.Cell;
                Cell other = line.Vertical ? new Cell(here.X - 1, here.Y) : new Cell(here.X, here.Y - 1);

                // the far one is the square on the side away from the camera
                bool hereIsFar = line.Vertical ? toward.X < 0 : toward.Y < 0;
                first = hereIsFar ? here : other;
            }

            if (first is not Cell start) yield break;

            for (int i = 0; i < Reach; i++) yield return Step(start, -toward * i);
        }

        bool Hides(Piece piece, Vector2I toward) =>
            !piece.Stays && _map != null && Behind(piece, toward).Any(c => _map.Contains(c) && _map.At(c).IsPassable());

        // the lines that meet at a grid corner (x, y is the corner at the top-left of square x, y)
        IEnumerable<Piece> Joins(Piece pillar)
        {
            Vector2I c = pillar.Corner.Value;

            foreach (Piece piece in _pieces.Values)
            {
                if (piece.Line is not Border line) continue;

                Cell s = line.Cell;
                bool touches = line.Vertical
                    ? s.X == c.X && (s.Y == c.Y || s.Y + 1 == c.Y)
                    : s.Y == c.Y && (s.X == c.X || s.X + 1 == c.X);

                if (touches) yield return piece;
            }
        }

        void Stand(Piece piece, bool low)
        {
            if (!GodotObject.IsInstanceValid(piece.Node) || piece.ModelTall <= 0f) return;

            piece.IsLow = low;

            float y = (low ? Low : Tall) / piece.ModelTall;
            Vector3 scale = piece.Node.Scale;

            piece.Node.Scale = new Vector3(scale.X, y, scale.Z);
            piece.Node.Position = new Vector3(piece.Node.Position.X, piece.Over - piece.Bottom * y, piece.Node.Position.Z);
        }

        static Cell Step(Cell from, Vector2I by) => new Cell(from.X + by.X, from.Y + by.Y);

        // developer diagnostic: how many stand low now
        public override string ToString() =>
            $"{_pieces.Count} walls, rock and pillars, {_pieces.Values.Count(p => p.IsLow)} cut away " +
            $"(camera toward {Facing})";
    }
}
