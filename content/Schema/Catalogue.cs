using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Schema
{
    // A CATALOGUE BY ID: every spell, item, monster or Wild Shape form the game knows, with the
    // problems its files had (SpellBook, ItemShelf, Bestiary, FormShelf; Listing for the classes,
    // species and backgrounds, in their files' order). built once, usually from
    // Srd.ReadAll, and handed round read-only. what it keeps, finds and counts is the same for all of
    // them; each adds its own order and its own questions (cc_task_dedupe-methods.md #3). not the
    // campaign Shelf, which is what packs are installed
    public abstract class Catalogue<T> where T : class
    {
        readonly Dictionary<string, T> _byId = new Dictionary<string, T>(StringComparer.Ordinal);
        readonly Func<T, IEnumerable<string>> _keys;

        protected Catalogue(IEnumerable<T> things, Func<T, string> id, Func<T, IEnumerable<string>> keys,
                            IEnumerable<string> problems)
        {
            var said = (problems ?? Enumerable.Empty<string>()).ToList();

            // a second of one id is a problem, never a quiet overwrite: the first stays
            foreach (T thing in things ?? Enumerable.Empty<T>())
                if (thing != null && !_byId.TryAdd(id(thing), thing))
                    said.Add($"'{id(thing)}' is defined twice - the first one is kept");

            _keys = keys;
            Problems = said;
        }

        public IReadOnlyList<string> Problems { get; }

        public bool Sound => Problems.Count == 0;

        public int Count => _byId.Count;

        // everything on it, in the shelf's own order
        public abstract IEnumerable<T> All { get; }

        public T Find(string id) =>
            id != null && _byId.TryGetValue(id, out T thing) ? thing : null;

        public bool Has(string id) => Find(id) != null;

        // every localization key the things on it use
        public IEnumerable<string> Keys() => All.SelectMany(_keys);

        // what is on it, in no order: for All to sort
        protected IEnumerable<T> Stock => _byId.Values;

        // what is on it and more, for a shelf's With: a campaign's own on top of the SRD's
        protected IEnumerable<T> Plus(IEnumerable<T> more) => _byId.Values.Concat(more ?? Enumerable.Empty<T>());

        // the SRD's own: everything in one folder of it, and what was wrong with the files
        protected static List<T> ReadSrd(string folder, ListReader<T> reader, out List<string> problems)
        {
            problems = new List<string>();

            return Srd.ReadAll(folder, reader, problems);
        }

        // "23 monsters, 2 problems"
        protected string Counted(string noun) =>
            $"{Count} {noun}" + (Problems.Count > 0 ? $", {Problems.Count} problems" : "");
    }
}
