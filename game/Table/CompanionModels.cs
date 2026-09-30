using System.Collections.Generic;
using Godot;

namespace Game.Table
{
    // WHICH SKELETON IS WHICH COMPANION (docs/updated_decisions.md: "companions will all be variations on
    // the kaykit skeleton pack. this pack will not be used anywhere else in the game"). A class names its
    // companion (content/srd/classes, "companion"); this is where that name meets a KayKit Skeletons 1.1
    // model in game/models/companions/, pulled with tools/pull-models.ps1. FIRST PICKS for Kathleen:
    //
    //   bonded_wolf            (barbarian, fighter)  Skeleton_Warrior - the one who fights beside you
    //   gossiping_raven        (rogue)               Skeleton_Rogue   - hooded, and it talks
    //   bound_imp              (mage)                Skeleton_Mage    - the bound thing in the hat
    //   saints_fragment        (cleric, paladin)     Skeleton_Minion  - a small, plain relic of someone
    //   borrowed_shape_spirit  (druid)               Skeleton_Minion  - until it has its own variation
    //
    // "Variations" is the plan: a recolour or a prop each (the pack's weapons and shields), which is
    // Kathleen's in Blender or the painted shader. Two share the Minion until then.
    public static class CompanionModels
    {
        const string Folder = "res://models/companions/";

        // the animations every skeleton shares (Rig_Medium): idles, a gesture, lying down, a hop
        public static readonly string[] Rigs = { Folder + "Rig_Medium_General.glb", Folder + "Rig_Medium_MovementBasic.glb" };

        static readonly Dictionary<string, string> Known = new Dictionary<string, string>
        {
            ["bonded_wolf"] = "Skeleton_Warrior.glb",
            ["gossiping_raven"] = "Skeleton_Rogue.glb",
            ["bound_imp"] = "Skeleton_Mage.glb",
            ["saints_fragment"] = "Skeleton_Minion.glb",
            ["borrowed_shape_spirit"] = "Skeleton_Minion.glb",
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
