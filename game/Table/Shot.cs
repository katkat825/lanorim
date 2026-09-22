using System;
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

        public static bool RequestedFrom(string[] args, out string path, out int after)
        {
            path = "table.png";
            after = 150;

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
            if (_waited++ < After) return;

            SetProcess(false);

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

            Error wrote = picture.SavePng(Path);

            if (wrote != Error.Ok)
            {
                GD.PrintErr($"shot: could not write {Path} - {wrote}");
                GetTree().Quit(1);
                return;
            }

            GD.Print($"shot    {picture.GetWidth()} x {picture.GetHeight()} saved to {Path}");

            GetTree().Quit(0);
        }
    }
}
