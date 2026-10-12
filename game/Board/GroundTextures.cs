using System.Collections.Generic;
using Content.Maps;
using Godot;

namespace Game.Board
{
    // WHAT EACH GROUND LOOKS LIKE (cc_task_l Part 2.3), FIRST PASS: a small tiling picture per ground, made here from the
    // palette's colours (tools/palette.ps1) and seamless noise, so it costs no megabytes and the painted shader relights
    // it like everything else. Stone is the bare mat, as the dungeon always was, and has none.
    //
    // KATHLEEN'S TO CHANGE: drop a tiling picture at res://textures/ground/<ground>.png (grass.png, planks.png...) and
    // it is used instead of the made one; or change the colours below. Nothing else needs touching
    public static class GroundTextures
    {
        public const string Folder = "res://textures/ground/";

        // pixels a side of a made picture, and how many squares one picture spans before it repeats
        const int Size = 256;

        public const int SquaresPerPicture = 2;

        // (dark, light) for each ground; what the noise runs between
        static readonly Dictionary<Ground, (Color Dark, Color Light)> Colours = new()
        {
            [Ground.Planks] = (new Color("#3A2A1E"), new Color("#8C6239")),
            [Ground.Grass] = (new Color("#3E5030"), new Color("#6E8248")),
            [Ground.Dirt] = (new Color("#4E3826"), new Color("#8C6A45")),
            [Ground.Cobble] = (new Color("#3B3A42"), new Color("#9D9B96")),
            [Ground.Sand] = (new Color("#B9A27A"), new Color("#DED2B2")),
            [Ground.Snow] = (new Color("#B9C4CE"), new Color("#F1F4F6")),
            [Ground.Water] = (new Color("#2F4256"), new Color("#5E7C95")),
        };

        // deep water and a chasm: a Chasm square, by its ground
        public static Color Deep(Ground ground) => ground == Ground.Water ? new Color("#1E2B3A") : new Color("#16131A");

        static readonly Dictionary<Ground, Texture2D> Made = new();

        public static Texture2D For(Ground ground)
        {
            if (ground == Ground.Stone) return null;

            if (Made.TryGetValue(ground, out Texture2D made)) return made;

            string path = Folder + Core.Words.EnumWords.Name(ground) + ".png";
            Texture2D picture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : Make(ground);

            Made[ground] = picture;
            return picture;
        }

        static Texture2D Make(Ground ground)
        {
            (Color dark, Color light) = Colours[ground];

            var noise = new FastNoiseLite
            {
                Seed = (int)ground * 7919,
                NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
                Frequency = ground is Ground.Sand or Ground.Snow or Ground.Water ? 0.012f : 0.03f,
                FractalOctaves = 3,
            };

            Image grain = noise.GetSeamlessImage(Size, Size);

            Image cells = null;

            if (ground == Ground.Cobble)
            {
                var stones = new FastNoiseLite
                {
                    Seed = 31,
                    NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
                    Frequency = 0.035f,
                    CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance2Sub,
                };
                cells = stones.GetSeamlessImage(Size, Size);
            }

            var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgb8);

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float t = grain.GetPixel(x, y).R;

                    if (ground == Ground.Planks) t = Plank(x, y, t);
                    else if (cells != null) t = cells.GetPixel(x, y).R < 0.08f ? 0.05f : 0.35f + t * 0.65f;
                    else if (ground == Ground.Grass) t = Mathf.Clamp(t * 1.2f - 0.1f, 0f, 1f);

                    image.SetPixel(x, y, dark.Lerp(light, t));
                }

            image.GenerateMipmaps();
            return ImageTexture.CreateFromImage(image);
        }

        // boards a quarter of the picture wide, each its own shade, a dark seam between, and the ends staggered
        static float Plank(int x, int y, float grain)
        {
            const int boards = 4;
            int width = Size / boards;
            int board = y / width;

            int end = (board * 97) % Size;
            bool seam = y % width < 3 || (x + end) % Size < 3;

            float shade = 0.35f + ((board * 37) % 5) * 0.08f;

            return seam ? 0.02f : Mathf.Clamp(shade + (grain - 0.5f) * 0.5f, 0f, 1f);
        }
    }
}
