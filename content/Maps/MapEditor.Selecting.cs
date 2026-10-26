using System.Collections.Generic;
using System.Linq;
using Core.Space;

namespace Content.Maps
{
    // THE SELECT TOOL (cc_task_l Part 4.3-4.4): a rectangle dragged out, then copied (Ctrl+C), put down where the pointer
    // is (Ctrl+V), dragged somewhere else, or cleared (Delete), each one undo step; or a click on a placed prop, which
    // picks it up to turn (R), drag or take away (Delete). Today the eraser was the only way to touch a placed prop
    public sealed partial class MapEditor
    {
        // the rectangle chosen, or null
        public CellBox? Selection { get; private set; }

        // the prop picked up, or null
        public Prop Picked { get; private set; }

        // WHO THE PICKED FIGURE IS (cc_task_m3 Part 10): one of the campaign's characters by id, or "" for someone. false
        // when what's picked isn't a figure
        public bool NamePicked(string actor)
        {
            if (Picked == null || Draft.NameProp(Picked, actor ?? "") is not { } named) return false;

            Picked = named;
            return true;
        }

        // what to outline on the board: the selection, or the squares the picked prop covers
        public CellBox? Chosen
        {
            get
            {
                if (Selection != null) return Selection;
                if (Picked == null) return null;

                var covers = Draft.Covers(Picked);
                return CellBox.Between(covers[0], covers[^1]);
            }
        }

        // what Ctrl+C took, kept until the next copy
        public MapClip Clip { get; private set; }

        public void Deselect()
        {
            Selection = null;
            Picked = null;
        }

        // a click: the prop over this square, or nothing
        void Pick(Cell cell)
        {
            Selection = null;
            Picked = Draft.PropAt(cell);
        }

        // a drag: from inside the selection moves it; from the picked prop moves that; from anywhere else chooses the
        // rectangle. how many things moved (0 for a new selection, which changes nothing on the map)
        int Grab(Cell from, Cell to)
        {
            int dx = to.X - from.X, dy = to.Y - from.Y;

            if (Selection is CellBox box && box.Contains(from))
            {
                if (!Draft.Move(box, dx, dy)) return 0;

                Selection = box.Moved(dx, dy);
                return box.Width * box.Height;
            }

            if (Picked != null && Draft.Covers(Picked).Contains(from))
            {
                Prop moved = Draft.MoveProp(Picked, new Cell(Picked.Cell.X + dx, Picked.Cell.Y + dy), Picked.Turn);

                if (moved == null) return 0;

                Picked = moved;
                return 1;
            }

            Picked = null;
            Selection = CellBox.Between(from, to);
            return 0;
        }

        // Ctrl+C: true when there was something to copy
        public bool Copy()
        {
            if (Selection is not CellBox box) return false;

            Clip = Draft.Copy(box);
            return true;
        }

        // Ctrl+V: the copy put down with its top-left on this square, and chosen there
        public bool Paste(Cell at)
        {
            if (Clip == null || !Draft.Contains(at)) return false;

            Picked = null;
            Selection = CellBox.At(at, Clip.Width, Clip.Height);

            return Draft.Paste(Clip, at);
        }

        // Delete: the picked prop, or everything in the selection
        public bool Delete()
        {
            if (Picked != null)
            {
                bool gone = Draft.RemoveProp(Picked);
                Picked = null;
                return gone;
            }

            return Selection is CellBox box && Draft.Clear(box);
        }

        // what the Select tool would show for a drag from here to there: the selection or the prop where it would land,
        // or the rectangle being chosen
        IReadOnlyList<Cell> Grabbing(Cell from, Cell to)
        {
            int dx = to.X - from.X, dy = to.Y - from.Y;

            if (Selection is CellBox box && box.Contains(from)) return box.Moved(dx, dy).Cells.ToList();

            if (Picked != null && Draft.Covers(Picked).Contains(from))
                return Draft.Covers(new Prop(Picked.Id, new Cell(Picked.Cell.X + dx, Picked.Cell.Y + dy), Picked.Turn));

            if (from == to && Draft.PropAt(to) is Prop under) return Draft.Covers(under);

            return CellBox.Between(from, to).Cells.ToList();
        }
    }
}
