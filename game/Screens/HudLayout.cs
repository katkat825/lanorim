using Godot;

namespace Game.Screens
{
    // THE TABLE HUD'S DIALS, in one resource Kathleen can open in the inspector: res://ui/hud_layout.tres.
    // The HUD is built in code (CombatHudUi, PlayDirector), so an [Export] on those nodes would never
    // reach the editor; these do. Colours, fonts and paddings are the theme's (ui/lanorim_theme.tres);
    // sizes, gaps and switches are here. Every number is at the 1920x1080 base canvas.
    [GlobalClass]
    public partial class HudLayout : Resource
    {
        public const string Path = "res://ui/hud_layout.tres";

        static HudLayout _current;

        // the one in res://ui, or the defaults below when it is missing
        public static HudLayout Current =>
            _current ??= (ResourceLoader.Exists(Path) ? GD.Load<HudLayout>(Path) : null) ?? new HudLayout();

        // --- the menus ---------------------------------------------------------------------------------

        // THE MENUS' SIZES WERE WRITTEN FOR THE OLD 1152x648 CANVAS (a 720-wide creation card, a 360-tall
        // list). The base is 1920x1080 now and the theme's text is 1.4 times the size it was, so every
        // one of those widths and heights is multiplied by this (Ui.Px), rather than each rewritten
        [Export(PropertyHint.Range, "0.8,2,0.05")] public float MenuScale { get; set; } = 1.4f;

        // the height kept for creation's description area under a list (what the selected class,
        // species or spell is), in the old canvas's units like the menus: the page doesn't jump as
        // descriptions of different lengths come and go
        [Export] public float CreationAboutHeight { get; set; } = 110f;

        // --- the action bar ---------------------------------------------------------------------------

        // an option that can't be taken right now (no action left, nobody in reach) is left off the
        // bar and the pips say why; off, it stays on the bar faded, flat and without its number.
        // A menu (More actions, Spells) always lists them greyed - a menu is where you look for what exists
        [Export] public bool HideUnavailable { get; set; } = true;

        // how faded an unavailable option is, when it is shown
        [Export(PropertyHint.Range, "0,1,0.05")] public float UnavailableAlpha { get; set; } = 0.45f;

        [Export] public int BarGap { get; set; } = 8;

        [Export] public int EndTurnMinWidth { get; set; } = 180;

        // --- the log ------------------------------------------------------------------------------------

        [Export] public int LogWidth { get; set; } = 460;

        // the top right corner kept for the turn hint, the tray's caption and the notices; the turn
        // strip lives between the log and this, so the three can never overlap (cc_task_ui-issues-9-30.md 2)
        [Export] public int HintWidth { get; set; } = 460;

        // words with no panel behind them (the top right corner) keep this far off the screen's edge
        [Export] public int TextInset { get; set; } = 20;

        [Export] public int LogLinesClosed { get; set; } = 3;

        [Export] public int LogLinesOpen { get; set; } = 14;

        // --- the edges ------------------------------------------------------------------------------------

        // the gap between a panel and the screen's edge: 0 is flush
        [Export] public int EdgeMargin { get; set; } = 0;

        // the health bar in the turn strip, the hero's and a foe's alike
        [Export] public Vector2 HealthBarSize { get; set; } = new Vector2(96, 14);

        // --- the tray's caption ---------------------------------------------------------------------------

        // how long "Hit! 17 against Armor Class 13" stays under the turn hint
        [Export] public float VerdictSeconds { get; set; } = 3.5f;
    }
}
