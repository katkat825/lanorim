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
            ["bandit"] = new("pirate_male.gltf", 0.1875f, Skin: FigureSkins.For("bandit")),
            ["guard"] = new("knight_male.gltf", 0.1875f, Skin: FigureSkins.For("guard")),
            ["priest"] = new("knight_male.gltf", 0.1875f, Skin: FigureSkins.For("priest")),
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

        // THE HUMAN MINIS' SKIN (Kathleen, 2026-10-23): the Ultimate Animated Character Pack paints it near-black; the
        // bandit, the guard and the priest wear a natural tone instead (FigureSkins), and in a fight each creature its own,
        // by its id, so two bandits needn't match. The goblin's and the zombie's own greens are kept
        public static (PackedScene Model, MiniLook Look)? For(string mini, string who) =>
            For(mini) is { } figure && figure.Look.Skin != null
                ? (figure.Model, figure.Look with { Skin = FigureSkins.For(who) })
                : For(mini);

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
