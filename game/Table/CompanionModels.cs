using System.Collections.Generic;
using Godot;

namespace Game.Table
{
    // WHICH MODEL IS WHICH COMPANION. A class names its companion (content/srd/classes, "companion"); this is where
    // that name meets a model in game/models/companions/, pulled with tools/pull-models.ps1.
    //
    // SINCE 2026-10-08 (cc_task_g, cc_task_h Part 1) Quaternius's Ultimate Monsters (CC0), Kathleen's picks:
    //
    //   bonded_wolf            (barbarian, fighter)  Dragon_Evolved made blue-green (three on offer, teal in)
    //   gossiping_raven        (rogue)               Pigeon in charcoal
    //   bound_imp              (mage)                Squidle in a darker red
    //   saints_fragment        (cleric, paladin)     Ghost made white
    //   borrowed_shape_spirit  (druid)               Mushnub_Evolved, one body
    //
    // A PICK IS ONE LINE in ByCompanion: the name of a look below. A look is the model, its clips, how it hovers
    // and its colours. The dragon's blue-greens are looks of their own until Kathleen picks one; then the other
    // two go.
    public static class CompanionModels
    {
        const string Folder = "res://models/companions/";

        // a flyer hangs this far off the table at its lowest, and rises and falls this much
        const float Hover = 0.035f;
        const float Bob = 0.008f;

        static readonly Dictionary<string, string> ByCompanion = new Dictionary<string, string>
        {
            ["bonded_wolf"] = "dragon_teal",
            ["gossiping_raven"] = "pigeon_charcoal",
            ["bound_imp"] = "squidle_crimson",
            ["saints_fragment"] = "ghost_white",
            ["borrowed_shape_spirit"] = "mushnub_evolved",
        };

        // the atlas cells: Dragon_Evolved's row 24 (2 its orange body, 3 dark purple, 1 and 4 grey horns and claws,
        // 0 eyes), Pigeon's row 19 (1 grey body, 2 purple wings and neck, 3 orange beak and feet, 0 eyes), Squidle's
        // row 29 (2 its pink), Ghost's row 25 (2 its purple, 1 grey, 0 eyes and mouth)
        static readonly Dictionary<string, CompanionLook> Looks = new Dictionary<string, CompanionLook>
        {
            ["dragon_teal"] = new("Dragon_Evolved.gltf", CompanionClips.Flying, Hover, Bob, "2,24 #0d6466"),
            ["dragon_turquoise"] = new("Dragon_Evolved.gltf", CompanionClips.Flying, Hover, Bob, "2,24 #1fa6c4"),
            ["dragon_mint"] = new("Dragon_Evolved.gltf", CompanionClips.Flying, Hover, Bob, "2,24 #5fc98e"),
            ["pigeon_charcoal"] = new("Pigeon.gltf", CompanionClips.Flying, Hover, Bob, "1,19 #45454c; 2,19 #222226"),
            ["squidle_crimson"] = new("Squidle.gltf", CompanionClips.Flying, Hover, Bob, "2,29 #8a1b2b"),
            ["ghost_white"] = new("Ghost.gltf", CompanionClips.Flying, Hover, Bob, "2,25 #f1efe9"),
            ["mushnub_evolved"] = new("Mushnub_Evolved.gltf", CompanionClips.Blob),
        };

        public static IEnumerable<string> Names => ByCompanion.Keys;

        public static IEnumerable<string> LookNames => Looks.Keys;

        // the look for a companion, or for a look by its own name (`--companion squidle_crimson` on the table);
        // null when it has none (or the class has no companion)
        public static CompanionLook For(string companion)
        {
            if (string.IsNullOrEmpty(companion)) return null;

            string name = ByCompanion.TryGetValue(companion, out string picked) ? picked : companion;

            return Looks.TryGetValue(name, out CompanionLook look) ? look : null;
        }

        // its model, or null when the file isn't there
        public static PackedScene Model(CompanionLook look)
        {
            if (look == null) return null;

            string path = Folder + look.File;
            return ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
        }
    }
}
