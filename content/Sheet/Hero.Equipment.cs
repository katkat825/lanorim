using System.Collections.Generic;
using Content.Inventory;
using Content.Items;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // wearing something takes it out of the pack, and whatever comes off goes back in
        public bool Wear(Item item)
        {
            if (item == null || Equipment.Refuses(item, Actor, Class.Id) != null) return false;

            IReadOnlyList<Item> off = Equipment.Wear(item, Actor, Class.Id);

            Pack.Drop(item.Id);

            foreach (Item was in off) Pack.Take(was);

            return true;
        }

        public bool TakeOff(Slot slot)
        {
            Item was = Equipment.Remove(slot, Actor);

            if (was == null) return false;

            Pack.Take(was);
            return true;
        }
    }
}
