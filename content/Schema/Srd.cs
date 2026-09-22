using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Content.Schema
{
    // the SRD reference data, read out of the Content assembly rather than off disk. the sim, the
    // tests and Godot all get the same bytes with no path to resolve and nothing to forget in an
    // export filter; campaign packs are the opposite and stay loose on disk so an author can edit
    // them in a text editor.
    public static class Srd
    {
        const string Prefix = "Content.srd.";

        static readonly Assembly Assembly = typeof(Srd).Assembly;

        public static IEnumerable<string> Names =>
            Assembly.GetManifestResourceNames()
                    .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
                    .OrderBy(n => n, StringComparer.Ordinal);

        // "spells/level_1.json" -> the embedded name the compiler gave it
        public static string NameOf(string path) =>
            Prefix + (path ?? "").Replace('/', '.').Replace('\\', '.');

        public static bool Has(string path) => Names.Contains(NameOf(path));

        public static string Read(string path)
        {
            using Stream stream = Assembly.GetManifestResourceStream(NameOf(path));

            if (stream == null)
                throw new FileNotFoundException(
                    $"no SRD file at '{path}'. what is there: " + string.Join(", ", Names));

            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }

        // every file in one folder, in name order, so a load is the same every run
        public static IEnumerable<(string Path, string Text)> ReadFolder(string folder)
        {
            string prefix = Prefix + (folder ?? "").Trim('/').Replace('/', '.') + ".";

            foreach (string name in Names.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)))
            {
                using Stream stream = Assembly.GetManifestResourceStream(name);

                if (stream == null) continue;

                using var reader = new StreamReader(stream);

                yield return (name.Substring(Prefix.Length), reader.ReadToEnd());
            }
        }
    }
}
