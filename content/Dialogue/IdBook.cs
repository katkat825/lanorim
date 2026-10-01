using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;

namespace Content.Dialogue
{
    // WHAT A CAMPAIGN'S BOOK OF THINGS BY ID SHARES: the beats/ and hints/ folders each keep their entries by id,
    // with the problems their files had, and answer the same questions of them (cc_task_d-seams-and-duplication.md
    // §9). ListFile reads the files; each book reads its own entry. not Catalogue: a book is a campaign author's,
    // its problems say which file and which field, and it fills as it reads
    public abstract class IdBook<T> where T : class
    {
        readonly Dictionary<string, T> _byId = new Dictionary<string, T>(StringComparer.Ordinal);

        readonly string _noun;

        // `noun` counts them: "beats"
        protected IdBook(string campaign, string noun)
        {
            Campaign = campaign ?? "";
            _noun = noun;
        }

        public string Campaign { get; }

        public IReadOnlyCollection<string> Ids => _byId.Keys;

        public IReadOnlyList<ContentProblem> Problems => Said;

        public bool Has(string id) => id != null && _byId.ContainsKey(id);

        public T Of(string id) => id == null ? null : _byId.GetValueOrDefault(id);

        // in id order, so a book reads the same every run
        public IEnumerable<T> All => _byId.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _byId[i]);

        public int Count => _byId.Count;

        // what was wrong with its files, as each book's reader finds it
        protected List<ContentProblem> Said { get; } = new List<ContentProblem>();

        protected void Add(string id, T thing) => _byId[id] = thing;

        // the entry's id, or null when it has none or it isn't one - said with the book's own `why`, which tells
        // the author what the id is for
        protected string IdOf(JsonElement entry, string file, string where, string why)
        {
            if (entry.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String &&
                ContentId.IsLocal(id.GetString()))
                return id.GetString();

            Said.Add(new ContentProblem(file, $"{where}.id", why));
            return null;
        }

        public override string ToString() =>
            $"{_byId.Count} {_noun}" + (Said.Count > 0 ? $", {Said.Count} problems" : "");
    }
}
