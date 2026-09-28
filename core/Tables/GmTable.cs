using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Core.Tables
{
    // WHAT EVERY GM TABLE IS: an id, whether it's rolled in the open, weighted entries, and the
    // narrator's line for each entry that speaks. the encounter table and the loot table each had
    // their own copy of this (cc_task_dedupe-methods.md #5); what an entry holds, and what else a
    // table knows (a trigger, the tables it rolls), stays theirs
    public abstract class GmTable<T> where T : class, ITableEntry
    {
        readonly string _aspect;

        // `aspect` keeps two kinds of table of one name from sharing a key: "line", "loot"
        protected GmTable(string id, IEnumerable<T> entries, Visibility visibility, string aspect)
        {
            Id = id ?? "";
            Visibility = visibility;
            Entries = (entries ?? Enumerable.Empty<T>()).Where(e => e != null).ToList();
            _aspect = aspect;
        }

        public string Id { get; }

        public Visibility Visibility { get; }

        public IReadOnlyList<T> Entries { get; }

        public int TotalWeight => Entries.TotalWeight();

        // what the narrator says when an entry comes up. derived from the ids so there is no free
        // text in the table to get wrong; an entry that doesn't speak has no line to owe the locale
        public string LineKey(T entry) =>
            entry == null || !entry.Speaks ? "" : KeyConventions.Key(KeyConventions.EncounterNs, Id, _aspect, entry.Id);

        public IEnumerable<string> Keys() => Entries.Where(e => e.Speaks).Select(LineKey);

        protected string RolledInTheOpen => Visibility == Visibility.Shown ? ", rolled in the open" : "";
    }
}
