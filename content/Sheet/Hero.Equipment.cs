using System.Collections.Generic;
using Content.Inventory;
using Content.Items;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // wearing something takes it out of the pack, and whatever comes off goes back in
        public bool Wear(Item item, Slot? into = null)
        {
            if (item == null || Equipment.Refuses(item, Actor, Class.Id) != null) return false;

            if (into is Slot hand && hand != item.Slot && !(hand == Slot.OffHand && item.FitsOffHand)) return false;

            IReadOnlyList<Item> off = Equipment.Wear(item, Actor, Class.Id, into);

            Pack.Drop(item.Id);

            foreach (Item was in off) Pack.Take(was);

            return true;
        }

        // SRD 5.2.1 Light: a one-handed Light weapon in the off hand, for the Light bonus attack
        public bool HoldInOffHand(Item item) => item != null && item.FitsOffHand && Wear(item, Slot.OffHand);

        public bool TakeOff(Slot slot)
        {
            Item was = Equipment.Remove(slot, Actor);

            if (was == null) return false;

            Pack.Take(was);
            return true;
        }
    }
}
