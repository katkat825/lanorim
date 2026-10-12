using System;
using System.Collections.Generic;
using Content.Inventory;
using Content.Items;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // wearing something takes it out of the pack, and whatever comes off goes back in (or back to the armory)
        public bool Wear(Item item, Slot? into = null)
        {
            if (!Fits(item, into)) return false;

            IReadOnlyList<Item> off = Equipment.Wear(item, Actor, Class.Id, into);

            Pack.Drop(item.Id);

            foreach (Item was in off) PutAway(was);

            return true;
        }

        // SRD 5.2.1 Light: a one-handed Light weapon in the off hand, for the Light bonus attack
        public bool HoldInOffHand(Item item) => item != null && item.FitsOffHand && Wear(item, Slot.OffHand);

        public bool TakeOff(Slot slot)
        {
            Item was = Equipment.Remove(slot, Actor);

            if (was == null) return false;

            PutAway(was);
            return true;
        }

        bool Fits(Item item, Slot? into) =>
            item != null && Equipment.Refuses(item, Actor, Class.Id) == null &&
            !(into is Slot hand && hand != item.Slot && !(hand == Slot.OffHand && item.FitsOffHand));

        // --- gear on loan from an armory (cc_task_i 3.3) ------------------------------------------------------
        //
        // An armory lends: the hero wears what it stocks for free, and it never goes in the pack - taken off, or
        // swapped for something else, it goes back. Kept in the save, so a loan stays a loan

        readonly HashSet<string> _loaned = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Loaned => _loaned;

        public bool OnLoan(string itemId) => itemId != null && _loaned.Contains(itemId);

        public bool Borrow(Item item, Slot? into = null)
        {
            if (!Fits(item, into)) return false;

            IReadOnlyList<Item> off = Equipment.Wear(item, Actor, Class.Id, into);

            foreach (Item was in off) PutAway(was);

            _loaned.Add(item.Id);
            return true;
        }

        // what came off: back to the armory if it was lent, else into the pack
        void PutAway(Item was)
        {
            if (_loaned.Remove(was.Id)) return;

            Pack.Take(was);
        }

        // a save putting the loans back
        internal void Lent(IEnumerable<string> itemIds)
        {
            foreach (string id in itemIds ?? Array.Empty<string>()) _loaned.Add(id);
        }
    }
}
