using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Game.Access;
using Game.Audio;
using Game.Tray;

namespace Game.Localization
{
    // EVERY KEY THE WHOLE GAME EMITS, engine and Godot layer together.
    //
    // content/Schema/EngineKeys.cs has the half that never touches Godot - abilities, skills,
    // spells, items, monsters. This adds the half that does, and it exists because those keys
    // cannot live in EngineKeys: a caption names an audio folder and a tray skin is a Resource,
    // and core and content are not allowed to know what either of those is.
    //
    // `sim locale` scaffolds the first half. The second is scaffolded by the same tool through
    // this list, which is why the list is derived from the enums and never written out twice.
    public static class GameKeys
    {
        public static IEnumerable<string> All()
        {
            foreach (string key in EngineKeys.Sorted()) yield return key;

            foreach (string key in Godot()) yield return key;
        }

        // just the Godot layer's own
        public static IEnumerable<string> Godot()
        {
            // the sounds, in words, for a player who cannot hear them
            foreach (string key in Sounds.Keys()) yield return key;

            // what every key on the keyboard does, said in words on the page where you rebind it
            foreach (string key in Acts.Keys()) yield return key;

            // the trays you can be given, named
            foreach (string key in TrayNames()) yield return key;
        }

        // a skin's name is a key in its own .tres, so the folder is the list
        public static IEnumerable<string> TrayNames()
        {
            foreach (string name in TraySkin.All())
            {
                TraySkin skin = TraySkin.Load(name);

                if (skin != null && !string.IsNullOrWhiteSpace(skin.NameKey))
                    yield return skin.NameKey;
            }
        }
    }
}
