using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;

namespace Content.Items
{
    // every item the game knows, by id.
    public sealed class ItemShelf
    {
        readonly Dictionary<string, Item> _byId;

        public ItemShelf(IEnumerable<Item> items, IEnumerable<string> problems = null)
        {
            _byId = new Dictionary<string, Item>(StringComparer.Ordinal);

            foreach (Item item in items ?? Enumerable.Empty<Item>())
                if (item != null)
                    _byId[item.Id] = item;

            Problems = (problems ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> Problems { get; }

        public bool Sound => Problems.Count == 0;

        public int Count => _byId.Count;

        public IEnumerable<Item> All => _byId.Values.OrderBy(i => i.Id, StringComparer.Ordinal);

        public Item Find(string id) =>
            id != null && _byId.TryGetValue(id, out Item item) ? item : null;

        public bool Has(string id) => Find(id) != null;

        public IEnumerable<Item> Of(ItemKind kind) => All.Where(i => i.Kind == kind);

        // what a shop may show this hero, and what a loot table may drop for them
        public IEnumerable<Item> For(string className, int level) =>
            All.Where(i => i.UsableBy(className, level));

        public ItemShelf With(IEnumerable<Item> more) =>
            new ItemShelf(_byId.Values.Concat(more ?? Enumerable.Empty<Item>()), Problems);

        public IEnumerable<string> Keys() => All.SelectMany(i => i.Keys());

        public static ItemShelf Srd()
        {
            var items = new List<Item>();
            var problems = new List<string>();

            foreach ((string path, string text) in Schema.Srd.ReadFolder("items"))
            {
                ItemReader.TryRead(text, out IReadOnlyList<Item> read,
                                   out IReadOnlyList<string> trouble);

                items.AddRange(read);
                problems.AddRange(trouble.Select(t => $"{path}: {t}"));
            }

            return new ItemShelf(items, problems);
        }

        public override string ToString() =>
            $"{Count} items" + (Problems.Count > 0 ? $", {Problems.Count} problems" : "");
    }
}
