using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;

namespace Core.Magic
{
    // CONCENTRATION: binary and one at a time. taking a new one drops the old one and every boon
    // and condition it put out, wherever those landed
    public sealed partial class Incantation
    {
        // who is holding what up: a thread per thing a held spell did to somebody
        readonly ConcentrationLedger _held = new();

        void Hold(Actor caster, string spellId)
        {
            string dropped = caster.Concentrate(spellId);

            if (string.IsNullOrEmpty(dropped)) return;

            Unwind(caster, dropped);
        }

        internal void Remember(Actor caster, Actor touched, string spellId,
                               Condition condition = Condition.None) =>
            _held.Remember(caster, touched, spellId, condition);

        void Unwind(Actor caster, string spellId)
        {
            // whatever it left lying on the board comes up first - a zone is held even when it
            // has not yet put anything on anybody
            EndZones(caster, spellId);

            // who it was on, and the fight and caster it was cast with - for what it does as it
            // ends (Haste's lethargy)
            var ending = new List<(Caster caster, Actor target, Encounter fight)>();

            foreach (ConcentrationLedger.Thread thread in _held.Of(caster, spellId))
            {
                Placement any = _placed.FirstOrDefault(p => ReferenceEquals(p.Target, thread.Target) &&
                                                            p.Spell == spellId);

                if (any?.Caster != null &&
                    !ending.Any(e => ReferenceEquals(e.target, thread.Target)))
                    ending.Add((any.Caster, thread.Target, any.Fight));
                else if (any?.Caster == null)
                    any?.Fight?.Recall(thread.Target, spellId);

                thread.Target.Boons.EndFrom(spellId);

                if (thread.Condition != Condition.None)
                {
                    thread.Target.Remove(thread.Condition);
                    any?.Fight?.Changed(thread.Target, thread.Condition, false);
                }

                _placed.RemoveAll(p => ReferenceEquals(p.Target, thread.Target) &&
                                       p.Spell == spellId);
            }

            _held.Drop(caster, spellId);

            foreach ((Caster who, Actor target, Encounter fight) in ending)
                Ending(who, spellId, target, fight);
        }

        // the caster went down, or let go on purpose
        public void Release(Actor caster)
        {
            if (caster == null) return;

            string dropped = caster.EndConcentration();

            if (!string.IsNullOrEmpty(dropped)) Unwind(caster, dropped);
        }

        // called every time anything goes down: SRD drops concentration when the caster does,
        // and SRD 5.2.1 when the caster is Incapacitated at all
        public void Check(IEnumerable<Actor> actors)
        {
            foreach (Actor actor in (actors ?? Enumerable.Empty<Actor>()).ToList())
                if ((actor.IsDown || actor.IsIncapacitated) && actor.IsConcentrating)
                    Release(actor);
        }
    }
}
