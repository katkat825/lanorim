using System.Collections.Generic;
using System.Linq;
using Core.Space;

namespace Content.Maps
{
    public sealed partial class MapDraft
    {
        // --- a rectangle at a time (cc_task_l Part 4.3) ------------------------------------------------------------

        // the squares, lines, props and markers in the box, counted from its corner. reading only: nothing changes
        public MapClip Copy(CellBox box)
        {
            var clip = new MapClip(box.Width, box.Height);

            foreach (Cell cell in box.Cells.Where(Contains))
                clip.Squares[Local(cell, box)] = (At(cell), GroundAt(cell));

            foreach (Border line in box.Borders.Where(Extent.Contains))
                if (At(line) != Edge.None)
                    clip.Lines[new Border(Local(line.Cell, box), line.Vertical)] = (At(line), StyleAt(line));

            foreach (Prop prop in _props.Where(p => box.Contains(p.Cell)))
                clip.Props.Add(new Prop(prop.Id, Local(prop.Cell, box), prop.Turn));

            foreach (KeyValuePair<int, Cell> spawn in _spawns.Where(s => box.Contains(s.Value)))
                clip.Spawns[spawn.Key] = Local(spawn.Value, box);

            if (box.Contains(Start)) clip.Start = Local(Start, box);

            return clip;
        }

        static Cell Local(Cell cell, CellBox box) => new(cell.X - box.Min.X, cell.Y - box.Min.Y);

        // PUT A CLIP DOWN with its top-left on `at`, as one undo step: what was under it is replaced (its props and
        // spawns go, its lines become the clip's, though the clip's outline only adds walls, so a room pasted against
        // another doesn't take that one's wall away). a spawn or the start in the clip moves there; a prop that won't
        // fit, or anything off the map's edge, is left behind. true when anything changed
        public bool Paste(MapClip clip, Cell at)
        {
            if (clip == null) return false;

            CellBox box = CellBox.At(at, clip.Width, clip.Height);

            return Change(() =>
            {
                foreach (Prop prop in _props.Where(p => box.Contains(p.Cell)).ToList()) RemoveProp(prop);

                foreach (int slot in _spawns.Where(s => box.Contains(s.Value)).Select(s => s.Key).ToList()) ClearSpawn(slot);

                foreach ((Cell local, (Tile tile, Ground ground)) in clip.Squares)
                {
                    var cell = new Cell(at.X + local.X, at.Y + local.Y);

                    if (Contains(cell)) Set(cell, tile, ground);
                }

                foreach (Border line in box.Borders.Where(Extent.Contains))
                {
                    var local = new Border(new Cell(line.Cell.X - at.X, line.Cell.Y - at.Y), line.Vertical);
                    (Edge edge, WallStyle style) = clip.Lines.TryGetValue(local, out var put) ? put : (Edge.None, WallStyle.Stone);

                    if (edge == Edge.None && box.OnOutline(line)) continue;

                    SetEdge(line, edge, style);
                }

                foreach (Prop prop in clip.Props)
                    PlaceProp(prop.Id, new Cell(at.X + prop.Cell.X, at.Y + prop.Cell.Y), prop.Turn);

                foreach ((int slot, Cell local) in clip.Spawns)
                    PlaceSpawn(slot, new Cell(at.X + local.X, at.Y + local.Y));

                if (clip.Start is Cell start) PlaceStart(new Cell(at.X + start.X, at.Y + start.Y));
            });
        }

        // CLEAR A BOX, as one undo step: its props, spawns and lines go, and its squares become the ground around it
        // (Background), so a house lifted off the grass leaves grass. the start stays: a map always has one
        public bool Clear(CellBox box) => Change(() =>
        {
            foreach (Prop prop in _props.Where(p => box.Contains(p.Cell)).ToList()) RemoveProp(prop);

            foreach (int slot in _spawns.Where(s => box.Contains(s.Value)).Select(s => s.Key).ToList()) ClearSpawn(slot);

            foreach (Border line in box.Borders.Where(Extent.Contains)) SetEdge(line, Edge.None);

            (Tile tile, Ground ground) = Background(box);

            foreach (Cell cell in box.Cells.Where(Contains))
                if (cell != Start || tile.IsPassable()) Set(cell, tile, ground);
        });

        // MOVE A BOX by its whole contents, as one undo step: lifted, the place it was cleared, and put down `by` away
        public bool Move(CellBox box, int dx, int dy)
        {
            if (dx == 0 && dy == 0) return false;

            MapClip clip = Copy(box);

            return Change(() =>
            {
                Clear(box);
                Paste(clip, new Cell(box.Min.X + dx, box.Min.Y + dy));
            });
        }

        // what a cleared box becomes: the commonest square just outside it, or rock when there's nothing outside it
        (Tile Tile, Ground Ground) Background(CellBox box)
        {
            var around = box.Ring.Where(Contains).Select(c => (At(c), GroundAt(c))).ToList();

            return around.Count == 0
                ? (Tile.Void, Ground.Stone)
                : around.GroupBy(s => s).OrderByDescending(g => g.Count()).First().Key;
        }
    }
}
