using Game.Screens;
using Godot;

namespace Game.Table
{
    // THE COMPANION'S BARK, ON SCREEN (cc_task_i 2.4): a short line in a box near the companion for a few seconds.
    // Built plain - its size, colours and timing are HudLayout's dials, and how it looks is Kathleen's. The text is
    // BBCode (tools/import_barks.py writes Kathleen's **bold** and *italics* as [b] and [i]); a new bark replaces the
    // one showing, which is how a higher pool cuts in
    public partial class BarkBubble : PanelContainer
    {
        public Camera3D Camera { get; set; }

        public Node3D Over { get; set; }

        RichTextLabel _text;
        double _left;

        public override void _Ready()
        {
            HudLayout layout = HudLayout.Current;

            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;
            CustomMinimumSize = new Vector2(layout.BarkWidth, 0);

            AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = layout.BarkBackground,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8,
            });

            _text = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(layout.BarkWidth - 28, 0),
            };
            _text.AddThemeColorOverride("default_color", layout.BarkText);
            foreach (string font in new[] { "normal_font_size", "bold_font_size", "italics_font_size" })
                _text.AddThemeFontSizeOverride(font, layout.BarkFontSize);
            AddChild(_text);
        }

        // a line for `seconds` (at least HudLayout.BarkSeconds)
        public void Say(string bbcode, double seconds)
        {
            _text.Text = bbcode ?? "";
            _left = System.Math.Max(seconds, HudLayout.Current.BarkSeconds);
            Size = new Vector2(HudLayout.Current.BarkWidth, 0);
            Visible = _text.Text.Length > 0;
            Place();
        }

        public string Showing => Visible ? _text.Text : "";

        public override void _Process(double delta)
        {
            if (!Visible) return;

            if ((_left -= delta) <= 0)
            {
                Visible = false;
                return;
            }

            Place();
        }

        // above the companion, centred on it, kept on the screen
        void Place()
        {
            if (Camera == null || Over == null || !Over.IsInsideTree()) return;

            Vector3 at = Over.GlobalPosition + Vector3.Up * HudLayout.Current.BarkLift;

            if (Camera.IsPositionBehind(at)) return;

            Vector2 point = Camera.UnprojectPosition(at);
            Rect2 screen = GetViewportRect();
            Vector2 size = Size;

            Position = new Vector2(Mathf.Clamp(point.X - size.X / 2, 8, screen.Size.X - size.X - 8),
                                   Mathf.Clamp(point.Y - size.Y - 12, 8, screen.Size.Y - size.Y - 8));
        }
    }
}
