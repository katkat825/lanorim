using System;
using System.Collections.Generic;
using System.Linq;
using Content.Inventory;
using Content.Items;
using Content.Sheet;
using Core.Resolution;

namespace Content.Screens
{
    public sealed class PackRow
    {
        public Item Item { get; init; }

        public string NameKey => Item.NameKey;

        public int Count { get; init; }

        public bool Equipped { get; init; }

        // what the merchant at the counter pays for one, or -1 with no merchant open
        public int SellsFor { get; init; } = -1;

        public bool Quest { get; init; }
    }

    public sealed class ShopRow
    {
        public Item Item { get; init; }

        public string NameKey => Item.NameKey;

        public int Price { get; init; }

        public bool Affordable { get; init; }
    }

    // THE PACK SCREEN, AND THE MERCHANT'S COUNTER BESIDE IT (Tier 2.8; inventory_decisions.md):
    // the stacks and the slots, gold, what the merchant pays and asks; buy, sell (an equipped item
    // asks every time), discard (gone for good - the warning dismissable per character), use
    public sealed class PackView
    {
        readonly ItemShelf _shelf;

        public PackView(Hero hero, ItemShelf shelf, Merchant merchant = null)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            _shelf = shelf;
            Merchant = merchant;
        }

        public Hero Hero { get; }

        public Merchant Merchant { get; }

        public bool AtTheCounter => Merchant != null;

        public Pack Pack => Hero.Pack;

        public int Gold => Pack.Gold;

        public int Used => Pack.Used;

        public int Capacity => Pack.Capacity;

        public IReadOnlyList<PackRow> Rows =>
            Pack.Stacks.Select(s => Row(s, false)).Concat(Pack.QuestItems.Select(s => Row(s, true))).ToList();

        PackRow Row(Stack stack, bool quest) => new PackRow
        {
            Item = stack.Item,
            Count = stack.Count,
            Equipped = Hero.Equipment.IsEquipped(stack.Item.Id),
            SellsFor = Merchant != null && !quest ? Merchant.PaysFor(stack.Item) : -1,
            Quest = quest,
        };

        // what the merchant shows this hero: the class and level filters are the merchant's
        public IReadOnlyList<ShopRow> Shelf =>
            Merchant == null
                ? Array.Empty<ShopRow>()
                : Merchant.Showing(Hero.Class.Id, Hero.Level)
                          .Select(item => new ShopRow
                          {
                              Item = item,
                              Price = Merchant.PriceOf(item),
                              Affordable = Merchant.PriceOf(item) <= Gold,
                          })
                          .ToList();

        // the line the shopkeeper said last
        public string MerchantLineKey { get; private set; } = Merchant.LineFor(Rebuff.None);

        public Deal Buy(string itemId, int count = 1)
        {
            if (Merchant == null) return new Deal(false, Rebuff.NotStocked);

            Deal deal = Merchant.Buy(Pack, Find(itemId), Hero.Actor, Hero.Class.Id, count);
            MerchantLineKey = deal.LineKey;

            return deal;
        }

        // an equipped item asks every time: confirmed is the player's yes to this sale
        public Deal Sell(string itemId, int count = 1, bool confirmed = false)
        {
            if (Merchant == null) return new Deal(false, Rebuff.NothingToSell);

            Deal deal = Merchant.Sell(Pack, Find(itemId), count, Hero.Equipment, Hero.Actor, confirmed);
            MerchantLineKey = deal.LineKey;

            return deal;
        }

        public bool NeedsSellConfirmation(string itemId) => Hero.Equipment.IsEquipped(itemId);

        // DISCARDING: the whole stack, gone. the first time, the screen shows the warning; the player
        // can dismiss it for good for this character
        public bool NeedsDiscardWarning => !Hero.DiscardWarningDismissed;

        public void DismissDiscardWarning() => Hero.DiscardWarningDismissed = true;

        public bool Discard(string itemId)
        {
            Stack stack = Pack.Stacks.FirstOrDefault(s => s.Item.Id == itemId);

            if (stack == null) return false;

            if (Hero.Equipment.IsEquipped(itemId)) Hero.Equipment.Remove(itemId, Hero.Actor);

            Pack.Drop(itemId, stack.Count);
            return true;
        }

        public int Use(string itemId, IResolver resolver) => Hero.Use(itemId, resolver);

        Item Find(string itemId) => _shelf?.Find(itemId) ?? Pack.FirstOf(itemId)?.Item;

        public static readonly string TitleKey = ScreenKeys.Key("pack", "title");
        public static readonly string GoldKey = ScreenKeys.Key("pack", "gold");
        public static readonly string SlotsKey = ScreenKeys.Key("pack", "slots");
        public static readonly string QuestKey = ScreenKeys.Key("pack", "quest_items");
        public static readonly string UseKey = ScreenKeys.Key("pack", "use");
        public static readonly string EquipKey = ScreenKeys.Key("pack", "equip");
        public static readonly string UnequipKey = ScreenKeys.Key("pack", "unequip");
        public static readonly string DiscardKey = ScreenKeys.Key("pack", "discard");
        public static readonly string DiscardWarningKey = ScreenKeys.Key("pack", "discard_warning");
        public static readonly string DontWarnAgainKey = ScreenKeys.Key("pack", "dont_warn_again");
        public static readonly string BuyKey = ScreenKeys.Key("pack", "buy");
        public static readonly string SellKey = ScreenKeys.Key("pack", "sell");
        public static readonly string SellEquippedKey = ScreenKeys.Key("pack", "sell_equipped");
        public static readonly string NewItemKey = ScreenKeys.Key("pack", "new_item");
        public static readonly string MakeRoomKey = ScreenKeys.Key("pack", "make_room");
        public static readonly string LeaveKey = ScreenKeys.Key("pack", "leave");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                TitleKey, GoldKey, SlotsKey, QuestKey, UseKey, EquipKey, UnequipKey, DiscardKey,
                DiscardWarningKey, DontWarnAgainKey, BuyKey, SellKey, SellEquippedKey, NewItemKey,
                MakeRoomKey, LeaveKey,
            };
    }
}
