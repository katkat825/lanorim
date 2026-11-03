namespace Core.Space
{
    // what stands on the line between two squares - a wall is a property of the edge, not a square
    public enum Edge
    {
        None = 0,

        Wall,

        // shut; an open door isn't a value here, it's Edge.None - a gap in the wall
        Door,

        // bars: nothing walks through, everything is seen through - Forcecage's cage. runtime
        // only (a spell raises them on the board); no map file writes one
        Bars,

        // AN ARCHWAY AND A WINDOW (cc_task_m4 Part 4): lines a map is drawn with, beside Wall and Door. To the rules an
        // archway is an open way that never shuts (None) and a window is bars (Bars), and the map builder hands the
        // rules those (WallStyles.Rule); the two values themselves are what a map file writes ('a' and 'w', MapReader)
        // and what the board draws. Should either reach the rules unmapped, they read the same way here
        Archway,

        Window,
    }

    public static class Edges
    {
        public static bool IsOpen(this Edge edge) => edge is Edge.None or Edge.Archway;

        // separate from IsOpen on purpose - a portcullis or low wall is one without the other
        public static bool IsTransparent(this Edge edge) => edge is Edge.None or Edge.Bars or Edge.Archway or Edge.Window;
    }
}
