using System.Collections.Generic;
using Core.Characters;

namespace Core.Magic
{
    // WHO IS HOLDING WHAT UP: one thread per thing a held spell did to somebody. keeping the condition on the
    // thread is what lets a dropped concentration lift exactly what it put there and nothing else. the
    // incantation holds one and decides what letting go does (Incantation.Concentration); this only keeps the
    // threads (cc_task_d-seams-and-duplication.md §8: it was Incantation's private state)
    internal sealed class ConcentrationLedger
    {
        readonly Dictionary<Actor, List<Thread>> _held = new();

        internal readonly struct Thread
        {
            public Thread(Actor target, string spellId, Condition condition)
            {
                Target = target;
                SpellId = spellId;
                Condition = condition;
            }

            public Actor Target { get; }

            public string SpellId { get; }

            public Condition Condition { get; }
        }

        public void Remember(Actor caster, Actor touched, string spellId, Condition condition)
        {
            if (!_held.TryGetValue(caster, out List<Thread> threads))
                _held[caster] = threads = new List<Thread>();

            threads.Add(new Thread(touched, spellId, condition));
        }

        // every thread of one spell, in the order they were spun
        public List<Thread> Of(Actor caster, string spellId) =>
            _held.TryGetValue(caster, out List<Thread> threads)
                ? threads.FindAll(t => t.SpellId == spellId)
                : new List<Thread>();

        // the spell let go of: its threads come off the ledger
        public void Drop(Actor caster, string spellId)
        {
            if (_held.TryGetValue(caster, out List<Thread> threads)) threads.RemoveAll(t => t.SpellId == spellId);
        }

        // a thread let go of without ending what it did: Flesh to Stone's stone
        public void Forget(Actor caster, Actor target, string spellId)
        {
            if (_held.TryGetValue(caster, out List<Thread> threads))
                threads.RemoveAll(t => ReferenceEquals(t.Target, target) && t.SpellId == spellId);
        }

        // the thread follows a worsened condition, so letting go ends the new one
        public void Swap(Actor caster, Actor target, string spellId, Condition was, Condition now)
        {
            if (!_held.TryGetValue(caster, out List<Thread> threads)) return;

            for (int i = 0; i < threads.Count; i++)
                if (ReferenceEquals(threads[i].Target, target) && threads[i].SpellId == spellId &&
                    threads[i].Condition == was)
                    threads[i] = new Thread(target, spellId, now);
        }
    }
}
