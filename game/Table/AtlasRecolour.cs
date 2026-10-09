using System.Collections.Generic;
using System.Globalization;
using Game.Board;
using Godot;

namespace Game.Table
{
    // A COMPANION IN ITS OWN COLOURS (task G 2: the pigeon black, the Squidle red, the ghost white).
    //
    // An Ultimate Monsters model paints from a 32 x 32 atlas, one flat colour a pixel and one row a monster, and
    // every vertex's UV sits on one pixel's centre. A tint on the material would colour the eyes and mouth too, and
    // white can't be reached by multiplying at all. So the model gets its own copy of the atlas with the body's
    // cells repainted, made when it is put on (a 32 x 32 copy costs nothing) and given to that one model's
    // surfaces. The eyes keep their cell and their colour; the pack's atlas, which the other models share, is
    // never touched. The cells and colours are data (CompanionLook.Recolour), so a shade is one line to change.
    public static class AtlasRecolour
    {
        // the atlas's grid, cells a side
        const int Grid = 32;

        // repaints the model's atlas cells; returns how many surfaces now wear a copy
        public static int Apply(Node model, string recolour)
        {
            Dictionary<Vector2I, Color> cells = Parse(recolour);

            if (cells.Count == 0) return 0;

            var made = new Dictionary<Texture2D, ImageTexture>();
            int dressed = 0;

            foreach (MeshInstance3D mesh in PaintedModel.Meshes(model))
                for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                {
                    if (mesh.Mesh.SurfaceGetMaterial(surface) is not BaseMaterial3D { AlbedoTexture: { } atlas } standard)
                        continue;

                    if (!made.TryGetValue(atlas, out ImageTexture copy)) made[atlas] = copy = Repainted(atlas, cells);

                    var own = (BaseMaterial3D)standard.Duplicate();
                    own.AlbedoTexture = copy;
                    mesh.SetSurfaceOverrideMaterial(surface, own);
                    dressed++;
                }

            return dressed;
        }

        static ImageTexture Repainted(Texture2D atlas, Dictionary<Vector2I, Color> cells)
        {
            Image image = (Image)atlas.GetImage().Duplicate();

            if (image.IsCompressed()) image.Decompress();

            image.ClearMipmaps();
            image.Convert(Image.Format.Rgba8);

            int cell = Mathf.Max(1, image.GetWidth() / Grid);

            foreach ((Vector2I at, Color colour) in cells)
                image.FillRect(new Rect2I(at * cell, new Vector2I(cell, cell)), colour);

            image.GenerateMipmaps();

            return ImageTexture.CreateFromImage(image);
        }

        // "1,19 #1d1d22; 2,19 #2b2738": column,row and the colour it becomes
        public static Dictionary<Vector2I, Color> Parse(string recolour)
        {
            var cells = new Dictionary<Vector2I, Color>();

            foreach (string part in (recolour ?? "").Split(';', System.StringSplitOptions.RemoveEmptyEntries))
            {
                string[] bits = part.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                string[] at = bits.Length == 2 ? bits[0].Split(',') : System.Array.Empty<string>();

                if (at.Length != 2 || !int.TryParse(at[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int column) ||
                    !int.TryParse(at[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int row) ||
                    !Color.HtmlIsValid(bits[1]))
                {
                    GD.PushWarning($"companion: can't read the recolour '{part.Trim()}' - want \"column,row #rrggbb\"");
                    continue;
                }

                cells[new Vector2I(column, row)] = Color.FromHtml(bits[1]);
            }

            return cells;
        }
    }
}
