using System.Collections.Generic;
using Godot;

namespace Game
{
    // THE FILES IN A res:// FOLDER AS THE GAME SEES THEM, in source or in an export. an export has
    // x.tres.remap for x.tres and x.wav.remap for x.wav, and source has x.wav.import beside x.wav:
    // all of them fold to x.tres and x.wav. subfolders are not listed, which keeps the slicer's
    // _review/ quarantine out of the game. the tray skins and the impact pools each listed their
    // own folder (cc_task_dedupe-methods.md #11)
    public static class ResFolder
    {
        // `who` names the caller in the error when the folder won't open
        public static SortedSet<string> Files(string folder, string who)
        {
            var names = new SortedSet<string>();

            using DirAccess dir = DirAccess.Open(folder);

            if (dir == null)
            {
                GD.PushError($"{who}: cannot open {folder} - {DirAccess.GetOpenError()}");
                return names;
            }

            foreach (string entry in dir.GetFiles())
                names.Add(entry.EndsWith(".import") || entry.EndsWith(".remap") ? entry.GetBaseName() : entry);

            return names;
        }
    }
}
