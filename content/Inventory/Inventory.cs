using System;
using System.Collections.Generic;
using System.Linq;
using Content.Items;

namespace Content.Inventory
{
    // one line in the pack: an item and how many of it. a stack past the limit becomes a second
    // stack and therefore a second slot (inventory_decisions.md).
    public sealed class Stack
    {
        public Stack(Item item, int count = 1)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Count = Math.Max(0, count);
        }

        public Item Item { get; }

        public int Count { get; private set; }

        public bool Full => Item.Stackable
            ? Count >= Item.StackLimit()
            : Count >= 1;

        public int Room => Item.StackLimit() - Count;

        public int Add(int more)
        {
            int taken = Math.Min(Math.Max(0, more), Room);
            Count += taken;
            return taken;
        }

        public int Take(int fewer)
        {
            int given = Math.Min(Math.Max(0, fewer), Count);
            Count -= given;
            return given;
        }

        // 1,000 reads as "1k" so the slot only has to plan for three characters
        // (inventory_decisions.md). the real number is on the details panel.
        public string ShortCount => Short(Count);

        public static string Short(int count) =>
            count < 1000 ? count.ToString() : (count / 1000) + "k";

        public override string ToString() => $"{Item.Id} x{Count}";
    }

    public static class Stacks
    {
        public static int StackLimit(this Item item) => item.Stackable ? Item.StackLimit : 1;
    }

    // what the hero is carrying. flat, forty slots, no containers (inventory_decisions.md).
    public sealed class Pack
    {
        readonly List<Stack> _stacks = new List<Stack>();
        readonly List<Stack> _questItems = new List<Stack>();

        public const int Slots = 40;

        public int Capacity { get; }

        public Pack(int capacity = Slots) => Capacity = Math.Max(1, capacity);

        // a quest item that does nothing for the character costs no slot, so it is kept apart
        public IReadOnlyList<Stack> Stacks => _stacks;

        public IReadOnlyList<Stack> QuestItems => _questItems;

        public IEnumerable<Stack> Everything => _stacks.Concat(_questItems);

        public int Used => _stacks.Count;

        public int Free => Math.Max(0, Capacity - Used);

        public bool IsFull => Free <= 0;

        public int Gold { get; private set; }

        public void Earn(int gold) => Gold += Math.Max(0, gold);

        public bool Spend(int gold)
        {
            if (gold < 0 || gold > Gold) return false;

            Gold -= gold;
            return true;
        }

        public int CountOf(string itemId) =>
            Everything.Where(s => s.Item.Id == itemId).Sum(s => s.Count);

        public bool Has(string itemId, int count = 1) => CountOf(itemId) >= count;

        public Stack FirstOf(string itemId) =>
            Everything.FirstOrDefault(s => s.Item.Id == itemId && s.Count > 0);

        // how many more slots taking this many of that item would need. zero means it fits in
        // what is already there.
        public int SlotsNeededFor(Item item, int count = 1)
        {
            if (item == null || !item.TakesASlot) return 0;

            int left = count;

            foreach (Stack stack in _stacks.Where(s => s.Item.Id == item.Id))
                left -= stack.Room;

            if (left <= 0) return 0;

            int limit = item.StackLimit();

            return (left + limit - 1) / limit;
        }

        public bool Fits(Item item, int count = 1) => SlotsNeededFor(item, count) <= Free;

        // takes what fits and says how many were left over; the caller then has to make room
        // (inventory_decisions.md - the player must discard before continuing)
        public int Take(Item item, int count = 1)
        {
            if (item == null || count <= 0) return 0;

            if (!item.TakesASlot)
            {
                Stack quest = _questItems.FirstOrDefault(s => s.Item.Id == item.Id);

                if (quest == null) _questItems.Add(new Stack(item, count));
                else quest.Add(count);

                return count;
            }

            int left = count;

            foreach (Stack stack in _stacks.Where(s => s.Item.Id == item.Id).ToList())
            {
                if (left <= 0) break;

                left -= stack.Add(left);
            }

            while (left > 0 && !IsFull)
            {
                int here = Math.Min(left, item.StackLimit());

                _stacks.Add(new Stack(item, here));
                left -= here;
            }

            return count - left;
        }

        public int Drop(string itemId, int count = 1)
        {
            int left = count;

            foreach (Stack stack in Everything.Where(s => s.Item.Id == itemId).ToList())
            {
                if (left <= 0) break;

                left -= stack.Take(left);
            }

            Tidy();

            return count - left;
        }

        void Tidy()
        {
            _stacks.RemoveAll(s => s.Count <= 0);
            _questItems.RemoveAll(s => s.Count <= 0);
        }

        public override string ToString() =>
            $"{Used}/{Capacity} slots, {Gold} gold" +
            (_questItems.Count > 0 ? $", {_questItems.Count} quest items" : "");
    }
}
