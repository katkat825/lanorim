using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Schema;
using Core.Words;

namespace Content.Dialogue
{
    // A FILE THAT IS ONE NAMED LIST OF OBJECTS: { "beats": [ ... ] }, { "hints": [ ... ] }. the file
    // holds that list and nothing else, and every entry is an object with only its own fields; each
    // entry that is one is handed on with where it sits, "beats[3]", for the book to read the rest
    // (cc_task_dedupe-methods.md #4: BeatBook and HintBook each had this)
    static class ListFile
    {
        // every file in a book's folder (PackFolder), each read as one list; `each` gets the entry,
        // the file it came from and where it sits in it
        public static void ReadFolder(string folder, string extension, string list, string shape, string entry,
                                      string[] fields, List<ContentProblem> problems,
                                      Action<JsonElement, string, string> each)
        {
            foreach ((string file, string _, string text) in PackFolder.Read(folder, extension, null, problems))
                Read(text, file, list, shape, entry, fields, problems, (one, where) => each(one, file, where));
        }

        // `shape` is the problem when the list is missing, with an example; `entry` names one, "a beat"
        public static void Read(string json, string file, string list, string shape, string entry,
                                string[] fields, List<ContentProblem> problems,
                                Action<JsonElement, string> each)
        {
            JsonDocument document = PackJson.ReadObject(json, file, $"a {list} file", out ContentProblem bad);

            if (document == null)
            {
                problems.Add(bad);
                return;
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                foreach (JsonProperty property in root.EnumerateObject())
                    if (property.Name != list)
                        problems.Add(new ContentProblem(
                            file, property.Name,
                            $"a {list} file has no '{property.Name}' - it has {list}, and nothing else"));

                if (!root.TryGetProperty(list, out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
                {
                    problems.Add(new ContentProblem(file, list, shape));
                    return;
                }

                int at = 0;

                foreach (JsonElement one in entries.EnumerateArray())
                {
                    string where = $"{list}[{at++}]";

                    if (one.ValueKind != JsonValueKind.Object)
                    {
                        problems.Add(new ContentProblem(
                            file, where, $"{entry} is an object and this is a {EnumWords.Name(one.ValueKind)}"));
                        continue;
                    }

                    foreach (JsonProperty property in one.EnumerateObject())
                        if (Array.IndexOf(fields, property.Name) < 0)
                            problems.Add(new ContentProblem(
                                file, $"{where}.{property.Name}",
                                $"{entry} has no '{property.Name}' - it has {Vocabulary.Offer(fields)}"));

                    each(one, where);
                }
            }
        }
    }
}
