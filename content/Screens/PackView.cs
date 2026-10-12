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

        public PackView(Hero hero, ItemShelf shelf, Merchant merchant = null, bool armory = false)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            _shelf = shelf;
            Merchant = merchant;
            Armory = armory && merchant != null;
        }

        // AN ARMORY (cc_task_i 3.3): the merchant's stock lent rather than sold - worn for free, as often as the player
        // likes, and nothing bought, sold or put in the pack
        public bool Armory { get; }

        public Hero Hero { get; }

        public Merchant Merchant { get; }

        // a merchant to buy from and sell to; an armory is neither
        public bool AtTheCounter => Merchant != null && !Armory;

        public Pack Pack => Hero.Pack;

        // the purse, in copper (Coins); Money says it
        public int Copper => Pack.Copper;

        public int Used => Pack.Used;

        public int Capacity => Pack.Capacity;

        public IReadOnlyList<PackRow> Rows =>
            Pack.Stacks.Select(s => Row(s, false)).Concat(Pack.QuestItems.Select(s => Row(s, true))).ToList();

        PackRow Row(Stack stack, bool quest) => new PackRow
        {
            Item = stack.Item,
            Count = stack.Count,
            Equipped = Hero.Equipment.IsEquipped(stack.Item.Id),
            SellsFor = AtTheCounter && !quest ? Merchant.PaysFor(stack.Item) : -1,
            Quest = quest,
        };

        // what the merchant shows this hero: the class and level filters are the merchant's
        public IReadOnlyList<ShopRow> Shelf =>
            !AtTheCounter
                ? Array.Empty<ShopRow>()
                : Merchant.Showing(Hero.Class.Id, Hero.Level)
                          .Select(item => new ShopRow
                          {
                              Item = item,
                              Price = Merchant.PriceOf(item),
                              Affordable = Merchant.PriceOf(item) <= Copper,
                          })
                          .ToList();

        // the line the shopkeeper said last
        public string MerchantLineKey { get; private set; } = Merchant.LineFor(Rebuff.None);

        public Deal Buy(string itemId, int count = 1)
        {
            if (!AtTheCounter) return new Deal(false, Rebuff.NotStocked);

            Deal deal = Merchant.Buy(Pack, Find(itemId), Hero.Actor, Hero.Class.Id, count);
            MerchantLineKey = deal.LineKey;

            return deal;
        }

        // an equipped item asks every time: confirmed is the player's yes to this sale
        public Deal Sell(string itemId, int count = 1, bool confirmed = false)
        {
            if (!AtTheCounter) return new Deal(false, Rebuff.NothingToSell);

            Deal deal = Merchant.Sell(Pack, Find(itemId), count, Hero.Equipment, Hero.Actor, confirmed);
            MerchantLineKey = deal.LineKey;

            return deal;
        }

        // selling from the pack sells the pack's; it asks only when what would go is the one being worn
        public bool NeedsSellConfirmation(string itemId) => Hero.Equipment.IsEquipped(itemId) && Pack.CountOf(itemId) == 0;

        // --- selling what's worn (cc_task_i 5.1) ------------------------------------------------------------------

        // what the merchant pays for the thing in a slot, or -1: no counter, nothing there, a quest item, or a loan
        public int SellsForWorn(Slot slot) =>
            AtTheCounter && Hero.Equipment.In(slot) is Item item && item.Kind != ItemKind.Quest && !Hero.OnLoan(item.Id)
                ? Merchant.PaysFor(item)
                : -1;

        // asks every time: confirmed is the player's yes to selling this one, off their back
        public Deal SellWorn(Slot slot, bool confirmed = false)
        {
            if (SellsForWorn(slot) < 0) return new Deal(false, Rebuff.NothingToSell);

            Deal deal = Merchant.SellWorn(Pack, Hero.Equipment.In(slot), Hero.Equipment, Hero.Actor, confirmed);
            MerchantLineKey = deal.LineKey;

            return deal;
        }

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

        // --- what is worn, and the off hand (cc_task_open-questions-answers.md 3.1) -------------------------------
        //
        // A weapon, armour or a shield in the pack can be put on; a one-handed Light weapon can be held in the off hand
        // instead (SRD 5.2.1 Light), which is what the Light bonus attack swings. What comes off goes back in the pack

        Item InPack(string itemId) => Pack.FirstOf(itemId)?.Item;

        public bool CanEquip(string itemId) =>
            InPack(itemId) is Item item && item.IsEquippable && Hero.Equipment.Refuses(item, Hero.Actor, Hero.Class.Id) == null;

        public bool Equip(string itemId) => CanEquip(itemId) && Hero.Wear(InPack(itemId));

        public bool CanHoldInOffHand(string itemId) => CanEquip(itemId) && InPack(itemId).FitsOffHand;

        public bool HoldInOffHand(string itemId) => CanHoldInOffHand(itemId) && Hero.HoldInOffHand(InPack(itemId));

        public IReadOnlyList<(Slot Slot, Item Item)> Worn =>
            Slots.All.Where(s => Hero.Equipment.In(s) != null).Select(s => (s, Hero.Equipment.In(s))).ToList();

        public bool TakeOff(Slot slot) => Hero.TakeOff(slot);

        // --- the armory's racks (cc_task_i 3.3) ----------------------------------------------------------------

        // what the armory lends this hero: the merchant's own class and level filters
        public IReadOnlyList<Item> Racks =>
            Armory ? Merchant.Showing(Hero.Class.Id, Hero.Level).ToList() : Array.Empty<Item>();

        Item OnTheRacks(string itemId) => Racks.FirstOrDefault(i => i.Id == itemId);

        public bool CanBorrow(string itemId) =>
            OnTheRacks(itemId) is Item item && item.IsEquippable && Hero.Equipment.Refuses(item, Hero.Actor, Hero.Class.Id) == null;

        public bool Borrow(string itemId) => CanBorrow(itemId) && Hero.Borrow(OnTheRacks(itemId));

        public bool CanBorrowInOffHand(string itemId) => CanBorrow(itemId) && OnTheRacks(itemId).FitsOffHand;

        public bool BorrowInOffHand(string itemId) =>
            CanBorrowInOffHand(itemId) && Hero.Borrow(OnTheRacks(itemId), Slot.OffHand);

        // a worn thing the armory lent: taking it off gives it back
        public bool IsLoaned(Item item) => item != null && Hero.OnLoan(item.Id);

        public static string SlotKey(Slot slot) => ScreenKeys.Key("pack", "slot_" + Core.Words.EnumWords.Id(slot));

        // AN AMOUNT, AS COINS (cc_task_f 1.5): copper said as gold, silver and copper, the largest first, each only when
        // there is some - "2 gp", "5 sp", "1 gp 5 sp" - and nothing at all as "0 gp". keys and counts; the screen says them
        public static IReadOnlyList<(string Key, int Count)> Money(int copper)
        {
            (int gold, int silver, int coppers) = Coins.Split(copper);

            var said = new List<(string, int)>();

            if (gold > 0) said.Add((GoldKey, gold));
            if (silver > 0) said.Add((SilverKey, silver));
            if (coppers > 0) said.Add((CopperKey, coppers));

            return said.Count > 0 ? said : new[] { (GoldKey, 0) };
        }

        Item Find(string itemId) => _shelf?.Find(itemId) ?? Pack.FirstOf(itemId)?.Item;

        public static readonly string TitleKey = ScreenKeys.Key("pack", "title");
        public static readonly string GoldKey = ScreenKeys.Key("pack", "gold");
        public static readonly string SilverKey = ScreenKeys.Key("pack", "silver");
        public static readonly string CopperKey = ScreenKeys.Key("pack", "copper");
        public static readonly string SlotsKey = ScreenKeys.Key("pack", "slots");
        public static readonly string QuestKey = ScreenKeys.Key("pack", "quest_items");
        public static readonly string UseKey = ScreenKeys.Key("pack", "use");
        public static readonly string EquipKey = ScreenKeys.Key("pack", "equip");
        public static readonly string UnequipKey = ScreenKeys.Key("pack", "unequip");
        public static readonly string OffHandKey = ScreenKeys.Key("pack", "off_hand");
        public static readonly string WornKey = ScreenKeys.Key("pack", "worn");
        public static readonly string DiscardKey = ScreenKeys.Key("pack", "discard");
        public static readonly string DiscardWarningKey = ScreenKeys.Key("pack", "discard_warning");
        public static readonly string DontWarnAgainKey = ScreenKeys.Key("pack", "dont_warn_again");
        public static readonly string BuyKey = ScreenKeys.Key("pack", "buy");
        public static readonly string SellKey = ScreenKeys.Key("pack", "sell");
        public static readonly string SellEquippedKey = ScreenKeys.Key("pack", "sell_equipped");
        public static readonly string NewItemKey = ScreenKeys.Key("pack", "new_item");
        public static readonly string MakeRoomKey = ScreenKeys.Key("pack", "make_room");
        public static readonly string LeaveKey = ScreenKeys.Key("pack", "leave");
        public static readonly string ArmoryKey = ScreenKeys.Key("pack", "armory");
        public static readonly string RacksKey = ScreenKeys.Key("pack", "racks");
        public static readonly string LoanedKey = ScreenKeys.Key("pack", "loaned");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                TitleKey, GoldKey, SilverKey, CopperKey, SlotsKey, QuestKey, UseKey, EquipKey, UnequipKey, OffHandKey, WornKey, DiscardKey,
                DiscardWarningKey, DontWarnAgainKey, BuyKey, SellKey, SellEquippedKey, NewItemKey,
                MakeRoomKey, LeaveKey, ArmoryKey, RacksKey, LoanedKey,
            }.Concat(Slots.All.Select(SlotKey));
    }
}
