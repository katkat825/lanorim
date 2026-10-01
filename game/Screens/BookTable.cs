using Game.Board;
using Godot;

namespace Game.Screens
{
    // THE CAMPAIGN BOOK ON THE TABLE (cc_task_ui-issues-9-30.md 6.3, cc_task_ui-issues-10-01.md 3). The launch
    // screen stands on this: the table's wood, the lamp, and a book. CLOSED, it is the title: turned a quarter
    // counterclockwise and stood large, "Lanorim" on its cover like a book's title and one Begin button on the
    // cover (BookCover). OPEN, its two pages are the menu, written on them like a table of contents
    // (BookPages); when the pages come out too small to read, the menu falls back to panels over the book.
    //
    // Both read the book's place on the screen from here (CoverOnScreen, PagesOnScreen), every frame, so they
    // stay on it at every window size. The camera frames the book for the screen's shape.
    //
    // Every size, turn and time here is an export: the look is Kathleen's to decide from the screenshots.
    public partial class BookTable : Node3D
    {
        [Export] public PackedScene ClosedModel { get; set; }

        [Export] public PackedScene OpenModel { get; set; }

        // the painted-miniature material the board's props wear, so the book is one of them; none
        // leaves the model's own materials
        [Export] public Material Paint { get; set; }

        [Export] public NodePath CameraPath { get; set; } = "Camera3D";

        // KayKit models the books standing up, as a hand holds them; these lay them on the table,
        // in degrees (the closed one on its back cover, the open one pages up)
        [Export] public Vector3 ClosedTurn { get; set; } = new Vector3(0f, 0f, 90f);

        [Export] public Vector3 OpenTurn { get; set; } = new Vector3(-90f, 0f, 0f);

        // the closed book turned on the table, degrees counterclockwise seen from above (Kathleen: a quarter)
        [Export] public float ClosedYaw { get; set; } = 90f;

        // how wide the book is on the table, in metres (the open one; the closed one is half of it)
        [Export] public float OpenWidth { get; set; } = 0.9f;

        // degrees the camera looks down, and how much of the screen's height the book fills
        [Export] public float Pitch { get; set; } = 62f;

        // the closed book's share as it was framed on 2026-10-01, and how many times that it stands now
        // (Kathleen: 2.5) - as near as fits inside MostOfTheHeight and MostOfTheWidth
        [Export(PropertyHint.Range, "0.2,1.2,0.01")] public float ClosedShare { get; set; } = 0.42f;

        [Export(PropertyHint.Range, "0.5,4,0.05")] public float ClosedScale { get; set; } = 2.5f;

        [Export(PropertyHint.Range, "0.2,1.2,0.01")] public float OpenShare { get; set; } = 0.85f;

        // at most this much of the screen's width and height (a narrow screen fits the book by its width)
        [Export(PropertyHint.Range, "0.2,1.2,0.01")] public float MostOfTheWidth { get; set; } = 0.95f;

        [Export(PropertyHint.Range, "0.2,1.2,0.01")] public float MostOfTheHeight { get; set; } = 0.92f;

        // where the book sits up the screen: 0 in the middle, positive puts it higher (metres at the book)
        [Export] public float ClosedLift { get; set; } = 0f;

        [Export] public float OpenSeconds { get; set; } = 0.45f;

        // THE TITLE ON THE COVER: how much of the cover's width it spans, how far down the cover its middle
        // is (0 the top edge, 1 the bottom: turned a quarter, the KayKit book's two straps cross the cover, and
        // the title sits in the band between them), its colour, and its outline
        [Export(PropertyHint.Range, "0.2,1,0.01")] public float TitleWidth { get; set; } = 0.62f;

        [Export(PropertyHint.Range, "0,1,0.01")] public float TitleAt { get; set; } = 0.55f;

        [Export] public Color TitleColour { get; set; } = new Color(0.95f, 0.82f, 0.48f);

        [Export] public Color TitleOutline { get; set; } = new Color(0.22f, 0.08f, 0.06f);

        [Export] public int TitleOutlineSize { get; set; } = 10;

        // none takes the theme's title font (AlegreyaSC-Bold)
        [Export] public Font TitleFont { get; set; }

        const string TitleFontPath = "res://fonts/AlegreyaSC-Bold.ttf";

        // where the Begin button's middle is down the cover as it stands on the screen (BookCover): under the
        // lower strap
        [Export(PropertyHint.Range, "0,1,0.01")] public float BeginAt { get; set; } = 0.9f;

        // THE CONTENTS ON THE PAGES (BookPages): how far in from each page's edges the words stand, as a share
        // of the page (across, down); the largest and smallest letters, in canvas pixels - smaller than the
        // least and the menu goes back to panels over the book; and the line height, in letter heights
        [Export] public Vector2 PageInset { get; set; } = new Vector2(0.14f, 0.15f);

        [Export] public int ContentsFontMax { get; set; } = 40;

        [Export] public int ContentsFontMin { get; set; } = 22;

        [Export(PropertyHint.Range, "1,3,0.05")] public float ContentsLine { get; set; } = 1.9f;

        // THE WORDS FOLLOW THE PAGES (cc_task_working-notes-10-01.md 2.2: "angle the Continue, Tutorial, Settings
        // etc. inward and downward just slightly so they look like they're curving with the pages"). Each page's
        // column turns this many degrees toward the spine about its outer top corner, so a line runs down toward
        // the gutter the way the page's own edge does (about 8 degrees at the top of the page, 6 at the foot,
        // measured off the 1280x720 screenshot). 0 sets them level
        [Export(PropertyHint.Range, "0,15,0.5")] public float PageTilt { get; set; } = 6f;

        Node3D _closed;
        Node3D _open;
        Label3D _title;
        Camera3D _camera;
        bool _isOpen;
        float _opening = 1f;

        public bool IsOpen => _isOpen;

        // open or shut, and done moving: what is on the screen is where it will stay
        public bool Still => _opening >= 1f && _framed;

        bool _framed;

        public Camera3D Camera => _camera;

        public override void _Ready()
        {
            _camera = GetNodeOrNull<Camera3D>(CameraPath);

            _closed = Stand(ClosedModel, "Closed", OpenWidth * 0.5f, ClosedTurn);
            _open = Stand(OpenModel, "Open", OpenWidth, OpenTurn);

            if (_closed != null) _closed.RotationDegrees = new Vector3(0f, ClosedYaw, 0f);
            if (_open != null) _open.Visible = false;

            _title = Title();
        }

        // the model laid down (turn) inside a holder the size of the table's book, standing on the
        // table with its middle over the table's middle; the holder is what opens and closes
        Node3D Stand(PackedScene scene, string name, float width, Vector3 turn)
        {
            if (scene?.Instantiate() is not Node3D model)
            {
                GD.PushWarning($"book: no {name.ToLowerInvariant()} book model - the table has no book on it");
                return null;
            }

            var holder = new Node3D { Name = name };
            AddChild(holder);

            var laid = new Node3D { Name = "Laid", RotationDegrees = turn };
            holder.AddChild(laid);
            laid.AddChild(model);

            if (Paint != null) PaintedModel.Paint(model, Paint, "book");

            Aabb bounds = PaintedModel.Bounds(holder);
            float scale = PaintedModel.ToFitWidth(bounds, width);

            holder.Scale = Vector3.One * scale;

            Vector3 centre = bounds.GetCenter();
            laid.Position = new Vector3(-centre.X, -bounds.Position.Y, -centre.Z);

            return holder;
        }

        // "Lanorim" lying on the closed cover, as wide as TitleWidth of it, in the title font
        Label3D Title()
        {
            if (_closed == null) return null;

            Aabb cover = Here(_closed);
            string words = Ui.Say(ScreenWords.GameTitle);
            TitleFont ??= GD.Load<Font>(TitleFontPath);

            var title = new Label3D
            {
                Name = "Title",
                Text = words,
                Font = TitleFont,
                FontSize = 128,
                Modulate = TitleColour,
                OutlineModulate = TitleOutline,
                OutlineSize = TitleOutlineSize,
                RotationDegrees = new Vector3(-90f, 0f, 0f),
                Position = new Vector3(cover.GetCenter().X, cover.End.Y + 0.002f,
                                       cover.Position.Z + cover.Size.Z * TitleAt),
                DoubleSided = false,
            };

            float wide = TitleFont?.GetStringSize(words, HorizontalAlignment.Left, -1, title.FontSize).X ?? 0f;
            if (wide > 0f) title.PixelSize = cover.Size.X * TitleWidth / wide;

            AddChild(title);
            return title;
        }

        public void Open() => Set(true);

        public void Close() => Set(false);

        void Set(bool open)
        {
            if (open == _isOpen) return;

            _isOpen = open;
            _opening = 0f;
            _framed = false;

            if (_closed != null) _closed.Visible = !open;
            if (_open != null) _open.Visible = open;
            if (_title != null) _title.Visible = !open;
        }

        public override void _Process(double delta)
        {
            if (_opening < 1f) _opening = Mathf.Min(1f, _opening + (float)delta / Mathf.Max(0.01f, OpenSeconds));

            // opening: the pages spread from the spine
            float t = _opening * _opening * (3f - 2f * _opening);
            Node3D shown = _isOpen ? _open : _closed;

            if (shown != null)
            {
                float x = Mathf.Lerp(_isOpen ? 0.55f : 1.1f, 1f, t);
                shown.Scale = new Vector3(Base(shown) * x, Base(shown), Base(shown));
            }

            Frame();
        }

        // the scale a model was stood at (its y, which the opening never touches)
        static float Base(Node3D model) => model.Scale.Y;

        // a node's bounds in the table's space, turned and scaled as it stands
        Aabb Here(Node3D node) =>
            GlobalTransform.AffineInverse() * node.GlobalTransform * PaintedModel.Bounds(node);

        // the camera looks down at the book from Pitch, as far back as the book's share of the screen says,
        // and never so near that any corner of the book leaves MostOfTheHeight and MostOfTheWidth of the screen.
        // The closed book stands ClosedScale times nearer than 2026-10-01's framing (the old book, laid long
        // across), as near as fits
        void Frame()
        {
            if (_camera == null) return;

            Node3D book = _isOpen ? _open : _closed;
            if (book == null) return;

            Vector2 screen = GetViewport().GetVisibleRect().Size;
            float aspect = screen.Y > 0 ? screen.X / screen.Y : 16f / 9f;
            float tan = Mathf.Tan(Mathf.DegToRad(_camera.Fov) * 0.5f);
            float pitch = Mathf.DegToRad(Mathf.Clamp(Pitch, 10f, 89f));

            Vector3 look = new Vector3(0f, 0f, _isOpen ? 0f : -ClosedLift);
            Vector3 back = new Vector3(0f, Mathf.Sin(pitch), Mathf.Cos(pitch));

            float distance;

            if (_isOpen)
            {
                float deep = OpenWidth * 0.72f * Mathf.Sin(pitch);
                distance = Mathf.Max(deep / (2f * tan * OpenShare), OpenWidth / (2f * tan * aspect * MostOfTheWidth));
            }
            else
            {
                float width = OpenWidth * 0.5f;
                float then = Mathf.Max(width * 1.35f * Mathf.Sin(pitch) / (2f * tan * ClosedShare),
                                       width / (2f * tan * aspect * MostOfTheWidth));

                distance = then / Mathf.Max(0.1f, ClosedScale);
            }

            distance = Mathf.Max(distance, Nearest(Here(book), look, back, tan, aspect));

            Vector3 from = look + back * distance;

            float weight = _opening >= 1f ? 1f : 0.2f;
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(from, weight);
            _camera.LookAt(look, Vector3.Up);
            _framed = _opening >= 1f;
        }

        // how near the camera can stand (along back, from look) with every corner of the box on the screen
        // inside the margins. A corner's height on the screen is its height across the camera over its depth
        // in front of it, and the depth grows one for one with the distance, so each corner gives a least
        // distance outright
        float Nearest(Aabb box, Vector3 look, Vector3 back, float tan, float aspect)
        {
            Vector3 forward = -back;
            Vector3 up = (Vector3.Up - forward * Vector3.Up.Dot(forward)).Normalized();
            Vector3 right = forward.Cross(up);
            float nearest = 0f;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = box.GetEndpoint(i) - look;
                float toward = corner.Dot(back);

                nearest = Mathf.Max(nearest, Mathf.Max(
                    Mathf.Abs(corner.Dot(up)) / (tan * MostOfTheHeight) + toward,
                    Mathf.Abs(corner.Dot(right)) / (tan * aspect * MostOfTheWidth) + toward));
            }

            return nearest;
        }

        // --- the book on the screen ------------------------------------------------------------------

        // the closed cover's top face as it stands on the screen (canvas pixels), or null with no book
        public Rect2? CoverOnScreen()
        {
            if (_closed == null || _camera == null || _isOpen) return null;

            Aabb cover = Here(_closed);
            return Outer(Top(cover, 0f, 0f));
        }

        // the open pages' words' room on the screen: the left half and the right half of the open book's top,
        // each PageInset in from its edges, kept inside the screen. Null with no open book
        public (Rect2 Left, Rect2 Right)? PagesOnScreen()
        {
            if (_open == null || _camera == null || !_isOpen) return null;

            Aabb book = Here(_open);
            float half = book.Size.X * 0.5f;

            var left = new Aabb(book.Position, new Vector3(half, book.Size.Y, book.Size.Z));
            var right = new Aabb(book.Position + new Vector3(half, 0f, 0f), new Vector3(half, book.Size.Y, book.Size.Z));

            Rect2 screen = GetViewport().GetVisibleRect();

            return (Inner(Top(left, PageInset.X, PageInset.Y)).Intersection(screen),
                    Inner(Top(right, PageInset.X, PageInset.Y)).Intersection(screen));
        }

        // the four corners of a box's top face, inset by a share of its width and depth
        static Vector3[] Top(Aabb box, float across, float down)
        {
            float x0 = box.Position.X + box.Size.X * across, x1 = box.End.X - box.Size.X * across;
            float z0 = box.Position.Z + box.Size.Z * down, z1 = box.End.Z - box.Size.Z * down;
            float y = box.End.Y;

            return new[] { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1) };
        }

        Vector2[] Projected(Vector3[] corners)
        {
            var points = new Vector2[corners.Length];

            for (int i = 0; i < corners.Length; i++) points[i] = _camera.UnprojectPosition(ToGlobal(corners[i]));

            return points;
        }

        // the rectangle round all four corners
        Rect2 Outer(Vector3[] corners)
        {
            Vector2[] p = Projected(corners);
            var rect = new Rect2(p[0], Vector2.Zero);

            foreach (Vector2 point in p) rect = rect.Expand(point);

            return rect;
        }

        // the rectangle inside the face's trapezoid: the far edge is the narrower one, so its corners set the
        // sides, and the far and near edges set the top and bottom (corners: far-left, far-right, near-left,
        // near-right)
        Rect2 Inner(Vector3[] corners)
        {
            Vector2[] p = Projected(corners);

            float left = Mathf.Max(p[0].X, p[2].X), right = Mathf.Min(p[1].X, p[3].X);
            float top = Mathf.Max(p[0].Y, p[1].Y), bottom = Mathf.Min(p[2].Y, p[3].Y);

            return new Rect2(left, top, Mathf.Max(0f, right - left), Mathf.Max(0f, bottom - top));
        }
    }
}
