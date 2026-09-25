using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Combat
{
    // SRD 5.2.1 MULTIATTACK: one action that makes several attacks - exactly the ones the statblock
    // lists, which may be three or more. each slot names the attacks that may fill it, so "one Bite
    // attack and one Claw attack" is two slots of one attack each, and "two attacks, using Scimitar
    // or Shortbow in any combination" is two slots that take either. an empty slot takes any attack
    // the creature has.
    //
    // it is a monster's thing: a hero's extra attacks are extra actions (ActionBudget), and a
    // hero's budget never carries one of these.
    public sealed class Multiattack
    {
        public Multiattack(IEnumerable<IEnumerable<string>> slots)
        {
            Slots = (slots ?? Enumerable.Empty<IEnumerable<string>>())
                    .Select(s => (IReadOnlyList<string>)(s ?? Enumerable.Empty<string>())
                                                         .Where(id => !string.IsNullOrEmpty(id))
                                                         .Distinct(StringComparer.Ordinal)
                                                         .ToList())
                    .ToList();
        }

        // "the creature makes N attacks", with whatever it has
        public static Multiattack Any(int count) =>
            new Multiattack(Enumerable.Range(0, Math.Max(0, count))
                                      .Select(_ => Enumerable.Empty<string>()));

        public IReadOnlyList<IReadOnlyList<string>> Slots { get; }

        public int Count => Slots.Count;

        // an attack the Multiattack is made of. one that isn't is used by an Attack action of its
        // own - a single attack, as the SRD reads it
        public bool Uses(string attackId) => Slots.Any(s => Fits(s, attackId));

        internal static bool Fits(IReadOnlyList<string> slot, string attackId) =>
            slot.Count == 0 || attackId == null || slot.Contains(attackId, StringComparer.Ordinal);

        public Volley Begin() => new Volley(Slots);

        public override string ToString() =>
            string.Join(" + ", Slots.Select(s => s.Count == 0 ? "any" : string.Join("|", s)));
    }

    // one Multiattack being made: the slots its action paid for and has not yet filled
    public sealed class Volley
    {
        readonly List<IReadOnlyList<string>> _open;

        internal Volley(IEnumerable<IReadOnlyList<string>> slots) => _open = slots.ToList();

        public int Left => _open.Count;

        public bool Allows(string attackId) => _open.Any(s => Multiattack.Fits(s, attackId));

        // fills the narrowest slot the attack fits, so "claw, or claw or bite" spends a claw on
        // the claw-only slot and keeps the other open for the bite
        public bool Take(string attackId)
        {
            IReadOnlyList<string> slot = _open.Where(s => Multiattack.Fits(s, attackId))
                                              .OrderBy(s => s.Count == 0 ? int.MaxValue : s.Count)
                                              .FirstOrDefault();

            if (slot == null) return false;

            _open.Remove(slot);
            return true;
        }
    }
}
