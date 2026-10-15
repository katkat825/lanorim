using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Content.Schema
{
    // EVERY FILE OF ONE KIND IN ONE FOLDER, READ: a pack's monsters/ and maps/, a campaign's beats/,
    // hints/, barks/ and dialogue/, the saves. in name order, so a load is the same every run. a file
    // that can't be read is a problem said with its name, and the rest are still read
    // (cc_task_dedupe-methods.md #4: this was six loops)
    public static class PackFolder
    {
        // A DOT-FOLDER IS THE WORKSHOP'S, NOT CONTENT (cc_task_m Part 2.3, Part 6): My campaigns' .deleted/ (Recently
        // deleted) and a campaign's .draft/ (its autosave). A campaign root's walk and a pack's own folders skip them
        public static bool IsHidden(string folder) => Path.GetFileName((folder ?? "").TrimEnd('/', '\\')).StartsWith('.');

        // THE FOLDERS IN A ROOT, the campaigns in a campaign root: in name order, so the walk is the same every run, and
        // with no dot-folder among them. none when there's no root
        public static IReadOnlyList<string> Folders(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return Array.Empty<string>();

            return Directory.EnumerateDirectories(root).Where(f => !IsHidden(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();
        }

        // `shownAs` is how a problem names the folder: "monsters" makes "monsters/goblin.json", and
        // null leaves the file's own name. no folder, or none there, is no files
        public static IEnumerable<(string File, string Path, string Text)> Read(
            string folder, string extension, string shownAs, List<ContentProblem> problems)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) yield break;

            foreach (string path in Directory.EnumerateFiles(folder, "*" + extension)
                                             .OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                string file = shownAs == null ? Path.GetFileName(path) : shownAs + "/" + Path.GetFileName(path);
                string text;

                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
                {
                    problems.Add(new ContentProblem(file, "", "could not be read - " + could.Message));
                    continue;
                }

                yield return (file, path, text);
            }
        }
    }
}
