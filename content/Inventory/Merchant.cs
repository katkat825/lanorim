using System;
using System.Collections.Generic;
using System.Linq;
using Content.Items;
using Core.Characters;
using Core.Localization;

namespace Content.Inventory
{
    // why a buy or a sell did not happen. each one has a line the merchant says, which is what
    // makes "the merchant has a line for a full inventory" a testable promise
    // (inventory_decisions.md).
    public enum Rebuff
    {
        None,

        NotStocked,

        NoGold,

        // every slot is taken - the merchant says so rather than the button going quiet
        PackFull,

        // class-locked or above the hero's level
        NotForYou,

        NothingToSell,

        // the player asked to sell something they are wearing and has not confirmed
        Equipped,
    }

    public sealed class Deal
    {
        public Deal(bool done, Rebuff rebuff = Rebuff.None, Item item = null, int count = 0,
                    int gold = 0)
        {
            Done = done;
            Rebuff = rebuff;
            Item = item;
            Count = count;
            Gold = gold;
        }

        public bool Done { get; }

        public Rebuff Rebuff { get; }

        public Item Item { get; }

        public int Count { get; }

        // gold that changed hands, positive either way
        public int Gold { get; }

        // what the shopkeeper says about it
        public string LineKey => Merchant.LineFor(Rebuff);

        public override string ToString() =>
            Done
                ? $"{Count} x {Item?.Id} for {Gold} gold"
                : $"no deal: {Rebuff}";
    }

    // buy and sell. the merchant's own gold is never modelled and never shown - it is unlimited
    // by decision (inventory_decisions.md), so the only question is what the player can afford.
    public sealed class Merchant
    {
        readonly List<string> _stock = new List<string>();

        public Merchant(string id, ItemShelf shelf, IEnumerable<string> stock = null,
                        int sellPercentOverride = -1)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Shelf = shelf ?? throw new ArgumentNullException(nameof(shelf));
            SellPercentOverride = sellPercentOverride;

            _stock.AddRange(stock ?? Enumerable.Empty<string>());
        }

        public string Id { get; }

        public ItemShelf Shelf { get; }

        // a merchant may pay better or worse than the game's default cut
        public int SellPercentOverride { get; }

        public IReadOnlyList<string> Stock => _stock;

        public void Stocks(string itemId)
        {
            if (!_stock.Contains(itemId)) _stock.Add(itemId);
        }

        // only what this hero could actually use - an item they can never equip must not be on
        // the shelf (inventory_decisions.md: "tests ensure unuseable items aren't surfaced")
        public IEnumerable<Item> Showing(string className, int level) =>
            _stock.Select(Shelf.Find)
                  .Where(i => i != null && i.UsableBy(className, level))
                  .OrderBy(i => i.Cost)
                  .ThenBy(i => i.Id, StringComparer.Ordinal);

        public int PriceOf(Item item) => item?.Cost ?? 0;

        public int PaysFor(Item item)
        {
            if (item == null) return 0;

            int percent = SellPercentOverride >= 0
                ? Math.Clamp(SellPercentOverride, 0, 100)
                : item.SellPercent;

            return item.Cost * percent / 100;
        }

        public Deal Buy(Pack pack, Item item, Actor hero, string className, int count = 1,
                        Equipment equip = null)
        {
            if (pack == null || item == null || count <= 0)
                return new Deal(false, Rebuff.NotStocked);

            if (!_stock.Contains(item.Id)) return new Deal(false, Rebuff.NotStocked, item);

            if (!item.UsableBy(className, hero?.Level ?? 1))
                return new Deal(false, Rebuff.NotForYou, item);

            int price = PriceOf(item) * count;

            if (pack.Gold < price) return new Deal(false, Rebuff.NoGold, item, count, price);

            // the full-pack refusal is checked before the gold leaves, so a refused buy costs
            // nothing at all
            if (!pack.Fits(item, count)) return new Deal(false, Rebuff.PackFull, item, count, price);

            pack.Spend(price);
            pack.Take(item, count);

            if (equip != null && item.IsEquippable)
                foreach (Item off in equip.Wear(item, hero, className))
                    pack.Take(off);

            return new Deal(true, Rebuff.None, item, count, price);
        }

        public Deal Sell(Pack pack, Item item, int count = 1, Equipment equip = null,
                         Actor hero = null, bool confirmed = false)
        {
            if (pack == null || item == null || count <= 0)
                return new Deal(false, Rebuff.NothingToSell);

            // selling what you are wearing warns every time, and the warning cannot be dismissed
            // for good (inventory_decisions.md)
            if (equip != null && equip.IsEquipped(item.Id) && !confirmed)
                return new Deal(false, Rebuff.Equipped, item, count);

            if (pack.CountOf(item.Id) < count && !(equip?.IsEquipped(item.Id) ?? false))
                return new Deal(false, Rebuff.NothingToSell, item, count);

            if (equip != null && equip.IsEquipped(item.Id))
            {
                equip.Remove(item.Id, hero);
                pack.Take(item);
            }

            int sold = pack.Drop(item.Id, count);

            if (sold == 0) return new Deal(false, Rebuff.NothingToSell, item, count);

            int paid = PaysFor(item) * sold;

            pack.Earn(paid);

            return new Deal(true, Rebuff.None, item, sold, paid);
        }

        public static string LineFor(Rebuff rebuff) =>
            KeyConventions.Key(KeyConventions.MerchantNs, Line(rebuff), "line");

        static string Line(Rebuff rebuff) => rebuff switch
        {
            Rebuff.NotStocked => "not_stocked",
            Rebuff.NoGold => "no_gold",
            Rebuff.PackFull => "pack_full",
            Rebuff.NotForYou => "not_for_you",
            Rebuff.NothingToSell => "nothing_to_sell",
            Rebuff.Equipped => "equipped",
            _ => "greeting",
        };

        // every line a merchant can say, so the locale audit covers them
        public static IEnumerable<string> Keys() =>
            new[]
            {
                Rebuff.None, Rebuff.NotStocked, Rebuff.NoGold, Rebuff.PackFull,
                Rebuff.NotForYou, Rebuff.NothingToSell, Rebuff.Equipped,
            }.Select(LineFor);

        public override string ToString() => $"{Id}, {_stock.Count} lines of stock";
    }

    // what the player must do when a pickup would overflow the pack: pick things to drop, with
    // the new item shown separately and choosable (inventory_decisions.md).
    public sealed class Overflow
    {
        readonly List<string> _chosen = new List<string>();

        public Overflow(Pack pack, Item arriving, int count = 1)
        {
            Pack = pack ?? throw new ArgumentNullException(nameof(pack));
            Arriving = arriving;
            Count = count;
        }

        public Pack Pack { get; }

        public Item Arriving { get; }

        public int Count { get; }

        public int SlotsNeeded => Pack.SlotsNeededFor(Arriving, Count);

        public int SlotsShort => Math.Max(0, SlotsNeeded - Pack.Free);

        public bool Resolved => SlotsShort == 0;

        public IEnumerable<Stack> Choosable => Pack.Stacks;

        // discarding the new item leaves the pack exactly as it was
        public bool RefuseTheNewItem { get; private set; }

        public void Refuse() => RefuseTheNewItem = true;

        public bool Discard(string itemId)
        {
            Stack stack = Pack.Stacks.FirstOrDefault(s => s.Item.Id == itemId);

            if (stack == null) return false;

            // a discard is the whole stack: half a stack frees no slot
            Pack.Drop(itemId, stack.Count);
            _chosen.Add(itemId);

            return true;
        }

        public IReadOnlyList<string> Discarded => _chosen;

        // discarded items are gone. this is where the once-per-character warning is raised
        public bool Finish()
        {
            if (RefuseTheNewItem) return true;

            if (!Resolved) return false;

            Pack.Take(Arriving, Count);
            return true;
        }

        public override string ToString() =>
            RefuseTheNewItem ? "the new item was refused"
          : Resolved ? $"room made for {Count} x {Arriving?.Id}"
          : $"{SlotsShort} slots short for {Count} x {Arriving?.Id}";
    }
}
