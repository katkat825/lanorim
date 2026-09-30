using Game.Board;
using Godot;

namespace Game.Screens
{
    // THE CAMPAIGN BOOK ON THE TABLE (cc_task_ui-issues-9-30.md 6.3). Kathleen: "either don't reference
    // a book, or make it look like a book". The launch screen stands on this: the table's wood, the
    // lamp, and a book - closed at the title, open under the campaign book's pages (updated_decisions.md:
    // the table of contents is the menu).
    //
    // THE MENU STAYS UI, LAID OVER THE OPEN BOOK. Text drawn onto the book's pages in 3D would be as
    // small as the pages are at 1280x720 and couldn't be read at every size (part 2), so the pages are
    // the backdrop and the menu is the theme's panels on top of them. The camera frames the book for
    // the screen's shape, so it fills the same share of the screen at 4:3 and at 21:9.
    //
    // Every size and time here is an export: the look is Kathleen's to decide from the screenshots.
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

        // how wide the book is on the table, in metres (the open one; the closed one is half of it)
        [Export] public float OpenWidth { get; set; } = 0.9f;

        // degrees the camera looks down, and how much of the screen's height the book fills
        [Export] public float Pitch { get; set; } = 62f;

        [Export(PropertyHint.Range, "0.2,1.2,0.01")] public float ClosedShare { get; set; } = 0.42f;

        [Export(PropertyHint.Range, "0.2,1.2,0.01")] public float OpenShare { get; set; } = 0.85f;

        // at most this much of the screen's width (a narrow screen fits the book by its width)
        [Export(PropertyHint.Range, "0.2,1.2,0.01")] public float MostOfTheWidth { get; set; } = 0.95f;

        // where the book sits up the screen: 0 in the middle, positive puts it higher (metres at the book)
        [Export] public float ClosedLift { get; set; } = 0.06f;

        [Export] public float OpenSeconds { get; set; } = 0.45f;

        Node3D _closed;
        Node3D _open;
        Camera3D _camera;
        bool _isOpen;
        float _opening = 1f;

        public bool IsOpen => _isOpen;

        public override void _Ready()
        {
            _camera = GetNodeOrNull<Camera3D>(CameraPath);

            _closed = Stand(ClosedModel, "Closed", OpenWidth * 0.5f, ClosedTurn);
            _open = Stand(OpenModel, "Open", OpenWidth, OpenTurn);

            if (_open != null) _open.Visible = false;
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

        public void Open() => Set(true);

        public void Close() => Set(false);

        void Set(bool open)
        {
            if (open == _isOpen) return;

            _isOpen = open;
            _opening = 0f;

            if (_closed != null) _closed.Visible = !open;
            if (_open != null) _open.Visible = open;
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

        // the camera looks down at the book from Pitch, as far back as the book's share of the screen says
        void Frame()
        {
            if (_camera == null) return;

            Node3D book = _isOpen ? _open : _closed;
            if (book == null) return;

            Vector2 screen = GetViewport().GetVisibleRect().Size;
            float aspect = screen.Y > 0 ? screen.X / screen.Y : 16f / 9f;
            float tan = Mathf.Tan(Mathf.DegToRad(_camera.Fov) * 0.5f);

            float width = _isOpen ? OpenWidth : OpenWidth * 0.5f;
            float pitch = Mathf.DegToRad(Mathf.Clamp(Pitch, 10f, 89f));
            float deep = width * (_isOpen ? 0.72f : 1.35f) * Mathf.Sin(pitch);
            float share = _isOpen ? OpenShare : ClosedShare;

            float distance = Mathf.Max(deep / (2f * tan * share), width / (2f * tan * aspect * MostOfTheWidth));

            Vector3 look = new Vector3(0f, 0f, _isOpen ? 0f : -ClosedLift);
            Vector3 from = look + new Vector3(0f, Mathf.Sin(pitch), Mathf.Cos(pitch)) * distance;

            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(from, _opening >= 1f ? 1f : 0.2f);
            _camera.LookAt(look, Vector3.Up);
        }
    }
}
