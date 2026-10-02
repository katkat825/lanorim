using System;
using System.Linq;
using Godot;

namespace Game.Table
{
    // A PICTURE OF THE TABLE, SAVED AND QUIT.
    //
    //   godot --path game res://table.tscn -- --shot table.png
    //
    // Not headless - headless has no renderer, and the whole point is the rendering. It opens a
    // window, waits for the dice to settle and the camera to stop drifting, saves a PNG and goes.
    //
    // WHY IT EXISTS. ART_DIRECTION section 8 says the shader's numbers "must be tuned by eye" and
    // that the screenshot loop is their natural home: apply, screenshot, adjust, repeat. This is
    // the screenshot half of that loop, so the adjusting half can be done against a picture rather
    // than against an argument. It is also the only way anybody who cannot see the editor can say
    // anything honest about how the table looks - which is to say: it cannot, and this is how it
    // hands the question to somebody who can.
    public partial class Shot : Node
    {
        public const string Flag = "--shot";

        // where to write it. relative paths land beside the project
        public string Path { get; set; } = "table.png";

        // frames to let the scene settle first: the dice are still falling for the first second
        // and a half, and the camera's handheld drift never stops
        public int After { get; set; } = 150;

        int _waited;

        // `--size 2560x1440`: the window made that size once it is up. `--resolution` is clamped to
        // the screen's work area, so a 1440-tall shot on a 1080 screen came out 1061 tall
        public Vector2I Size { get; set; }

        // `--zoom 1 --quarter 2 --focus goblin`: the camera parked at that zoom (0 all the way out, 1
        // all the way in) and quarter, following the first mini whose name starts with that - for a
        // close look at one figure from each side
        public float? Zoom { get; set; }

        public int Quarter { get; set; }

        public string Focus { get; set; }

        // `--tray-up`: wait (past --after) for the tray to have come to the player and its dice to be
        // read, and photograph that - the moment the player reads the roll
        public bool WhenTrayUp { get; set; }

        // `--frames 90`: that many pictures, one a frame, numbered beside Path (table_000.png...), for measuring what
        // changes frame to frame with nothing moving but the handheld drift (tools/flicker.ps1, cc_task_f 1.7)
        public int Frames { get; set; } = 1;

        int _saved;

        TableCamera _camera;

        public override void _Ready()
        {
            // the game opens maximized (project.godot), and a maximized window keeps its size
            if (Size.X > 0 && Size.Y > 0)
            {
                GetWindow().Mode = Window.ModeEnum.Windowed;
                GetWindow().Size = Size;
            }

            _camera = Nodes.Under<TableCamera>(GetTree().Root).FirstOrDefault();

            if (_camera == null) return;

            if (Zoom is float zoom) _camera.ZoomBy(zoom - _camera.Zoom);
            if (Quarter != 0) _camera.Turn(Quarter);
        }

        void Follow()
        {
            if (_camera == null || string.IsNullOrEmpty(Focus)) return;

            Game.Board.Mini mini = Nodes.Under<Game.Board.Mini>(GetTree().Root)
                                        .FirstOrDefault(m => m.Name.ToString().StartsWith(Focus, StringComparison.OrdinalIgnoreCase));

            // or anything else on the table by its name (`--focus Companion`)
            Node3D other = mini ?? Nodes.Under<Node3D>(GetTree().Root)
                                        .FirstOrDefault(n => n.Name.ToString().StartsWith(Focus, StringComparison.OrdinalIgnoreCase));

            if (other != null) _camera.Following = other.GlobalPosition;
        }

        bool TrayUpAndRead()
        {
            Game.Tray.TrayLift lift = Nodes.Under<Game.Tray.TrayLift>(GetTree().Root).FirstOrDefault();
            Game.Tray.DiceTray tray = Nodes.Under<Game.Tray.DiceTray>(GetTree().Root).FirstOrDefault();

            return lift != null && tray != null && lift.IsUp && !tray.IsThrowing && tray.Last != null;
        }

        // the shot the command line asks for, or null
        public static Shot From(string[] args) =>
            RequestedFrom(args, out string path, out int after, out Vector2I size)
                ? new Shot
                {
                    Name = "Shot", Path = path, After = after, Size = size,
                    Zoom = Arg(args, "--zoom") is { } z && float.TryParse(z, System.Globalization.NumberStyles.Float,
                                                                         System.Globalization.CultureInfo.InvariantCulture, out float zoom)
                        ? zoom : null,
                    Quarter = int.TryParse(Arg(args, "--quarter"), out int quarter) ? quarter : 0,
                    Focus = Arg(args, "--focus"),
                    WhenTrayUp = Array.IndexOf(args ?? Array.Empty<string>(), "--tray-up") >= 0,
                    Frames = int.TryParse(Arg(args, "--frames"), out int frames) ? Math.Max(1, frames) : 1,
                }
                : null;

        static string Numbered(string path, int index) =>
            System.IO.Path.ChangeExtension(path, null) + $"_{index:000}" + System.IO.Path.GetExtension(path);

        internal static string Arg(string[] args, string flag)
        {
            int at = Array.IndexOf(args ?? Array.Empty<string>(), flag);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        public static bool RequestedFrom(string[] args, out string path, out int after, out Vector2I size)
        {
            path = "table.png";
            after = 150;
            size = default;

            int sized = Array.IndexOf(args ?? Array.Empty<string>(), "--size");

            if (sized >= 0 && sized + 1 < args.Length)
            {
                string[] wh = args[sized + 1].Split('x');

                if (wh.Length == 2 && int.TryParse(wh[0], out int w) && int.TryParse(wh[1], out int h))
                    size = new Vector2I(w, h);
            }

            if (args == null) return false;

            int at = Array.IndexOf(args, Flag);

            if (at < 0) return false;

            if (at + 1 < args.Length && !args[at + 1].StartsWith("--")) path = args[at + 1];

            int frames = Array.IndexOf(args, "--after");

            if (frames >= 0 && frames + 1 < args.Length &&
                int.TryParse(args[frames + 1], out int asked))
                after = asked;

            return true;
        }

        public override void _Process(double delta)
        {
            Follow();

            if (_waited++ < After) return;

            if (WhenTrayUp && !TrayUpAndRead()) return;

            Viewport viewport = GetViewport();

            if (viewport == null)
            {
                GD.PrintErr("shot: no viewport to photograph");
                GetTree().Quit(1);
                return;
            }

            Image picture = viewport.GetTexture()?.GetImage();

            if (picture == null)
            {
                GD.PrintErr("shot: the viewport gave back no image - is this running headless? " +
                            "headless has no renderer, and a picture of nothing is not useful");
                GetTree().Quit(1);
                return;
            }

            // whether the window fits the screen above the taskbar (cc_task_ui-issues-9-30.md 1.3)
            Window window = GetWindow();
            Rect2I usable = DisplayServer.ScreenGetUsableRect(window.CurrentScreen);
            GD.Print($"shot    window {window.Size.X}x{window.Size.Y} at {window.Position.X},{window.Position.Y} " +
                     $"({window.Mode}); the screen's usable area {usable.Size.X}x{usable.Size.Y} at {usable.Position.X},{usable.Position.Y}");

            string path = Frames > 1 ? Numbered(Path, _saved) : Path;
            Error wrote = picture.SavePng(path);

            if (wrote != Error.Ok)
            {
                GD.PrintErr($"shot: could not write {path} - {wrote}");
                GetTree().Quit(1);
                return;
            }

            if (++_saved < Frames) return;

            SetProcess(false);
            GD.Print($"shot    {picture.GetWidth()} x {picture.GetHeight()} saved to {path}" +
                     (Frames > 1 ? $" ({Frames} frames)" : ""));

            // a shot of a --begin run leaves no probe saves behind
            Game.Play.GameState.ForgetProbeSaves();
            GetTree().Quit(0);
        }
    }
}
