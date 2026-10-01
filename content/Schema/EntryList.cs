using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Content.Schema
{
    // WHAT EVERY SRD LIST FILE SHARES: { "monsters": [ { "id": ... }, ... ] }. the parse, the one root
    // key, each entry's id and keys, the empty file, and where each problem sits. eight readers had a
    // copy of this and the copies drifted (cc_task_d-seams-and-duplication.md §1); what an entry is
    // stays each reader's own ReadOne, and a check of the whole file (a species' lineages, the
    // consequence pool's two sides) is that reader's `whole`. modelled on TableReader.Tables, which
    // is the same idea for GM tables, where two of one id is refused in the file itself
    public sealed class EntryList<T> where T : class
    {
        readonly string _list;
        readonly string _one;
        readonly IReadOnlyCollection<string> _keys;
        readonly Func<JsonElement, string, List<string>, T> _readOne;
        readonly Action<List<T>, List<string>> _whole;
        readonly bool _bareArray;

        // `list` is the root key ("monsters"), `one` what an entry is ("monster"), `keys` every key an
        // entry takes; `readOne` is given an entry whose id and keys are already checked. a spell file
        // may also be a bare array of spells (`bareArray`)
        public EntryList(string list, string one, IEnumerable<string> keys,
                         Func<JsonElement, string, List<string>, T> readOne,
                         Action<List<T>, List<string>> whole = null, bool bareArray = false)
        {
            _list = list;
            _one = one;
            _keys = new HashSet<string>(keys, StringComparer.Ordinal);
            _readOne = readOne;
            _whole = whole;
            _bareArray = bareArray;
        }

        // the ListReader<T> shape, for Srd.ReadAll: the problems as sentences, each about an entry
        // starting with its id ("goblin: ...")
        public bool TryRead(string text, out IReadOnlyList<T> read, out IReadOnlyList<string> problems)
        {
            var located = new List<ContentProblem>();

            read = Read(text, located);
            problems = located.Select(Said).ToList();

            return located.Count == 0;
        }

        // the same, each problem with where it sits ("monsters.goblin", "monsters[3]" for an entry
        // with no good id, "" for the file) and the entry's id taken off the front of what it says:
        // a campaign's folder names the file on top (Package.ReadFolder). File is left empty
        public List<T> Read(string text, List<ContentProblem> problems)
        {
            var found = new List<T>();
            var file = new List<string>();
            int before = problems.Count;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                problems.Add(new ContentProblem("", "", bad));
                return found;
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                IReadOnlyList<JsonElement> entries;

                if (_bareArray && root.ValueKind == JsonValueKind.Array)
                {
                    entries = root.EnumerateArray().ToList();
                }
                else
                {
                    Keyed.OnlyKnown(root, new[] { _list }, $"the {_list} file", file);
                    entries = root.Items(_list);
                }

                Located(file, "", null, problems);

                for (int i = 0; i < entries.Count; i++)
                {
                    var trouble = new List<string>();
                    string id = entries[i].Text("id");

                    T one = ReadEntry(entries[i], trouble);

                    if (one != null) found.Add(one);

                    if (Json.IsId(id)) Located(trouble, $"{_list}.{id}", id, problems);
                    else Located(trouble, $"{_list}[{i}]", null, problems);
                }

                if (found.Count == 0 && problems.Count == before)
                    problems.Add(new ContentProblem("", "", $"no {_list} in it - the file is " +
                        (_bareArray ? "an array, or " : "") + $"an object with {A(_list)} '{_list}' array"));
            }

            if (_whole != null)
            {
                _whole(found, file);
                Located(file, "", null, problems);
            }

            return found;
        }

        // one entry on its own, its id and keys checked: a statblock's special action is a spell, inline
        public T ReadEntry(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not {A(_one)} {_one} id - lowercase a-z, 0-9 and underscore only");
                return null;
            }

            Keyed.OnlyKnown(entry, _keys, id, problems);

            return _readOne(entry, id, problems);
        }

        // an entry's reader starts most sentences with the entry's id; the where says that now
        static void Located(List<string> said, string where, string id, List<ContentProblem> problems)
        {
            foreach (string what in said)
                problems.Add(new ContentProblem("", where,
                    id != null && what.StartsWith(id + ": ", StringComparison.Ordinal)
                        ? what.Substring(id.Length + 2) : what));

            said.Clear();
        }

        // a located problem as one sentence again: "goblin: ..." for an entry with a good id
        string Said(ContentProblem problem)
        {
            string id = problem.Where.StartsWith(_list + ".", StringComparison.Ordinal)
                ? problem.Where.Substring(_list.Length + 1) : "";

            return id.Length > 0 && !problem.What.StartsWith(id + ".", StringComparison.Ordinal)
                ? $"{id}: {problem.What}"
                : problem.What;
        }

        static string A(string word) => "aeiou".IndexOf(word[0]) >= 0 ? "an" : "a";
    }
}
