using System;
using System.Linq;
using Content.Workshop.Story;
using Godot;

namespace Game.Workshop
{
    // THE BOARD DRAWN (cc_task_o 1.3, 1.1): an arrow from each card to every card it can lead to, lettered for a note's
    // options, the one being dragged from a handle, and the overview of the whole board in the corner
    public partial class StoryBoard
    {
        public override void _Draw()
        {
            var ink = new Color(0.35f, 0.25f, 0.15f, 0.85f);

            foreach (CardDraft card in Story.Cards)
            {
                if (!_tiles.TryGetValue(card.Title, out Control from)) continue;

                foreach ((string to, string letter) in StoryDraft.Leads(card).Distinct())
                {
                    if (!_tiles.TryGetValue(to, out Control target)) continue;

                    Vector2 a = from.Position + new Vector2(Tile.X, Tile.Y / 2) * _zoom;
                    Vector2 b = target.Position + new Vector2(0, Tile.Y / 2) * _zoom;
                    Arrow(a, b, ink);

                    if (letter.Length > 0) DrawString(GetThemeDefaultFont(), (a + b) / 2 + new Vector2(0, -4), letter, HorizontalAlignment.Center, -1, 16, ink);
                }
            }

            if (_arrowFrom != null && _tiles.TryGetValue(_arrowFrom, out Control dragging))
                Arrow(dragging.Position + new Vector2(Tile.X, Tile.Y / 2) * _zoom, _arrowTo, new Color(0.7f, 0.3f, 0.1f));

            Overview();
        }

        void Arrow(Vector2 a, Vector2 b, Color colour)
        {
            Vector2 bend = new(Math.Max(40, Math.Abs(b.X - a.X) / 2), 0);
            var points = new Vector2[16];

            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1);
                Vector2 c1 = a + bend, c2 = b - bend;
                points[i] = a * (1 - t) * (1 - t) * (1 - t) + c1 * 3 * (1 - t) * (1 - t) * t + c2 * 3 * (1 - t) * t * t + b * t * t * t;
            }

            DrawPolyline(points, colour, 2, true);

            Vector2 back = (points[^2] - b).Normalized() * 10;
            DrawColoredPolygon(new[] { b, b + back.Rotated(0.45f), b + back.Rotated(-0.45f) }, colour);
        }

        // THE OVERVIEW: the whole board, small, in the corner, with where the view is
        void Overview()
        {
            if (_tiles.Count == 0 || Size.X < 200) return;

            Rect2 all = Bounds().Grow(40);
            var box = new Rect2(Size - new Vector2(170, 110), new Vector2(160, 100));
            float scale = Mathf.Min(box.Size.X / all.Size.X, box.Size.Y / all.Size.Y);

            DrawRect(box, new Color(0.15f, 0.12f, 0.1f, 0.6f));

            foreach (string title in _tiles.Keys)
                DrawRect(new Rect2(box.Position + (Where(title) - all.Position) * scale, Tile * scale), new Color(0.9f, 0.85f, 0.7f));

            var seen = new Rect2(box.Position + (ToBoard(Vector2.Zero) - all.Position) * scale, Size / _zoom * scale);
            DrawRect(seen.Intersection(box), new Color(1, 0.8f, 0.3f), false, 1.5f);
        }
    }
}
