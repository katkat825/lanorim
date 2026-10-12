using System.Collections.Generic;
using Content.Maps;
using Godot;

namespace Game.Board
{
    // WHAT EACH WALL STYLE IS DRAWN WITH (cc_task_l Part 3.4): res://models/walls/<style>/wall.tscn for a wall and
    // door.tscn for a door in it, each a small scene over the props' own pulled models (the Medieval Village MegaKit's
    // timber, plaster and fieldstone walls and its door, its wooden fence, the Ruins pack's hedge), so there is one copy
    // of every model. Stone is the dungeon's, on the Board's own WallModel and DoorwayModel, as it always was. A door
    // scene's leaf is the child whose name ends in "_door", which BoardTiles hangs on a hinge, as it does the dungeon's.
    //
    // Kathleen's to lay out: these scenes are first passes, fitted by BoardTiles to a line and stood at their height
    public static class WallLooks
    {
        const string Folder = "res://models/walls/";

        static readonly Dictionary<(WallStyle, string), PackedScene> Loaded = new();

        public static PackedScene Wall(WallStyle style) => Scene(style, "wall");

        public static PackedScene Door(WallStyle style) => Scene(style, "door");

        // how tall a style stands, as a share of the dungeon's walls (Board.WallTall): a fence and a hedge come to a
        // mini's waist, so they never need to come down for the camera
        public static float Height(WallStyle style) => style switch
        {
            WallStyle.Fence => 0.4f,
            WallStyle.Hedge => 0.55f,
            _ => 1f,
        };

        // a low one is never cut away
        public static bool Low(WallStyle style) => Height(style) < 1f;

        static PackedScene Scene(WallStyle style, string piece)
        {
            if (style == WallStyle.Stone) return null;

            if (Loaded.TryGetValue((style, piece), out PackedScene cached)) return cached;

            string path = $"{Folder}{Core.Words.EnumWords.Name(style)}/{piece}.tscn";
            PackedScene scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;

            return Loaded[(style, piece)] = scene;
        }
    }
}
