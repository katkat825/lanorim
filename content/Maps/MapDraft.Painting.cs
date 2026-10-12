using System;
using System.Collections.Generic;
using System.Linq;
using Core.Space;

namespace Content.Maps
{
    public sealed partial class MapDraft
    {
        // --- painting ----------------------------------------------------------------------------

        public bool Paint(Cell cell, Tile tile) => Paint(cell, cell, Brush.Of(tile)) > 0;

        public bool Paint(Cell cell, Ground ground) => Paint(cell, cell, Brush.Of(ground)) > 0;

        public int Paint(Cell from, Cell to, Tile tile) => Paint(from, to, Brush.Of(tile));

        // the editor's drag: every square between two corners at once, one undo step for the lot
        public int Paint(Cell from, Cell to, Brush brush) => Paint(CellBox.Between(from, to).Cells, brush);

        // these squares, under the brush, as one undo step; how many changed
        public int Paint(IEnumerable<Cell> cells, Brush brush)
        {
            var changed = new List<(Cell Cell, Tile Was, Ground Looked)>();

            foreach (Cell cell in cells.Where(Contains).Distinct())
            {
                (Tile tile, Ground ground) = brush.On(At(cell), GroundAt(cell));

                if (tile != At(cell) || ground != GroundAt(cell)) changed.Add((cell, At(cell), GroundAt(cell)));
            }

            if (changed.Count == 0) return 0;

            Do(() =>
               {
                   foreach ((Cell cell, Tile was, Ground looked) in changed)
                   {
                       (Tile tile, Ground ground) = brush.On(was, looked);
                       Set(cell, tile, ground);
                   }
               },
               () =>
               {
                   foreach ((Cell cell, Tile was, Ground looked) in changed) Set(cell, was, looked);
               });

            return changed.Count;
        }

        // FILL (cc_task_l Part 4.2): the brush over every square joined to this one that has its rule and its look, up
        // to the walls, doors and fences between squares and to any other ground. one undo step
        public int Fill(Cell cell, Brush brush) => Contains(cell) ? Paint(Region(cell), brush) : 0;

        // the squares a fill from here would take, for the hover too
        public IReadOnlyList<Cell> Region(Cell cell)
        {
            if (!Contains(cell)) return Array.Empty<Cell>();

            Tile tile = At(cell);
            Ground ground = GroundAt(cell);

            var seen = new HashSet<Cell> { cell };
            var region = new List<Cell>();
            var next = new Queue<Cell>();
            next.Enqueue(cell);

            while (next.Count > 0)
            {
                Cell here = next.Dequeue();
                region.Add(here);

                foreach (Cell there in new[]
                         {
                             new Cell(here.X + 1, here.Y), new Cell(here.X - 1, here.Y),
                             new Cell(here.X, here.Y + 1), new Cell(here.X, here.Y - 1),
                         })
                {
                    if (!Contains(there) || seen.Contains(there)) continue;
                    if (At(there) != tile || GroundAt(there) != ground) continue;
                    if (!Border.Between(here, there, out Border line) || At(line) != Edge.None) continue;

                    seen.Add(there);
                    next.Enqueue(there);
                }
            }

            return region;
        }

        void Set(Cell cell, Tile tile) => _tiles[Extent.Index(cell)] = tile;

        void Set(Cell cell, Tile tile, Ground ground)
        {
            _tiles[Extent.Index(cell)] = tile;
            _ground[Extent.Index(cell)] = ground;
        }

        // --- walls -------------------------------------------------------------------------------

        public bool Wall(Border border, Edge edge, WallStyle style = WallStyle.Stone) => Wall(new[] { border }, edge, style) > 0;

        // the editor's drag along a run of lines: every one at once, one undo step for the lot (cc_task_f Part 2). a
        // wall takes the style it's laid in; a door takes its wall's (DoorStyle), so a gate in a fence is a fence's
        public int Wall(IEnumerable<Border> borders, Edge edge, WallStyle style = WallStyle.Stone)
        {
            var changed = new List<(Border Line, Edge Was, WallStyle Styled, WallStyle Now)>();

            foreach (Border border in borders)
            {
                if (!Extent.Contains(border) || changed.Exists(c => c.Line == border)) continue;

                WallStyle now = edge == Edge.None ? WallStyle.Stone : edge == Edge.Door ? DoorStyle(border, style) : style;

                if (At(border) != edge || StyleAt(border) != now)
                    changed.Add((border, At(border), StyleAt(border), now));
            }

            if (changed.Count == 0) return 0;

            Do(() =>
               {
                   foreach ((Border line, Edge _, WallStyle _, WallStyle now) in changed) SetEdge(line, edge, now);
               },
               () =>
               {
                   foreach ((Border line, Edge was, WallStyle styled, WallStyle _) in changed) SetEdge(line, was, styled);
               });

            return changed.Count;
        }

        // the editor clicks between two squares rather than naming a border
        public bool Wall(Cell a, Cell b, Edge edge) =>
            Border.Between(a, b, out Border border) && Wall(border, edge);

        // DOORS FOLLOW THEIR WALL (cc_task_l Part 3.4): a door put where a wall was is that wall's kind; one put in a run
        // takes the kind of the wall either side of it along the line; one standing alone is the kind chosen
        WallStyle DoorStyle(Border border, WallStyle chosen)
        {
            if (At(border) == Edge.Wall) return StyleAt(border);

            Border[] beside = border.Vertical
                ? new[] { new Border(new Cell(border.Cell.X, border.Cell.Y - 1), true), new Border(new Cell(border.Cell.X, border.Cell.Y + 1), true) }
                : new[] { new Border(new Cell(border.Cell.X - 1, border.Cell.Y), false), new Border(new Cell(border.Cell.X + 1, border.Cell.Y), false) };

            foreach (Border wall in beside)
                if (At(wall) == Edge.Wall) return StyleAt(wall);

            return chosen;
        }

        void SetEdge(Border border, Edge edge, WallStyle style = WallStyle.Stone)
        {
            if (edge == Edge.None) _edges.Remove(border);
            else _edges[border] = edge;

            if (edge == Edge.None || style == WallStyle.Stone) _styles.Remove(border);
            else _styles[border] = style;
        }

        // the outside edge, in one stroke. the first thing anyone does to a new map
        public int Enclose(WallStyle style = WallStyle.Stone)
        {
            var outline = new List<Border>();

            for (int y = 0; y < Rows; y++)
            {
                outline.Add(new Border(new Cell(0, y), true));
                outline.Add(new Border(new Cell(Columns, y), true));
            }

            for (int x = 0; x < Columns; x++)
            {
                outline.Add(new Border(new Cell(x, 0), false));
                outline.Add(new Border(new Cell(x, Rows), false));
            }

            return Wall(outline, Edge.Wall, style);
        }
    }
}
