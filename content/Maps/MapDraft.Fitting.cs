using System;
using System.Collections.Generic;
using Core.Space;

namespace Content.Maps
{
    public sealed partial class MapDraft
    {
        // THIS MAP AT ANOTHER SIZE (cc_task_l Part 1.3: a starting template made at the size the author asked for). It
        // stays centred: a bigger map carries the edge squares on outward (the grass round a clearing goes on, a
        // room's floor grows and its walls move out with its edge), a smaller one is cut in from both sides and keeps
        // its outline. The start, the spawns and the props move with the middle; one that falls off is left out
        public MapDraft Fitted(int columns, int rows)
        {
            var fitted = new MapDraft(columns, rows) { Palette = _palette };

            if (fitted.Columns == Columns && fitted.Rows == Rows)
            {
                fitted.Restore(Save());
                fitted._undo.Clear();
                return fitted;
            }

            var across = new Axis(Columns, fitted.Columns);
            var down = new Axis(Rows, fitted.Rows);

            foreach (Cell cell in fitted.Extent.Cells)
            {
                var from = new Cell(across.Square(cell.X), down.Square(cell.Y));
                fitted.Set(cell, At(from), GroundAt(from));
            }

            for (int y = 0; y < fitted.Rows; y++)
                for (int x = 0; x <= fitted.Columns; x++)
                    if (across.Line(x) is int line)
                        Copy(new Border(new Cell(line, down.Square(y)), true), new Border(new Cell(x, y), true), fitted);

            for (int y = 0; y <= fitted.Rows; y++)
                for (int x = 0; x < fitted.Columns; x++)
                    if (down.Line(y) is int line)
                        Copy(new Border(new Cell(across.Square(x), line), false), new Border(new Cell(x, y), false), fitted);

            fitted.Start = new Cell(across.Moved(Start.X), down.Moved(Start.Y));

            foreach (KeyValuePair<int, Cell> spawn in _spawns)
                fitted.PlaceSpawn(spawn.Key, new Cell(across.Moved(spawn.Value.X), down.Moved(spawn.Value.Y)));

            foreach (Prop prop in _props)
                fitted.PlaceProp(prop.Id, new Cell(prop.Cell.X + across.Offset, prop.Cell.Y + down.Offset), prop.Turn);

            fitted._undo.Clear();
            return fitted;
        }

        void Copy(Border from, Border to, MapDraft into)
        {
            if (At(from) != Edge.None) into.SetEdge(to, At(from), StyleAt(from));
        }

        // one direction of a fitting: which old square a new one copies, and which old line a new line does
        readonly struct Axis
        {
            readonly int _was, _now;

            public Axis(int was, int now)
            {
                _was = was;
                _now = now;
                Offset = (now - was) / 2;
            }

            public int Offset { get; }

            public int Square(int i) => Math.Clamp(i - Offset, 0, _was - 1);

            public int Moved(int i) => Math.Clamp(i + Offset, 0, _now - 1);

            // the outline is the outline; between two new squares that copy the same old one there is no line
            public int? Line(int k)
            {
                if (k == 0) return 0;
                if (k == _now) return _was;

                int before = Square(k - 1), after = Square(k);

                return before == after ? null : after;
            }
        }
    }
}
