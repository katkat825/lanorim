using System;
using System.Collections.Generic;
using System.Linq;
using Content.Items;
using Core.Characters;
using Core.Words;

namespace Content.Inventory
{
    // what is worn and held, and the one path that puts it on an Actor. nothing else may set
    // Actor.Armor or hand out a Boon from an item, so taking a thing off always undoes exactly
    // what putting it on did.
    public sealed class Equipment
    {
        readonly Dictionary<Slot, Item> _worn = new Dictionary<Slot, Item>();

        public IReadOnlyDictionary<Slot, Item> Worn => _worn;

        public Item In(Slot slot) => _worn.TryGetValue(slot, out Item item) ? item : null;

        public bool IsEquipped(string itemId) => _worn.Values.Any(i => i.Id == itemId);

        public IEnumerable<Item> All => Slots.All.Select(In).Where(i => i != null);

        // an off-hand weapon swings from the off hand: the same weapon, Hand.Off, which is how the Light bonus attack
        // tells two daggers apart (Hero.Hitting)
        public IEnumerable<Attack> Attacks =>
            Slots.All.Where(s => In(s)?.Attack != null)
                 .Select(s => s == Slot.OffHand ? In(s).Attack.With(hand: Hand.Off) : In(s).Attack);

        // why not, so the UI can grey the button and say something. null means it may be worn.
        public string Refuses(Item item, Actor actor, string className)
        {
            if (item == null) return "no item";

            if (!item.IsEquippable) return "not something you wear";

            if (actor != null && !item.UsableBy(className, actor.Level))
                return item.MinimumLevel > (actor?.Level ?? 1)
                    ? $"needs level {item.MinimumLevel}"
                    : $"for a {string.Join(" or ", item.Classes)}";

            return null;
        }

        // returns what came off - the caller puts those back in the pack. `into` is the slot when it isn't the item's
        // own: a one-handed Light weapon held in the off hand (SRD 5.2.1 Light; cc_task_open-questions-answers.md 3.1)
        public IReadOnlyList<Item> Wear(Item item, Actor actor, string className = null, Slot? into = null)
        {
            if (Refuses(item, actor, className) != null) return Array.Empty<Item>();

            Slot to = into ?? item.Slot;

            if (to != item.Slot && !(to == Slot.OffHand && item.FitsOffHand)) return Array.Empty<Item>();

            var removed = new List<Item>();

            foreach (Slot slot in to.Conflicts().Concat(new[] { to }).Distinct())
            {
                Item was = In(slot);

                if (was == null) continue;

                _worn.Remove(slot);
                removed.Add(was);
            }

            _worn[to] = item;

            Apply(actor);

            return removed;
        }

        public Item Remove(Slot slot, Actor actor)
        {
            Item was = In(slot);

            if (was == null) return null;

            _worn.Remove(slot);

            Apply(actor);

            return was;
        }

        public Item Remove(string itemId, Actor actor)
        {
            Slot slot = Slots.All.FirstOrDefault(s => In(s)?.Id == itemId);

            return slot == Slot.None ? null : Remove(slot, actor);
        }

        // rebuilds the actor's gear-derived numbers from scratch. from scratch on purpose: an
        // incremental add/remove is where a +1 gets left behind after a swap.
        public void Apply(Actor actor)
        {
            if (actor == null) return;

            actor.Boons.EndFrom(Source);

            foreach (Item item in All)
                foreach (Boon boon in item.Boons)
                    actor.Boons.Add(Restamped(boon));

            Item body = In(Slot.Body);

            actor.Armor = body?.Armor ?? ArmorProfile.Unarmored;
            actor.HasShield = In(Slot.Shield) != null;
        }

        // every item boon is stamped with the same source, so one call takes all of them off and
        // a spell's boons are untouched
        public const string Source = "equipment";

        // the whole spec comes along: this used to be a hand copy of the constructor's first
        // sixteen arguments, which dropped everything after them (cc_task_dedupe-effects.md, Lead 1)
        static Boon Restamped(Boon boon) => Boon.Of(boon.Spec, boon.Id, Source);

        public override string ToString() =>
            _worn.Count == 0
                ? "nothing equipped"
                : string.Join(", ", _worn.Select(p => $"{p.Key.Id()}: {p.Value.Id}"));
    }
}
