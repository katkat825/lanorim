using System;
using System.Collections.Generic;
using Core.Space;

namespace Content.Maps
{
    public sealed partial class MapDraft
    {
        // --- painting ----------------------------------------------------------------------------

        public bool Paint(Cell cell, Tile tile)
        {
            if (!Contains(cell)) return false;

            Tile was = At(cell);

            if (was == tile) return false;

            Do(() => Set(cell, tile), () => Set(cell, was));

            return true;
        }

        // the editor's drag: every square between two corners at once, one undo step for the lot
        public int Paint(Cell from, Cell to, Tile tile)
        {
            var changed = new List<(Cell Cell, Tile Was)>();

            for (int y = Math.Min(from.Y, to.Y); y <= Math.Max(from.Y, to.Y); y++)
                for (int x = Math.Min(from.X, to.X); x <= Math.Max(from.X, to.X); x++)
                {
                    var cell = new Cell(x, y);

                    if (!Contains(cell) || At(cell) == tile) continue;

                    changed.Add((cell, At(cell)));
                }

            if (changed.Count == 0) return 0;

            Do(() =>
               {
                   foreach ((Cell cell, Tile _) in changed) Set(cell, tile);
               },
               () =>
               {
                   foreach ((Cell cell, Tile was) in changed) Set(cell, was);
               });

            return changed.Count;
        }

        void Set(Cell cell, Tile tile) => _tiles[Extent.Index(cell)] = tile;

        public bool Wall(Border border, Edge edge)
        {
            if (!Extent.Contains(border)) return false;

            Edge was = At(border);

            if (was == edge) return false;

            Do(() => SetEdge(border, edge), () => SetEdge(border, was));

            return true;
        }

        // the editor's drag along a run of lines: every one at once, one undo step for the lot (cc_task_f Part 2)
        public int Wall(IEnumerable<Border> borders, Edge edge)
        {
            var changed = new List<(Border Line, Edge Was)>();

            foreach (Border border in borders)
                if (Extent.Contains(border) && At(border) != edge && !changed.Exists(c => c.Line == border))
                    changed.Add((border, At(border)));

            if (changed.Count == 0) return 0;

            Do(() =>
               {
                   foreach ((Border line, Edge _) in changed) SetEdge(line, edge);
               },
               () =>
               {
                   foreach ((Border line, Edge was) in changed) SetEdge(line, was);
               });

            return changed.Count;
        }

        // the editor clicks between two squares rather than naming a border
        public bool Wall(Cell a, Cell b, Edge edge) =>
            Border.Between(a, b, out Border border) && Wall(border, edge);

        void SetEdge(Border border, Edge edge)
        {
            if (edge == Edge.None) _edges.Remove(border);
            else _edges[border] = edge;
        }


        // the outside edge, in one stroke. the first thing anyone does to a new map
        public int Enclose()
        {
            int put = 0;

            for (int y = 0; y < Rows; y++)
            {
                if (Wall(new Border(new Cell(0, y), true), Edge.Wall)) put++;
                if (Wall(new Border(new Cell(Columns, y), true), Edge.Wall)) put++;
            }

            for (int x = 0; x < Columns; x++)
            {
                if (Wall(new Border(new Cell(x, 0), false), Edge.Wall)) put++;
                if (Wall(new Border(new Cell(x, Rows), false), Edge.Wall)) put++;
            }

            return put;
        }
    }
}
