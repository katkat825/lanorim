using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Localization;

namespace Content.Items
{
    public enum ItemKind
    {
        Weapon,
        Armor,
        Shield,

        // drunk, eaten, thrown away: a potion, a scroll
        Consumable,

        // worn and does something while it is worn
        Trinket,

        // sold for gold and nothing else
        Treasure,

        // a key, a signet ring, a letter. costs no inventory slot
        // (inventory_decisions.md: quest items that give no character benefit)
        Quest,

        // rope, a lamp, a crowbar
        Tool,
    }

    public enum Slot
    {
        None = 0,
        MainHand,
        OffHand,
        TwoHand,
        Body,
        Shield,
        Trinket,
    }

    public sealed class Item
    {
        public Item(string id, ItemKind kind, int cost = 0, bool stackable = false,
                    int sellPercent = -1, Slot slot = Slot.None,
                    Attack attack = null, ArmorProfile? armor = null,
                    IReadOnlyList<string> classes = null, int minimumLevel = 1,
                    IReadOnlyList<Boon> boons = null, DiceRoll heals = default,
                    string castsSpell = null, int uses = 0)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Kind = kind;
            Cost = Math.Max(0, cost);
            Stackable = stackable;
            SellPercent = sellPercent < 0 ? DefaultSellPercent : Math.Clamp(sellPercent, 0, 100);
            Slot = slot;
            Attack = attack;
            Armor = armor;
            Classes = classes ?? Array.Empty<string>();
            MinimumLevel = Math.Max(1, minimumLevel);
            Boons = boons ?? Array.Empty<Boon>();
            Heals = heals;
            CastsSpell = castsSpell ?? "";
            Uses = Math.Max(0, uses);
        }

        // the game's cut on a sale; an item may raise its own, up to the full price
        // (inventory_decisions.md)
        public const int DefaultSellPercent = 40;

        // one stack is this many; past it, the next one takes another slot
        public const int StackLimit = 99_999;

        public string Id { get; }

        public ItemKind Kind { get; }

        // gold. currency is gold-only (decisions_checklist.md section 1)
        public int Cost { get; }

        public bool Stackable { get; }

        public int SellPercent { get; }

        public int SellPrice => Cost * SellPercent / 100;

        public Slot Slot { get; }

        public Attack Attack { get; }

        public ArmorProfile? Armor { get; }

        // empty means anyone may use it
        public IReadOnlyList<string> Classes { get; }

        public int MinimumLevel { get; }

        // what wearing it does
        public IReadOnlyList<Boon> Boons { get; }

        // what drinking it does
        public DiceRoll Heals { get; }

        public string CastsSpell { get; }

        // 0 is unlimited; a wand has a number
        public int Uses { get; }

        // gone from the pack at the next long rest: Goodberry's berries (24 hours)
        public bool Vanishes { get; init; }

        // what using it costs in a fight: SRD 5.2.1 drinks a potion as a bonus action, and eats
        // a goodberry the same way
        public Core.Combat.Spend UseTime { get; init; } = Core.Combat.Spend.Bonus;

        public bool IsEquippable => Slot != Slot.None;

        // a quest item that does nothing for the character takes no slot
        public bool TakesASlot => Kind != ItemKind.Quest;

        public string NameKey => KeyConventions.ItemName(Id);

        public string DescriptionKey => KeyConventions.ItemDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;
        }

        // gating: a shop must not offer it and a loot table must not drop it
        // (inventory_decisions.md - "tests ensure unuseable items aren't surfaced")
        public bool UsableBy(string className, int level) =>
            level >= MinimumLevel &&
            (Classes.Count == 0 ||
             Classes.Contains(className, StringComparer.OrdinalIgnoreCase));

        public override string ToString() =>
            $"{Id} [{Kind.ToString().ToLowerInvariant()}] {Cost}gp" +
            (Stackable ? ", stacks" : "") +
            (Slot == Slot.None ? "" : $", {Slot.ToString().ToLowerInvariant()}") +
            (Classes.Count > 0 ? $", {string.Join("/", Classes)} only" : "") +
            (MinimumLevel > 1 ? $", level {MinimumLevel}+" : "");
    }

    public static class Slots
    {
        public static readonly IReadOnlyList<Slot> All = new[]
        {
            Slot.MainHand, Slot.OffHand, Slot.TwoHand, Slot.Body, Slot.Shield, Slot.Trinket,
        };

        public static string Id(this Slot slot) => slot switch
        {
            Slot.MainHand => "main_hand",
            Slot.OffHand => "off_hand",
            Slot.TwoHand => "two_hand",
            _ => slot.ToString().ToLowerInvariant(),
        };

        public static bool TryParse(string id, out Slot slot)
        {
            foreach (Slot s in All)
            {
                if (!string.Equals(s.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                slot = s;
                return true;
            }

            slot = Slot.None;
            return false;
        }

        // a two-handed weapon takes both hands: equipping one takes the other two off
        public static IEnumerable<Slot> Conflicts(this Slot slot) => slot switch
        {
            Slot.TwoHand => new[] { Slot.MainHand, Slot.OffHand, Slot.Shield },
            Slot.MainHand => new[] { Slot.TwoHand },
            Slot.OffHand => new[] { Slot.TwoHand },
            Slot.Shield => new[] { Slot.TwoHand },
            _ => Array.Empty<Slot>(),
        };
    }

    public static class ItemKinds
    {
        public static readonly IReadOnlyList<ItemKind> All = new[]
        {
            ItemKind.Weapon, ItemKind.Armor, ItemKind.Shield, ItemKind.Consumable,
            ItemKind.Trinket, ItemKind.Treasure, ItemKind.Quest, ItemKind.Tool,
        };

        public static string Id(this ItemKind kind) => kind.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out ItemKind kind)
        {
            foreach (ItemKind k in All)
            {
                if (!string.Equals(k.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                kind = k;
                return true;
            }

            kind = ItemKind.Treasure;
            return false;
        }
    }
}
