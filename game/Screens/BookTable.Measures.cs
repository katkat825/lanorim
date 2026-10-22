using Godot;

namespace Game.Screens
{
    // THE BOOK'S MEASURED PARTS (cc_task_m2 Part 2), apart from BookTable.cs's framing and opening: where the closed
    // cover's red is, for the title to sit on it, and where the open book's paper ends, for the right page's words to
    // mirror the left's. Each is measured once from its KayKit model and kept here with how it was measured; another
    // book model needs them measured again
    public partial class BookTable
    {
        // THE RED OF THE COVER, in the closed model's own space (KayKit spellbook_closed.gltf: z across the cover from
        // the spine, y along it, x up through it). Measured once, 2026-10-22, by sampling the model's atlas at every
        // vertex: the front cover's flat red runs z -0.145 to 0.216, the hinge's bevel z -0.181 to -0.145 before it,
        // and the two straps cross the cover at y -0.172 to -0.081 and 0.081 to 0.172 (from the spine to z 0.057), so
        // the red band between them is y -0.081 to 0.081. Another book model needs these measured again
        static readonly Vector2 RedAcross = new Vector2(-0.145f, 0.216f);

        static readonly Vector2 RedAlong = new Vector2(-0.081f, 0.081f);

        // and the cover's face is x 0.115 (the studs stand to 0.147): the title lies on it, not floating at the studs'
        // height, where the camera's slant would carry it up and off the red
        const float RedFace = 0.115f;

        Transform3D ClosedToTable() =>
            GlobalTransform.AffineInverse() * _closed.GetChild<Node3D>(0).GetChild<Node3D>(0).GlobalTransform;

        // how high the red face is above the table
        float RedHeight() => (ClosedToTable() * new Vector3(RedFace, 0f, 0f)).Y;

        // the red band between the straps as it lies on the table, seen from above: x across, y the table's z
        Rect2 RedOnTheTable()
        {
            Transform3D toTable = ClosedToTable();
            var rect = new Rect2();
            bool first = true;

            foreach (float z in new[] { RedAcross.X, RedAcross.Y })
                foreach (float y in new[] { RedAlong.X, RedAlong.Y })
                {
                    Vector3 at = toTable * new Vector3(RedFace, y, z);
                    var flat = new Vector2(at.X, at.Z);

                    rect = first ? new Rect2(flat, Vector2.Zero) : rect.Expand(flat);
                    first = false;
                }

            return rect;
        }

        // THE RIGHT PAGE'S WORDS MIRROR THE LEFT'S (cc_task_m2 Part 2): they start as far in from the crease as the left
        // page's words start in from the page's outer edge, both measured on the screen at the words' top line, where
        // the slanting edges are. `lean` is how far the left column is set in further when it is turned (BookPages).
        // The one place the two margins are tied: change the left page's inset and the right follows
        public Rect2 Mirrored(Rect2 left, Rect2 right, float lean)
        {
            if (_open == null || _camera == null) return right;

            Transform3D toTable = GlobalTransform.AffineInverse() * _open.GetChild<Node3D>(0).GetChild<Node3D>(0).GlobalTransform;

            Vector2 At(float x, float along) => _camera.UnprojectPosition(ToGlobal(toTable * new Vector3(x, along, PaperTop)));

            // the left page's outer edge, and the crease, from the far end of the book to the near
            float outer = EdgeAt(At(-PaperEdge, -PaperAlong), At(-PaperEdge, PaperAlong), left.Position.Y);
            float crease = EdgeAt(At(0f, -PaperAlong), At(0f, PaperAlong), right.Position.Y);
            float start = crease + (left.Position.X + lean - outer);

            return new Rect2(start, right.Position.Y, Mathf.Max(0f, right.End.X - start), right.Size.Y);
        }

        // THE PAPER OF THE OPEN BOOK, in its model's own space (KayKit spellbook_open.gltf: x across from the spine, y along
        // it, z up). Measured once, 2026-10-22, from the model's vertices: the pages' outer edges stand at x -0.36 and 0.36,
        // z 0.134 (the red covers reach on to 0.414, lower), the spine is x 0, and the book runs y -0.284 to 0.284
        const float PaperEdge = 0.36f;
        const float PaperTop = 0.134f;
        const float PaperAlong = 0.284f;

        // where a page's edge, from its far corner to its near one, crosses a line across the screen at y
        static float EdgeAt(Vector2 far, Vector2 near, float y) =>
            Mathf.IsEqualApprox(far.Y, near.Y) ? far.X : Mathf.Lerp(far.X, near.X, (y - far.Y) / (near.Y - far.Y));
    }
}
