using Core.Space;

namespace Content.Maps
{
    // WHAT ESCAPE DID (cc_task_m2 Part 8): one step at a time, in this order
    public enum MapEscape
    {
        // nothing was in hand, nothing open, nothing chosen
        Nothing,

        // the prop or building in hand was put down, and the tool is Select again
        Dropped,

        // a picker or pop-up is open: the screen closes it (only the screen knows it's there)
        Close,

        // what was chosen - a rectangle, or a prop picked up - is let go
        Cleared,
    }

    // THE PROP IN HAND (cc_task_m2 Part 8). A prop or building picked from a picker, then a square: ONE is placed and it is
    // selected (Copy, Paste, Delete and Turn are then the selection's), and the hand is empty. Ctrl held, the hand keeps
    // it and every click places another of the same. Before this a picked prop stayed in hand and every click placed
    // another, with no way to stop. Shift-drag is still the scatter (Scatter), so Shift never means "more of the same"
    public sealed partial class MapEditor
    {
        // DO IT, as a click or a drag does (Apply), and then: a prop just placed with nothing held (`more` false) is
        // selected and the hand let go
        public int Apply(MapTarget from, MapTarget to, bool more)
        {
            bool placing = Tool == MapTool.Prop && PropId != null;
            int changed = Apply(from, to);

            if (placing && changed > 0 && !more && to.Square is Cell placed && Draft.PropAt(placed) is Prop prop)
            {
                PropId = null;
                Tool = MapTool.Select;
                Selection = null;
                Picked = prop;
            }

            return changed;
        }

        // ESCAPE: whatever is in hand put down, else the open picker or pop-up closed (`open` says one is; the screen
        // closes it), else what's chosen let go. One step a press
        public MapEscape Escape(bool open)
        {
            if (Tool == MapTool.Prop && PropId != null)
            {
                PropId = null;
                Tool = MapTool.Select;
                return MapEscape.Dropped;
            }

            if (open) return MapEscape.Close;

            if (Chosen != null)
            {
                Deselect();
                return MapEscape.Cleared;
            }

            return MapEscape.Nothing;
        }

        // something is in hand to place
        public bool InHand => Tool == MapTool.Prop && PropId != null;
    }
}
