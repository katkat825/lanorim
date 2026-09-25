using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Dice;

namespace Content.Items
{
    public static class ItemReader
    {
        public static bool TryRead(string text, out IReadOnlyList<Item> items,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<Item>();
            var trouble = new List<string>();

            items = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                foreach (JsonElement entry in document.RootElement.Items("items"))
                {
                    Item item = ReadOne(entry, trouble);

                    if (item != null) found.Add(item);
                }

                if (found.Count == 0 && trouble.Count == 0)
                    trouble.Add("no items in it - the file is an object with an 'items' array");
            }

            return trouble.Count == 0;
        }

        static Item ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not an item id - lowercase a-z, 0-9 and underscore only");
                return null;
            }

            if (!ItemKinds.TryParse(entry.Text("kind"), out ItemKind kind))
            {
                problems.Add($"{id}: '{entry.Text("kind")}' is not a kind of item");
                return null;
            }

            if (!Slots.TryParse(entry.Text("slot", "none"), out Slot slot) &&
                entry.Text("slot", "none") != "none")
                problems.Add($"{id}: '{entry.Text("slot")}' is not an equipment slot");

            Attack attack = null;
            ArmorProfile? armor = null;

            if (entry.Has("weapon"))
            {
                JsonElement w = entry.GetProperty("weapon");

                DiceRoll damage = w.Dice("damage", problems, id);

                if (damage.IsNothing) problems.Add($"{id}: a weapon with no damage");

                DamageType type = w.Damage("damage_type", problems, id);

                if (type == DamageType.None)
                    problems.Add($"{id}: a weapon with no damage_type");

                Ability? ability = w.Ability("ability", problems, id);

                attack = new Attack(id, damage, type,
                                    ability ?? Ability.Strength,
                                    true,
                                    w.Number("reach", 1),
                                    w.Number("range"),
                                    w.Number("long_range"),
                                    slot == Slot.TwoHand ? Hand.Two
                                    : slot == Slot.OffHand ? Hand.Off : Hand.Main,
                                    w.Flag("finesse"),
                                    w.Number("attack_bonus"),
                                    w.Number("damage_bonus"));
            }
            else if (kind == ItemKind.Weapon)
            {
                problems.Add($"{id}: a weapon with no 'weapon' block");
            }

            if (entry.Has("armor"))
            {
                JsonElement a = entry.GetProperty("armor");

                if (!ArmorWeights.TryParse(a.Text("weight", "none"), out ArmorWeight weight))
                    problems.Add($"{id}: '{a.Text("weight")}' is not light, medium or heavy");

                armor = new ArmorProfile(weight, a.Number("base", 10),
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
                                entry.Number("cost"),
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
                UseTime = entry.Text("use_time", "bonus_action") == "action"
                    ? Core.Combat.Spend.Action
                    : Core.Combat.Spend.Bonus,
            };

            if (!string.IsNullOrEmpty(entry.Text("vanishes")) && entry.Text("vanishes") != "long_rest")
                problems.Add($"{id}: 'vanishes' is 'long_rest' - the one time an item goes");

            Check(item, problems);

            return item;
        }

        static Boon ReadBoon(string itemId, JsonElement raw, List<string> problems)
        {
            if (!Core.Magic.Primitives.TryParse(raw.Text("touches", "none"),
                                                out Core.Magic.Sways touches))
            {
                problems.Add($"{itemId}: '{raw.Text("touches")}' is not a list of swayed rolls");
                return null;
            }

            if (!Schools.TryParseDuration(raw.Text("duration", "rest"), out Duration duration))
                problems.Add($"{itemId}: '{raw.Text("duration")}' is not a duration");

            return new Boon(raw.Text("id", itemId), itemId, duration,
                            raw.Number("flat"),
                            raw.Dice("dice", problems, itemId),
                            attacks: (touches & Core.Magic.Sways.Attacks) != 0,
                            saves: (touches & Core.Magic.Sways.Saves) != 0,
                            checks: (touches & Core.Magic.Sways.Checks) != 0,
                            damage: (touches & Core.Magic.Sways.Damage) != 0,
                            armorClass: (touches & Core.Magic.Sways.ArmorClass) != 0
                                            ? raw.Number("flat") : 0,
                            skill: raw.Skill("skill", problems, itemId),
                            save: raw.Ability("save", problems, itemId));
        }

        static void Check(Item item, List<string> problems)
        {
            if (item.Kind == ItemKind.Quest && item.Boons.Count > 0)
                problems.Add($"{item.Id}: a quest item that grants a boon takes a slot like " +
                             "anything else - give it another kind (inventory_decisions.md)");

            if (item.Kind == ItemKind.Shield && item.Slot != Slot.Shield)
                problems.Add($"{item.Id}: a shield goes in the shield slot");

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

    // the Duration parser lives with the spells; items need it too, and a second copy would drift
    static class Schools
    {
        public static bool TryParseDuration(string id, out Duration duration) =>
            Core.Magic.Schools.TryParse(id, out duration);
    }
}
