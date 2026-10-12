using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Core.Combat
{
    // WHO HOLDS WHOM, and the escape DC each grapple was made at (SRD 5.2.1 Unarmed Strike, p.190). Was Encounter's
    // private state (cc_task_j-split-classes-dev-hooks.md Part 1); making, escaping and letting go are still the
    // fight's (Encounter.GrappleShove.cs), since they say so to everyone
    internal sealed class Grapples
    {
        readonly Dictionary<Actor, (Actor By, int Dc)> _held = new();

        public bool IsBy(Actor target, Actor grappler) =>
            target != null && _held.TryGetValue(target, out var g) && ReferenceEquals(g.By, grappler);

        public bool Holds(Actor target) => target != null && _held.ContainsKey(target);

        // everyone one creature is holding
        public List<Actor> HeldBy(Actor grappler) =>
            _held.Where(g => ReferenceEquals(g.Value.By, grappler)).Select(g => g.Key).ToList();

        public void Hold(Actor target, Actor by, int dc) => _held[target] = (by, dc);

        public bool TryGet(Actor target, out (Actor By, int Dc) grapple) => _held.TryGetValue(target, out grapple);

        public bool Release(Actor target) => _held.Remove(target);

        // every grapple, as (held, by), in the order they were made; a copy, safe to release from while walked
        public List<(Actor Held, Actor By)> All() => _held.Select(g => (g.Key, g.Value.By)).ToList();
    }
}
