using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Words;

namespace Content.Items
{
    public static class ItemReader
    {
        public static bool TryRead(string text, out IReadOnlyList<Item> items,
                                   out IReadOnlyList<string> problems) =>
            Entries.TryRead(text, out items, out problems);

        // every key an item takes, and the keys of its weapon, its armor and each of its boons
        // (a boon's own keys are BoonSpecReader.Keys)
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "id", "kind", "cost", "stackable", "sell_percent", "slot", "weapon", "armor", "classes",
            "minimum_level", "boons", "heals", "casts", "uses", "vanishes", "use_time",
        };

        // the file: { "items": [ ... ] }, each entry read by ReadOne (EntryList)
        public static readonly EntryList<Item> Entries =
            new EntryList<Item>("items", "item", Keys, ReadOne);

        // what a weapon is beside an attack (AttackReader.Keys): its category and its properties
        public static readonly IReadOnlyList<string> WeaponKeys = new[] { "category", "light", "heavy", "versatile" };

        public static readonly IReadOnlyList<string> ArmorKeys = new[] { "category", "base", "strength", "noisy" };

        public static readonly IReadOnlyList<string> BoonKeys = new[] { "id", "duration" };

        static Item ReadOne(JsonElement entry, string id, List<string> problems)
        {
            if (!EnumWords.TryParse(entry.Text("kind"), out ItemKind kind))
            {
                problems.Add($"{id}: '{entry.Text("kind")}' is not a kind of item");
                return null;
            }

            if (!EnumWords.TryParse(entry.Text("slot", "none"), out Slot slot) &&
                entry.Text("slot", "none") != "none")
                problems.Add($"{id}: '{entry.Text("slot")}' is not an equipment slot");

            Attack attack = null;
            ArmorProfile? armor = null;

            if (entry.Has("weapon"))
            {
                JsonElement w = entry.GetProperty("weapon");

                Keyed.OnlyKnown(w, WeaponKeys.Concat(AttackReader.Keys), $"{id} weapon", problems);

                attack = AttackReader.Read(w, id, slot == Slot.TwoHand ? Hand.Two
                                                  : slot == Slot.OffHand ? Hand.Off : Hand.Main,
                                           id, problems);

                if (attack.Category.Length > 0 && attack.Category != "simple" && attack.Category != "martial")
                    problems.Add($"{id}: a weapon's category is simple or martial");
            }
            else if (kind == ItemKind.Weapon)
            {
                problems.Add($"{id}: a weapon with no 'weapon' block");
            }

            if (entry.Has("armor"))
            {
                JsonElement a = entry.GetProperty("armor");

                Keyed.OnlyKnown(a, ArmorKeys, $"{id} armor", problems);

                if (!EnumWords.TryParse(a.Text("category", "none"), out ArmorCategory category))
                    problems.Add($"{id}: '{a.Text("category")}' is not light, medium or heavy");

                armor = new ArmorProfile(category, a.Number("base", 10),
                                         a.Number("strength"), a.Flag("noisy"));
            }
            else if (kind == ItemKind.Armor)
            {
                problems.Add($"{id}: armor with no 'armor' block");
            }

            var boons = new List<Boon>();

            foreach (JsonElement raw in entry.Items("boons"))
            {
                Boon boon = ReadBoon(id, raw, problems);

                if (boon != null) boons.Add(boon);
            }

            var item = new Item(id, kind,
                                entry.Price("cost", id, problems),
                                entry.Flag("stackable"),
                                entry.Has("sell_percent") ? entry.Number("sell_percent") : -1,
                                slot,
                                attack,
                                armor,
                                entry.Strings("classes"),
                                entry.Number("minimum_level", 1),
                                boons,
                                entry.Dice("heals", problems, id),
                                entry.Text("casts"),
                                entry.Number("uses"))
            {
                Vanishes = entry.Text("vanishes") == "long_rest",
                UseTime = EnumWords.TryParse(entry.Text("use_time", "bonus_action"), out Core.Combat.Spend use) &&
                          use == Core.Combat.Spend.Action
                    ? Core.Combat.Spend.Action
                    : Core.Combat.Spend.Bonus,
            };

            if (!string.IsNullOrEmpty(entry.Text("vanishes")) && entry.Text("vanishes") != "long_rest")
                problems.Add($"{id}: 'vanishes' is 'long_rest' - the one time an item goes");

            Check(item, problems);

            return item;
        }

        // an item's boon: its own id and duration, and what the boon is in the one boon vocabulary
        static Boon ReadBoon(string itemId, JsonElement raw, List<string> problems)
        {
            Keyed.OnlyKnown(raw, BoonKeys.Concat(BoonSpecReader.Keys), $"{itemId} boon", problems);

            if (!EnumWords.TryParse(raw.Text("duration", "rest"), out Duration duration))
                problems.Add($"{itemId}: '{raw.Text("duration")}' is not a duration");

            BoonSpec spec = BoonSpecReader.Read(raw, duration, itemId, problems);

            BoonSpecReader.Check(spec, itemId, problems, mustDoSomething: true);

            return Boon.Of(spec, raw.Text("id", itemId), itemId);
        }

        static void Check(Item item, List<string> problems)
        {
            if (item.Kind == ItemKind.Quest && item.Boons.Count > 0)
                problems.Add($"{item.Id}: a quest item that grants a boon takes a slot like " +
                             "anything else - give it another kind (inventory_decisions.md)");

            if (item.Kind == ItemKind.Shield && item.Slot != Slot.OffHand)
                problems.Add($"{item.Id}: a shield goes in the off hand (\"slot\": \"off_hand\")");

            if (item.Kind == ItemKind.Armor && item.Slot != Slot.Body)
                problems.Add($"{item.Id}: armor goes on the body");

            if (item.Kind == ItemKind.Weapon &&
                item.Slot != Slot.MainHand && item.Slot != Slot.OffHand && item.Slot != Slot.TwoHand)
                problems.Add($"{item.Id}: a weapon goes in a hand");

            if (item.Stackable && item.IsEquippable)
                problems.Add($"{item.Id}: an equippable item does not stack - the equipped icon " +
                             "would have to sit on one of ninety-nine thousand");
        }
    }
}
