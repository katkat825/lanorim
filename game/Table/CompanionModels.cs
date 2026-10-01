using System.Collections.Generic;
using Godot;

namespace Game.Table
{
    // WHICH ANIMAL IS WHICH COMPANION (cc_task_ui-issues-10-01.md 4; Kathleen, 2026-10-01: "Quaternius animals,
    // not KayKit skeletons"). A class names its companion (content/srd/classes, "companion"); this is where that
    // name meets a model from Quaternius's Ultimate Animated Animals in game/models/companions/, pulled with
    // tools/pull-models.ps1. Every one carries its own clips. PICKS for Kathleen:
    //
    //   bonded_wolf            (barbarian, fighter)  Wolf         - the direct match
    //   gossiping_raven        (rogue)               Fox          - stand-in: no bird in any Quaternius pack here
    //   bound_imp              (mage)                ShibaInu     - stand-in: the Bestiary's Imp has no clips
    //   saints_fragment        (cleric, paladin)     Horse_White  - stand-in: a pale, holy-looking beast
    //   borrowed_shape_spirit  (druid)               Stag         - stand-in: the druid's spirit of the wild
    public static class CompanionModels
    {
        const string Folder = "res://models/companions/";

        static readonly Dictionary<string, string> Known = new Dictionary<string, string>
        {
            ["bonded_wolf"] = "Wolf.gltf",
            ["gossiping_raven"] = "Fox.gltf",
            ["bound_imp"] = "ShibaInu.gltf",
            ["saints_fragment"] = "Horse_White.gltf",
            ["borrowed_shape_spirit"] = "Stag.gltf",
        };

        public static IEnumerable<string> Names => Known.Keys;

        // the model for a companion, or null when it has none (or the class has no companion)
        public static PackedScene For(string companion)
        {
            if (string.IsNullOrEmpty(companion) || !Known.TryGetValue(companion, out string file)) return null;

            string path = Folder + file;
            return ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
        }
    }
}
