using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Resolution;

namespace Core.Magic
{
    // WHAT A SPELL LEFT ON A CREATURE (Placement), and the ways it comes off: the spell ending on
    // it, one placement ending, the bearer acting (Invisibility)
    public sealed partial class Incantation
    {
        readonly List<Placement> _placed = new();

        internal void Place(Placement placement, Encounter fight)
        {
            placement.Fight = fight;
            _placed.Add(placement);

            if (fight != null && placement.NeedsWatching) Watch(fight);
        }

        // a placement made from an effect: its LingerSpec, whose turn counts it, and the plain
        // duration beside that owner
        internal Placement PlacementFor(Contact contact, Duration duration)
        {
            SpellEffect effect = contact.Effect;

            return new Placement
            {
                Target = contact.Target,
                Caster = contact.Caster,
                Spell = contact.Spell.Id,
                Level = contact.CastAt,
                Dc = contact.Dc,
                Spec = effect.Linger,
                Duration = duration.Plain(),
                Owner = duration.OnCastersTurn() ? contact.Caster.Actor : contact.Target,
                Since = contact.Fight?.Round ?? 0,
            };
        }

        // the placements of one condition on one creature are over: Lesser Restoration took it off
        internal void Unplace(Actor target, Condition condition) =>
            _placed.RemoveAll(p => ReferenceEquals(p.Target, target) && p.Condition == condition);

        // SRD 5.2.1 Invisibility: it ends "immediately after the target makes an attack roll, deals
        // damage, or casts a spell"
        internal void Unveil(Actor actor, Encounter fight)
        {
            if (actor == null) return;

            fight?.Reveal(actor);

            foreach (string spell in _placed.Where(p => ReferenceEquals(p.Target, actor) && p.Spec.EndsOnAct)
                                            .Select(p => p.Spell).Distinct().ToList())
                Lift(actor, spell, fight);
        }

        internal static string RevertedId(string spell) => spell + ".reverted";

        // a concentration thread let go of without ending what it did: Flesh to Stone's stone
        void Forget(Actor caster, Actor target, string spell)
        {
            if (_held.TryGetValue(caster, out List<Thread> threads))
                threads.RemoveAll(t => ReferenceEquals(t.Target, target) && t.SpellId == spell);
        }

        // ending a spell on one creature: its boons, the conditions it put there, its books - and
        // anything it does as it ends (Haste's lethargy)
        internal void Lift(Actor target, string spell, Encounter fight = null)
        {
            target.Boons.EndFrom(spell);

            List<Placement> going = _placed.Where(p => ReferenceEquals(p.Target, target) &&
                                                       p.Spell == spell).ToList();

            foreach (Placement placed in going.Where(p => p.Condition != Condition.None))
            {
                target.Remove(placed.Condition);
                fight?.Changed(target, placed.Condition, false);
            }

            _placed.RemoveAll(p => ReferenceEquals(p.Target, target) && p.Spell == spell);

            Ending(going.Select(p => p.Caster).FirstOrDefault(c => c != null), spell, target,
                   fight ?? going.Select(p => p.Fight).FirstOrDefault(f => f != null));
        }

        // one placement over - a timed blinding, a sleep shaken off - without touching the rest
        // of what the same spell did to the same creature
        void Drop(Placement placed, Encounter fight)
        {
            _placed.Remove(placed);

            if (placed.Condition == Condition.None) return;

            // another placement may still be holding the same condition on it
            bool held = _placed.Any(p => ReferenceEquals(p.Target, placed.Target) &&
                                         p.Condition == placed.Condition);

            if (held) return;

            placed.Target.Remove(placed.Condition);
            fight?.Changed(placed.Target, placed.Condition, false);
        }

        // the effects a spell has for the moment it ends on a creature
        void Ending(Caster caster, string spellId, Actor target, Encounter fight)
        {
            // Banishment, Maze: it reappears where it left, or on the nearest free square
            if (target != null) fight?.Recall(target, spellId);

            if (caster == null || target == null) return;

            Spell spell = caster.Find(spellId);

            if (spell == null) return;

            List<SpellEffect> last = spell.Effects.Where(e => e.Lands == Lands.OnEnd).ToList();

            if (last.Count == 0) return;

            var saves = new Dictionary<Actor, Attempt>();

            foreach (SpellEffect effect in last)
                Apply(caster, spell, effect, Aim.At(target), target, spell.Level, fight, null, saves);
        }
    }
}
