using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;

namespace Content.Saves
{
    // THE SAVES ON DISK, LISTED.
    //
    // A BROKEN SAVE IS ONE BROKEN FILE, never a shelf that will not open. Same isolation boundary a
    // campaign folder gets: one bad file fails alone and named, and everything beside it still
    // lists. A save whose label is smudged still goes on the shelf, because SaveReader reads as far
    // as it can and a partly-read afternoon is better than a refused one.
    //
    // LIFTED from the old build's SaveShelf. What went with the room is the framing - there, a save
    // WAS a box on a diegetic shelf and taking one down was loading it. lanorim has no room, so
    // this is a list of files and the UI decides what a save looks like.
    //
    // Godot-free, so the shelf can be listed and checked headless.
    public sealed class SaveShelf
    {
        public const string Extension = ".json";

        readonly List<Saved> _saves = new List<Saved>();

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        SaveShelf(string folder) => Folder = folder ?? "";

        public string Folder { get; }

        public IReadOnlyList<Saved> Saves => _saves;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public int Count => _saves.Count;

        public Saved Of(string file) =>
            file == null ? null : _saves.FirstOrDefault(s => s.File == file);

        // every campaign with a save on this shelf
        public IEnumerable<string> Campaigns =>
            _saves.Select(s => s.Game.Campaign)
                  .Where(c => !string.IsNullOrEmpty(c))
                  .Distinct(StringComparer.Ordinal)
                  .OrderBy(c => c, StringComparer.Ordinal);

        public sealed class Saved
        {
            public Saved(string path, string file, SaveGame game, DateTime written, int problems)
            {
                Path = path ?? "";
                File = file ?? "";
                Game = game;
                Written = written;
                Problems = problems;
            }

            public string Path { get; }

            public string File { get; }

            public SaveGame Game { get; }

            public DateTime Written { get; }

            // how many things the reader did not understand; 0 is a clean read
            public int Problems { get; }

            public bool Clean => Problems == 0;

            public override string ToString() =>
                $"{File}: {Game}" + (Clean ? "" : $" ({Problems} not understood)");
        }

        public static SaveShelf Read(string folder)
        {
            var shelf = new SaveShelf(folder);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return shelf;

            foreach (string path in Directory.EnumerateFiles(folder, "*" + Extension)
                                             .OrderBy(f => f, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);

                string text;
                DateTime written;

                try
                {
                    text = File.ReadAllText(path);
                    written = File.GetLastWriteTimeUtc(path);
                }
                catch (IOException bad)
                {
                    shelf._problems.Add(new ContentProblem(name, "",
                        "could not be read - " + bad.Message));
                    continue;
                }

                Read<SaveGame> read = SaveReader.Parse(text, name);

                shelf._problems.AddRange(read.Problems);

                // Partial is the NORMAL case for a save, so Any is the question, not Ok
                if (read.Any)
                    shelf._saves.Add(new Saved(path, name, read.Value, written,
                                               read.Problems.Count));
            }

            // newest first, which is the order a person reaches for them in
            shelf._saves.Sort((a, b) => b.Written.CompareTo(a.Written));

            return shelf;
        }

        public static string Write(string folder, string file, SaveGame save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, file.EndsWith(Extension, StringComparison.Ordinal)
                ? file : file + Extension);

            File.WriteAllText(path, SaveWriter.Write(save));

            return path;
        }

        public override string ToString() =>
            $"{_saves.Count} saves in {Folder}" +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
