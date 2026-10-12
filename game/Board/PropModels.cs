using System.Collections.Generic;
using System.Text.RegularExpressions;
using Content.Maps;
using Godot;

namespace Game.Board
{
    // WHERE A PROP'S MODEL IS: the palette (content/srd/props/props.json) names the pack and the model
    // inside it; tools/pull_props.py copies it to res://models/props/<pack folder>/<file>, and this
    // computes the same folder name the tool does. A prop whose model isn't pulled stands as nothing
    // (the map still plays; the package said so when it loaded).
    //
    // A glTF imports as a scene; an OBJ (the packs with no glTF, cc_task_l Part 3) imports as a mesh, and is stood in a
    // node of its own, so every prop comes out as one Node3D either way (Make)
    public static class PropModels
    {
        const string Folder = "res://models/props/";

        static readonly Dictionary<string, Resource> Loaded = new Dictionary<string, Resource>();

        // kaykit_dungeon_pack_1_1_free - the same rule as folder_of() in tools/pull_props.py, a Google Drive download's
        // stamp ("-20260921T173759Z-1-001") left off
        public static string FolderOf(string pack)
        {
            string name = pack.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase) ? pack[..^4] : pack;
            name = Regex.Replace(name, @"-\d{8}T\d{6}Z-\d+-\d+$", "");
            return Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        }

        public static string PathOf(PropEntry prop) =>
            prop == null ? null : Folder + FolderOf(prop.Pack) + "/" + System.IO.Path.GetFileName(prop.Model);

        // the prop's model, made, or null when it was never pulled
        public static Node3D Make(string id)
        {
            switch (Model(id))
            {
                case PackedScene scene:
                    return scene.Instantiate<Node3D>();

                case Mesh mesh:
                    var holder = new Node3D { Name = id };
                    holder.AddChild(new MeshInstance3D { Name = "Body", Mesh = mesh });
                    return holder;

                default:
                    return null;
            }
        }

        public static bool Has(string id) => Model(id) != null;

        static Resource Model(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (Loaded.TryGetValue(id, out Resource cached)) return cached;

            string path = PathOf(PropCatalogue.Srd().Find(id));
            Resource model = path != null && ResourceLoader.Exists(path) ? GD.Load(path) : null;

            Loaded[id] = model;
            return model;
        }
    }
}
