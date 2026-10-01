using System;
using System.Collections.Generic;
using System.Linq;
using Core.Resolution;
using Core.Words;

namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- conditions -----------------------------------------------------------------------

        readonly HashSet<Condition> _conditions = new();

        // who put a condition there, where it matters: Charmed is about the charmer, Frightened
        // about the source of the fear. a condition with nobody behind it (0 HP) has no entry
        readonly Dictionary<Condition, List<Actor>> _sources = new();

        // conditions this creature cannot have: a statblock's condition immunities, a Petrified
        // body's immunity to Poisoned
        public IReadOnlyCollection<Condition> Conditions => _conditions;

        public bool Has(Condition condition) => _conditions.Contains(condition);

        // whether it has the condition *because of* this creature - "charmed by you"
        public bool HasFrom(Condition condition, Actor source) =>
            source != null && _sources.TryGetValue(condition, out List<Actor> from) &&
            from.Contains(source);

        public IReadOnlyList<Actor> SourcesOf(Condition condition) =>
            _sources.TryGetValue(condition, out List<Actor> from)
                ? from
                : (IReadOnlyList<Actor>)Array.Empty<Actor>();

        public bool IsImmuneTo(Condition condition) =>
            Boons.Immune(condition) ||
            condition == Condition.Poisoned && _conditions.Contains(Condition.Petrified);

        // what the creature is by nature - a statblock's immunity - is a boon that never ends, the
        // same as what a feature or a spell gives: one place to ask (cc_task_dedupe-leftovers.md #11)
        public void MakeImmune(Condition condition)
        {
            if (condition == Condition.None) return;

            Boons.Add(Boon.Of(new BoonSpec { Duration = Duration.Permanent, ImmuneTo = new[] { condition } },
                              "nature." + condition.Id(), Nature));
        }

        // the source of what a creature is by nature
        public const string Nature = "nature";

        public bool Apply(Condition condition, Actor source = null)
        {
            if (condition == Condition.None || IsImmuneTo(condition)) return false;

            bool added = _conditions.Add(condition);

            if (source != null)
            {
                if (!_sources.TryGetValue(condition, out List<Actor> from))
                    _sources[condition] = from = new List<Actor>();

                if (!from.Contains(source)) from.Add(source);
            }

            // SRD 5.2.1: Unconscious includes Prone, and "when this condition ends, you remain
            // Prone" - so the prone is its own condition, not a part that leaves with it
            if (added && condition == Condition.Unconscious) _proneFromFalling = _conditions.Add(Condition.Prone);

            // Petrified: immunity to Poisoned, which ends one already there
            if (added && condition == Condition.Petrified) _conditions.Remove(Condition.Poisoned);

            // SRD 5.2.1: a Wild Shape ends when its wearer is incapacitated. 0 hit points is
            // unconscious, which incapacitates, so this one line is also "drop to 0 and revert"
            if (added && Shape != null && condition.Incapacitates()) Revert();

            return added;
        }

        // conditions the creature cannot end itself while a spell holds them: Hideous Laughter's
        // Prone. standing up asks this
        readonly HashSet<Condition> _pinned = new();

        public void Pin(Condition condition)
        {
            if (Has(condition)) _pinned.Add(condition);
        }

        public bool IsPinned(Condition condition) => _pinned.Contains(condition);

        // the Prone that came with falling unconscious, not one the creature had already: StaysUp takes only that
        bool _proneFromFalling;

        public bool Remove(Condition condition)
        {
            if (condition == Condition.Prone) _proneFromFalling = false;

            _sources.Remove(condition);
            _pinned.Remove(condition);

            return _conditions.Remove(condition);
        }

        public void ClearConditions()
        {
            _conditions.Clear();
            _sources.Clear();
            _pinned.Clear();
        }

        public bool IsIncapacitated => _conditions.Any(c => c.Incapacitates());

        public bool IsRooted => _conditions.Any(c => c.Roots());

        public bool IsDown => Health.IsDown;

        // down is not dead. a hero at 0 hit points is still in the fight until the single d20 >= 10
        // says otherwise, and the fight is not lost while that die is still to be thrown.
        public bool IsDead { get; private set; }

        public void Perish()
        {
            IsDead = true;
            Stable = false;
            Health.Kill();
            Apply(Condition.Unconscious);
        }

        public bool CanAct => !IsDown && !IsIncapacitated;

        public Advantage AttackAdvantage =>
            Advantages.Of(Boons.AnyAdvantageOnAttacks,
                          Boons.AnyDisadvantageOnAttacks ||
                          _conditions.Any(c => c.AttacksAtDisadvantage()));

        public Advantage CheckAdvantage =>
            Advantages.Of(Boons.AnyAdvantageOnChecks,
                          Boons.AnyDisadvantageOnChecks ||
                          _conditions.Any(c => c.ChecksAtDisadvantage()));

        // the same, for one particular check: a boon can lean on a single skill or a single
        // ability (Hex), and the blanket answer above cannot see that
        public Advantage CheckAdvantageFor(Ability ability, Skill skill) =>
            Advantages.Of(Boons.AdvantageOnCheck(ability, skill),
                          Boons.DisadvantageOnCheck(ability, skill) ||
                          _conditions.Any(c => c.ChecksAtDisadvantage()));
    }
}
