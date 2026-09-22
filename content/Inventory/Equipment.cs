using System;
using System.Collections.Generic;
using System.Linq;
using Content.Items;
using Core.Characters;

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

        public IEnumerable<Attack> Attacks =>
            All.Where(i => i.Attack != null).Select(i => i.Attack);

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

        // returns what came off - the caller puts those back in the pack
        public IReadOnlyList<Item> Wear(Item item, Actor actor, string className = null)
        {
            if (Refuses(item, actor, className) != null) return Array.Empty<Item>();

            var removed = new List<Item>();

            foreach (Slot slot in item.Slot.Conflicts().Concat(new[] { item.Slot }).Distinct())
            {
                Item was = In(slot);

                if (was == null) continue;

                _worn.Remove(slot);
                removed.Add(was);
            }

            _worn[item.Slot] = item;

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

        static Boon Restamped(Boon boon) =>
            new Boon(boon.Id, Source, boon.Duration, boon.Flat, boon.Dice,
                     boon.Attacks, boon.Saves, boon.Checks, boon.Damage, boon.ArmorClass,
                     boon.Skill, boon.Save,
                     boon.AdvantageOnChecks, boon.DisadvantageOnChecks,
                     boon.AdvantageOnAttacks, boon.DisadvantageOnAttacks);

        public override string ToString() =>
            _worn.Count == 0
                ? "nothing equipped"
                : string.Join(", ", _worn.Select(p => $"{p.Key.Id()}: {p.Value.Id}"));
    }
}
