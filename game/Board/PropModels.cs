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
    public static class PropModels
    {
        const string Folder = "res://models/props/";

        static readonly Dictionary<string, PackedScene> Loaded = new Dictionary<string, PackedScene>();

        // kaykit_dungeon_pack_1_1_free - the same rule as folder_of() in tools/pull_props.py
        public static string FolderOf(string pack)
        {
            string name = pack.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase) ? pack[..^4] : pack;
            return Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        }

        public static string PathOf(PropEntry prop) =>
            prop == null ? null : Folder + FolderOf(prop.Pack) + "/" + System.IO.Path.GetFileName(prop.Model);

        public static PackedScene For(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (Loaded.TryGetValue(id, out PackedScene cached)) return cached;

            string path = PathOf(PropCatalogue.Srd().Find(id));
            PackedScene scene = path != null && ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;

            Loaded[id] = scene;
            return scene;
        }
    }
}
