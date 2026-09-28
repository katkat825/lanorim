using System;
using System.Collections.Generic;
using Content.Inventory;
using Content.Items;
using Core.Characters;
using Core.Combat;
using Core.Magic;
using Core.Resolution;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // --- what spells hand over, and what gets used up --------------------------------------

        // Goodberry: the items a casting made go in the pack. what does not fit is left behind -
        // returned, so the discard flow can offer a swap
        public IReadOnlyList<(Item item, int count)> Receive(Casting casting, ItemShelf shelf)
        {
            var left = new List<(Item, int)>();

            if (casting == null || shelf == null) return left;

            foreach ((string id, int count) in casting.Conjured)
            {
                Item item = shelf.Find(id);

                if (item == null) continue;

                int taken = Pack.Take(item, count);

                if (taken < count) left.Add((item, count - taken));
            }

            return left;
        }

        // drink a potion, eat a berry: it heals what it heals and one is gone. in a fight it costs
        // what the item says (a bonus action for both); refused when there is none to spend
        public int Use(string itemId, IResolver resolver, Turn turn = null)
        {
            Stack stack = Pack.FirstOf(itemId);

            if (stack == null || resolver == null || stack.Item.Kind != ItemKind.Consumable) return -1;

            if (turn != null && !turn.Take(stack.Item.UseTime)) return -1;

            int healed = stack.Item.Heals.IsNothing
                ? 0
                : Actor.Mend(Math.Max(0, resolver.Roll(stack.Item.Heals)));

            Pack.Drop(itemId, 1);

            return healed;
        }
    }
}
