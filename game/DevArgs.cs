using System;
using System.Globalization;
using Godot;

namespace Game
{
    // THE COMMAND LINE, READ ONCE (cc_task_j-split-classes-dev-hooks.md Part 2). Every flag after `--` the game knows,
    // as a typed member. This is the only call to OS.GetCmdlineUserArgs() in the game: a class that a flag changes reads
    // its value here, and only where the flag really changes it. Which flags there are, what each adds to the scene and
    // who asks is the dev registry's (Dev/DevHooks.cs, and the README's list beside it).
    //
    // A release build reads the same line but has no registry (game/Dev/ isn't compiled into it), so of these only
    // what the game itself reads means anything there: the first screen (--show, --open) and the play switches.
    // Each value is parsed the way the class that used to read it parsed it, defaults included
    public static class DevArgs
    {
        static string[] _all;

        // the words after `--`, as given
        public static string[] All => _all ??= OS.GetCmdlineUserArgs() ?? Array.Empty<string>();

        public static bool Has(string flag) => Array.IndexOf(All, flag) >= 0;

        // the word after the flag, whatever it is, or null
        public static string Value(string flag)
        {
            int at = Array.IndexOf(All, flag);
            return at >= 0 && at + 1 < All.Length ? All[at + 1] : null;
        }

        // the word after the flag unless it is the next flag (`--companion --mood alert` names no look)
        public static string Word(string flag) => Value(flag) is { } value && !value.StartsWith("--") ? value : null;

        // the word after the word after the flag, unless it is a flag (`--begin <campaign> [class]`)
        static string Second(string flag)
        {
            int at = Array.IndexOf(All, flag);
            return at >= 0 && at + 2 < All.Length && !All[at + 2].StartsWith("--") ? All[at + 2] : null;
        }

        static int? Int(string flag) => int.TryParse(Value(flag), out int value) ? value : null;

        static float? Float(string flag) =>
            float.TryParse(Value(flag), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : null;

        // --- the launch screen ---------------------------------------------------------------------------------------

        // `--show book|<a book page>|controls|create`: the first screen; the title when not asked
        public static string Show => Value("--show") ?? "title";

        // `--open <campaign>`: the book open at that campaign's pages, or the map builder on its maps
        public static string Open => Value("--open");

        // `--show create --page spells --class mage`: a creation page part filled in
        public static string Page => Value("--page");

        // the demonstration hero's class, and a creation page's
        public static string Class => Word("--class");

        // the demonstration hero's species or lineage ("fairy", "beastfolk_vulpine"), and a creation page's
        public static string Species => Word("--species");

        // `--show create --page look --look 2,4`: the Look page with that skin tone and hair colour picked (FigureSkins)
        public static (int Skin, int Hair)? Look =>
            Value("--look")?.Split(',') is { Length: 2 } two && int.TryParse(two[0], out int skin) && int.TryParse(two[1], out int hair)
                ? (skin, hair)
                : null;

        // `--show create --page equipment --longest`: the page with the picks that make it longest (cc_task_m3 Part 1)
        public static bool Longest => Has("--longest");

        public static bool BookOverlay => Has("--book-overlay");

        public static bool AskDelete => Has("--ask-delete");

        // `--begin <campaign> [class]`: a character with the defaults, at the table
        public static string Begin => Value("--begin");

        public static string BeginClass => Second("--begin") ?? "fighter";

        public static string Learn => Value("--learn");

        public static bool Gold => Has("--gold");

        // `--build <campaign> [map]`: that map, or a new one, in the map builder
        public static string Build => Value("--build");

        public static string BuildMap => Second("--build");

        // `--template <id>`: with --build, a new map starts as that template (cc_task_l Part 1.3)
        public static string Template => Value("--template");

        // `--tool <tool>`: the builder opens with that tool in hand (a picture of the palette, the walls...)
        public static string Tool => Value("--tool");

        // `--picker buildings|props|walls|problems`: the builder opens with that picker or pop-up open (a picture of it)
        public static string Picker => Value("--picker");

        // `--trial-panel`: the builder opens its Play this map panel (a picture of it)
        public static bool TrialPanel => Has("--trial-panel");

        // `--prop-icons`: the palette's thumbnails rendered into game/ui/prop_icons/, then quit (not headless)
        public static bool PropIcons => Has("--prop-icons");

        // --- the Workshop (cc_task_m) ----------------------------------------------------------------------------------

        // `--mine <folder>`: My campaigns and My maps kept in <folder>/campaigns and <folder>/maps, not the player's
        public static string Mine => Value("--mine");

        // `--show workshop --wizard <page>`: the new-campaign wizard at that page (about, rules, first_chapter), filled in
        public static string Wizard => Value("--wizard");

        public static string Card => Value("--card");

        public static string Press => Value("--press");

        public static int Scroll => int.TryParse(Value("--scroll"), out int px) ? px : 0;

        // `--show workshop --open <campaign> --tab <tab>`: the campaign's page at that tab (about, chapters, characters)
        public static string Tab => Value("--tab");

        // --- the probes and the checks -------------------------------------------------------------------------------

        public static bool ControlsProbe => Has("--controls");

        public static bool CreationProbe => Has("--creation");

        public static bool MapProbe => Has("--map-probe");

        public static bool WorkshopProbe => Has("--workshop-probe");

        public static bool LayoutProbe => Has("--layout");

        public static bool ProneProbe => Has("--prone-probe");

        // `--pose Attack --pose-at 0.4`: every hero mini held in that clip, so far in (a picture mid-swing, cc_task_m3)
        public static string Pose => Value("--pose");

        public static float? PoseAt => float.TryParse(Value("--pose-at"), System.Globalization.NumberStyles.Float,
                                                      System.Globalization.CultureInfo.InvariantCulture, out float at) ? at : null;

        // `--cast command:grovel`: the hero's first turn casts it (CastProbe, cc_task_m3 Part 2)
        public static string Cast => Value("--cast");

        public static bool AccessProbe => Has("--access-probe");

        public static bool LocaleProbe => Has("--locale");

        public static bool FiguresProbe => Has("--figures-probe");

        // `--dice d20 --throws 400 --handful 8`: the fairness sweep. the shape is the word after it, read by the probe
        public static bool Dice => Has("--dice");

        public static string DiceShape => Value("--dice");

        public static int Throws => Int("--throws") ?? 200;

        public static int Handful => Int("--handful") ?? 8;

        public static string TraySkin => Value("--tray-skin");

        public static string Alternate => Value("--alternate");

        // --- the picture ---------------------------------------------------------------------------------------------

        public static bool Shot => Has("--shot");

        public static string ShotPath => Word("--shot") ?? "table.png";

        public static int ShotAfter => Int("--after") ?? 150;

        // `--size 2560x1440`, or 0x0
        public static Vector2I ShotSize
        {
            get
            {
                string[] wh = (Value("--size") ?? "").Split('x');

                return wh.Length == 2 && int.TryParse(wh[0], out int w) && int.TryParse(wh[1], out int h)
                    ? new Vector2I(w, h)
                    : default;
            }
        }

        public static float? Zoom => Float("--zoom");

        public static int Quarter => Int("--quarter") ?? 0;

        public static string Focus => Value("--focus");

        public static bool TrayUp => Has("--tray-up");

        public static int Frames => Math.Max(1, Int("--frames") ?? 1);

        // --- the demonstration table ---------------------------------------------------------------------------------

        public static bool Lineup => Has("--lineup");

        public static bool Companion => Has("--companion");

        public static string CompanionLook => Word("--companion");

        public static string Mood => Word("--mood");

        public static double MoodAfter =>
            double.TryParse(Word("--mood-after"), NumberStyles.Float, CultureInfo.InvariantCulture, out double s) ? s : 2.0;

        public static bool Corners => Has("--corners");

        // every wall style with its door, on the demonstration table (cc_task_m3 Part 6)
        public static bool WallStyles => Has("--wall-styles");

        public static bool OpenDoor => Has("--open-door");

        // --- play ----------------------------------------------------------------------------------------------------

        // plays itself: first choices, the AutoPlayer in fights, dice thrown as soon as asked
        public static bool Auto => Has("--auto");

        public static bool AutoDice => Has("--autodice");

        public static bool AutoStory => Has("--autostory");

        // `--start <node>`: the story starts there, not at the chapter's first
        public static string Start => Value("--start");

        // the pause menu opened on the hero's first turn in a fight
        public static bool Pause => Has("--pause");

        // `--say-bark <pool>`: a line from that pool every few seconds, past the chatter
        public static string SayBark => Value("--say-bark");

        // --- how things look -----------------------------------------------------------------------------------------

        // `--gm-screen <skin>`: another of the GM screens at the table
        public static string GmScreen => Value("--gm-screen");

        // `--gm-tallness 1`: the screen at its authored height
        public static float? GmTallness => Float("--gm-tallness");

        // `--tray-footprint 0.65 --tray-walls 1`: another size of dice tray, 0.4 to 3
        public static float? TrayFootprint => Float("--tray-footprint") is float f ? Mathf.Clamp(f, 0.4f, 3f) : null;

        public static float? TrayWalls => Float("--tray-walls") is float w ? Mathf.Clamp(w, 0.4f, 3f) : null;

        // the hero's mini: `--old-hero` the board's own figure, `--plain-pose` Idle_A, `--empty-handed` nothing held,
        // `--figure 1` the second figure, `--head kaykit/Mage.glb` any head on the body
        public static bool OldHero => Has("--old-hero");

        public static bool PlainPose => Has("--plain-pose");

        public static bool EmptyHanded => Has("--empty-handed");

        public static int? Figure => Int("--figure");

        public static string Head => Value("--head");
    }
}
