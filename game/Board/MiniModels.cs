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

        static readonly Dictionary<string, (string File, float Height)> Known =
            new Dictionary<string, (string, float)>
            {
                ["goblin"] = ("goblin_male.gltf", 0.15f),
                ["bandit"] = ("pirate_male.gltf", 0.1875f),
                ["guard"] = ("knight_male.gltf", 0.1875f),
                ["priest"] = ("knight_male.gltf", 0.1875f),
                ["zombie"] = ("zombie_male.gltf", 0.1875f),
                ["wolf"] = ("wolf.gltf", 0.11f),
                ["rat"] = ("rat.tscn", 0.06f),
                ["spider"] = ("spider.tscn", 0.08f),
            };

        static readonly Dictionary<string, PackedScene> Loaded = new Dictionary<string, PackedScene>();

        public static IEnumerable<string> Names => Known.Keys;

        // the model and its height, or null when the mini has no model yet
        public static (PackedScene Model, float Height)? For(string mini)
        {
            if (string.IsNullOrEmpty(mini) || !Known.TryGetValue(mini, out var known)) return null;

            if (!Loaded.TryGetValue(known.File, out PackedScene scene))
            {
                string path = Folder + known.File;
                scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
                Loaded[known.File] = scene;
            }

            return scene == null ? null : (scene, known.Height);
        }
    }
}
