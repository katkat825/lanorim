using System;
using Godot;

namespace Game.Workshop
{
    // THE WORKSHOP'S PICTURES (cc_task_m 4.1, 4.3): a campaign's cover and a character's portrait, picked from anywhere
    // on disk and copied into the campaign at a fixed size, and the GM screens' own pictures, which are the cover a
    // campaign has until it's given one and what the wizard's GM screen picker shows
    public static class Pictures
    {
        // a cover is copied in at 640 x 360 (pack.json's "preview"), a portrait at 256 square: cropped to that shape from
        // the middle, then scaled
        public static readonly Vector2I Cover = new(640, 360);

        public static readonly Vector2I Portrait = new(256, 256);

        // a picture on disk (or in res://), or null when there's none there or it isn't a picture
        public static Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (path.StartsWith("res://", StringComparison.Ordinal))
                return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

            if (!System.IO.File.Exists(path)) return null;

            Image image = Image.LoadFromFile(path);
            return image == null || image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
        }

        // the GM screen's own picture; the plain screen has none, and is drawn as its colour
        public static Texture2D GmScreen(string skin) =>
            Load($"res://models/gm_screen/gm_screen_{skin}_gm_screen_{skin}_image.png");

        // A PICTURE COPIED IN: `from` read, cut to `size`'s shape from its middle, scaled to it and written to `to` as a
        // PNG. null when it was, or why not (the engine's own error name, which is the disk's and not the game's)
        public static string Import(string from, string to, Vector2I size)
        {
            Image image = Image.LoadFromFile(from);

            if (image == null || image.IsEmpty()) return "not a picture";

            float want = (float)size.X / size.Y;
            int w = image.GetWidth(), h = image.GetHeight();

            Rect2I keep = (float)w / h > want
                ? new Rect2I((w - (int)(h * want)) / 2, 0, (int)(h * want), h)
                : new Rect2I(0, (h - (int)(w / want)) / 2, w, (int)(w / want));

            image = image.GetRegion(keep);
            image.Resize(size.X, size.Y, Image.Interpolation.Lanczos);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(to) ?? ".");
            Error saved = image.SavePng(to);

            return saved == Error.Ok ? null : saved.ToString();
        }

        // a picture shown at a size, kept to its shape
        public static TextureRect Shown(Texture2D texture, Vector2 size, string name = "Picture") => new()
        {
            Name = name,
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = size,
        };

        // where nothing's picked: the GM screen's picture, or the plain screen's colour
        public static Control CoverOf(string cover, string skin, Vector2 size)
        {
            Texture2D texture = Load(cover) ?? GmScreen(skin);

            if (texture != null) return Shown(texture, size, "Cover");

            return new ColorRect { Name = "Cover", Color = new Color(0.32f, 0.26f, 0.2f), CustomMinimumSize = size };
        }

        // the system's file picker, for a PNG or a JPG: `picked` gets its path
        public static FileDialog Picker(Node parent, Action<string> picked)
        {
            var dialog = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.OpenFile,
                Access = FileDialog.AccessEnum.Filesystem,
                Filters = new[] { "*.png, *.jpg, *.jpeg" },
                UseNativeDialog = true,
                Title = Screens.Ui.Say(Content.Workshop.WorkshopWords.PickPictureKey),
            };

            dialog.FileSelected += path =>
            {
                picked(path);
                dialog.QueueFree();
            };
            dialog.Canceled += dialog.QueueFree;

            parent.AddChild(dialog);
            dialog.PopupCentered(new Vector2I(900, 600));
            return dialog;
        }
    }
}
