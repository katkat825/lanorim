using Godot;

namespace Game.Builder
{
    // THE MAP BUILDER'S DIALS, in one resource Kathleen can open in the inspector: res://ui/builder_layout.tres, the
    // builder's HudLayout (cc_task_f Part 2). The screen is built in code (MapBuilderUi), so its numbers live here;
    // colours, fonts and paddings are the theme's (ui/lanorim_theme.tres). Every number is at the 1920x1080 base canvas.
    // Built plain on purpose: how it looks is hers (#5)
    [GlobalClass]
    public partial class BuilderLayout : Resource
    {
        public const string Path = "res://ui/builder_layout.tres";

        static BuilderLayout _current;

        // the one in res://ui, or the defaults below when it is missing
        public static BuilderLayout Current =>
            _current ??= (ResourceLoader.Exists(Path) ? GD.Load<BuilderLayout>(Path) : null) ?? new BuilderLayout();

        // the tools and what they lay, down the left
        [Export] public int ToolsWidth { get; set; } = 380;

        // the problems, down the right
        [Export] public int ProblemsWidth { get; set; } = 420;

        // the panels keep this far off the screen's edges
        [Export] public int EdgeMargin { get; set; } = 16;

        [Export] public int Gap { get; set; } = 8;

        // how tall the prop palette's list is before it scrolls
        [Export] public int PaletteHeight { get; set; } = 360;

        // how long "Saved." and the like stay up, in seconds
        [Export] public float NoticeSeconds { get; set; } = 4f;

        // the hover highlight, and the line one
        [Export] public Color HoverColour { get; set; } = new Color(0.95f, 0.85f, 0.4f, 0.6f);

        [Export] public Color LineColour { get; set; } = new Color(0.95f, 0.85f, 0.4f, 0.9f);

        // a line's highlight, as a share of a square: how thick and how tall
        [Export] public float LineThickness { get; set; } = 0.12f;

        [Export] public float LineHeight { get; set; } = 0.05f;

        // the spawn numbers and the start's word, standing over their squares: their size and how high they float
        [Export] public int MarkFontSize { get; set; } = 64;

        [Export] public float MarkPixel { get; set; } = 0.0012f;

        [Export] public float MarkLift { get; set; } = 0.03f;

        [Export] public Color SpawnColour { get; set; } = new Color(0.85f, 0.2f, 0.15f);

        [Export] public Color StartColour { get; set; } = new Color(0.2f, 0.55f, 0.9f);

        // the camera's pitch for "From above", in degrees off the felt (the table's own is TableCamera.Pitch)
        [Export(PropertyHint.Range, "45,89,1")] public float TopDownPitch { get; set; } = 89f;
    }
}
