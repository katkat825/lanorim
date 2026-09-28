using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;

namespace Content.Items
{
    // every item the game knows, by id.
    public sealed class ItemShelf : Catalogue<Item>
    {
        public ItemShelf(IEnumerable<Item> items, IEnumerable<string> problems = null)
            : base(items, i => i.Id, i => i.Keys(), problems)
        {
        }

        public override IEnumerable<Item> All => Stock.OrderBy(i => i.Id, StringComparer.Ordinal);

        public IEnumerable<Item> Of(ItemKind kind) => All.Where(i => i.Kind == kind);

        // what a shop may show this hero, and what a loot table may drop for them
        public IEnumerable<Item> For(string className, int level) =>
            All.Where(i => i.UsableBy(className, level));

        public ItemShelf With(IEnumerable<Item> more) => new ItemShelf(Plus(more), Problems);

        // read once and shared: a catalogue is read-only (Catalogue)
        static readonly Lazy<ItemShelf> TheSrd =
            new(() => new ItemShelf(ReadSrd("items", ItemReader.TryRead, out List<string> problems), problems));

        public static ItemShelf Srd() => TheSrd.Value;

        public override string ToString() => Counted("items");
    }
}
