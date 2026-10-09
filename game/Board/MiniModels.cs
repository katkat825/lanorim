using System.Collections.Generic;
using Godot;

namespace Game.Board
{
    // WHICH MODEL STANDS FOR A STATBLOCK'S MINI (docs/v1_minis_map.md): a mini is a silhouette, not a
    // statblock, so a dozen archetypes front every monster. A monster's data names its mini ("rat",
    // "wolf", "bandit"); this is where that name meets a model on disk and how tall it stands.
    //
    // A mini with no model here falls back to the board's EnemyFigure, as every enemy did before.
    // Models come out of the packs with tools/pull-models.ps1 (or unzip for the FBX-only ones) into
    // game/models/minis/, lower-case. THE HEIGHTS ARE FIRST GUESSES for Kathleen to tune by eye: a
    // humanoid at the board's usual 0.1875, a wolf lower and a rat lower still.
    public static class MiniModels
    {
        const string Folder = "res://models/minis/";

        static readonly Dictionary<string, MiniLook> Known = new Dictionary<string, MiniLook>
        {
            ["goblin"] = new("goblin_male.gltf", 0.15f),
            ["bandit"] = new("pirate_male.gltf", 0.1875f),
            ["guard"] = new("knight_male.gltf", 0.1875f),
            ["priest"] = new("knight_male.gltf", 0.1875f),
            ["zombie"] = new("zombie_male.gltf", 0.1875f),
            ["wolf"] = new("wolf.gltf", 0.11f),
            ["rat"] = new("rat.tscn", 0.06f),
            ["spider"] = new("spider.tscn", 0.08f),
        };

        static readonly Dictionary<string, PackedScene> Loaded = new Dictionary<string, PackedScene>();

        public static IEnumerable<string> Names => Known.Keys;

        // the model and how it stands, or null when the mini has no model yet
        public static (PackedScene Model, MiniLook Look)? For(string mini) =>
            string.IsNullOrEmpty(mini) || !Known.TryGetValue(mini, out MiniLook look) ? null : Of(look);

        // any look's model, loaded once (HeroMinis' too); null when its file isn't there
        public static (PackedScene Model, MiniLook Look)? Of(MiniLook look)
        {
            if (look == null) return null;

            if (!Loaded.TryGetValue(look.File, out PackedScene scene))
            {
                string path = Folder + look.File;
                scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
                Loaded[look.File] = scene;
            }

            return scene == null ? null : (scene, look);
        }
    }
}
