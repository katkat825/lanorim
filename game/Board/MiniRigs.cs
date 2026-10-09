using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Board
{
    // CLIPS FOR A MODEL THAT CARRIES NONE (task G). The KayKit Adventurers characters have no animations of their
    // own: the pack keeps them in separate files on one shared skeleton, Rig_Medium, and KayKit Character Animations
    // 1.1 has seven more of those files. Every Rig_Medium track is addressed "Rig_Medium/Skeleton3D:<bone>", which
    // is the same path in every character on that rig, so a library lifted out of one file plays on any of them.
    //
    // Each file is one library, named after it ("CombatMelee"), so a clip is "CombatMelee/Melee_2H_Idle" and found
    // by its short name (ClipMatch). The files are loaded once and their libraries shared between every mini.
    public static class MiniRigs
    {
        const string Folder = "res://models/minis/kaykit/";

        static readonly Dictionary<string, string[]> Files = new Dictionary<string, string[]>
        {
            ["Rig_Medium"] = new[]
            {
                "Rig_Medium_General.glb", "Rig_Medium_MovementBasic.glb", "Rig_Medium_MovementAdvanced.glb",
                "Rig_Medium_CombatMelee.glb", "Rig_Medium_CombatRanged.glb", "Rig_Medium_Simulation.glb",
                "Rig_Medium_Special.glb", "Rig_Medium_Tools.glb",
            },
        };

        static readonly Dictionary<string, List<(string Name, AnimationLibrary Library)>> Loaded =
            new Dictionary<string, List<(string, AnimationLibrary)>>();

        // gives the model the rig's libraries, on its own AnimationPlayer or a new one; returns that player, or null
        // when there is no such rig
        public static AnimationPlayer Give(Node model, string rig)
        {
            List<(string Name, AnimationLibrary Library)> libraries = Libraries(rig);

            if (libraries.Count == 0) return null;

            AnimationPlayer player = model.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();

            if (player == null)
            {
                // root_node ".." is the model, where the rig's tracks start from
                player = new AnimationPlayer { Name = "AnimationPlayer" };
                model.AddChild(player);
            }

            foreach ((string name, AnimationLibrary library) in libraries)
                if (!player.HasAnimationLibrary(name)) player.AddAnimationLibrary(name, library);

            return player;
        }

        static List<(string Name, AnimationLibrary Library)> Libraries(string rig)
        {
            if (string.IsNullOrEmpty(rig)) return new List<(string, AnimationLibrary)>();

            if (Loaded.TryGetValue(rig, out var known)) return known;

            var libraries = new List<(string, AnimationLibrary)>();

            foreach (string file in Files.TryGetValue(rig, out string[] files) ? files : System.Array.Empty<string>())
            {
                string path = Folder + file;

                if (!ResourceLoader.Exists(path))
                {
                    GD.PushWarning($"mini: rig {rig} has no '{path}' - its clips are missing");
                    continue;
                }

                Node holder = GD.Load<PackedScene>(path).Instantiate();
                AnimationPlayer player = holder.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();

                if (player != null && player.HasAnimationLibrary(""))
                    libraries.Add((file.GetBaseName().TrimPrefix(rig + "_"), player.GetAnimationLibrary("")));

                holder.Free();
            }

            if (libraries.Count == 0) GD.PushWarning($"mini: no clips for rig '{rig}'");

            Loaded[rig] = libraries;
            return libraries;
        }
    }
}
