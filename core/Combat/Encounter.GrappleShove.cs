using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): Grapple and Shove
    public sealed partial class Encounter
    {
        // --- Grapple and Shove (SRD 5.2.1 Unarmed Strike, p.190) --------------------------------

        // who holds whom, and the escape DC it was made at
        readonly Dictionary<Actor, (Actor By, int Dc)> _grapples = new();

        public bool IsGrappledBy(Actor target, Actor grappler) =>
            target != null && _grapples.TryGetValue(target, out var g) && ReferenceEquals(g.By, grappler);

        public bool IsHeldByGrapple(Actor target) => target != null && _grapples.ContainsKey(target);

        List<Actor> Held(Actor grappler) =>
            _grapples.Where(g => ReferenceEquals(g.Value.By, grappler)).Select(g => g.Key).ToList();

        // DC 8 + Strength modifier + Proficiency Bonus
        public static int UnarmedDc(Actor attacker) =>
            8 + attacker.AbilityModifier(Ability.Strength) + attacker.ProficiencyBonus;

        // the Unarmed Strike's other two options, each one attack: the target is within 5 feet and
        // no more than one size larger, and makes a Strength or Dexterity save (its choice - the
        // better) against the DC
        Attempt Unarmed(Turn turn, Actor target, Attack strike)
        {
            if (turn == null || turn.Ended || target == null || Over) return null;

            Actor me = turn.Actor;

            if (Field.Distance(me, target) > 1) return null;

            if ((int)target.CurrentSize > (int)me.CurrentSize + 1) return null;

            if (me.HasFrom(Condition.Charmed, target)) return null;

            if (!turn.TakeAttack(Spend.Action, strike)) return null;

            AttackRolled?.Invoke(me);
            Reveal(me);

            Ability best = target.SaveModifier(Ability.Strength) >= target.SaveModifier(Ability.Dexterity)
                ? Ability.Strength
                : Ability.Dexterity;

            return Checks.Save(_resolver, target, best, UnarmedDc(me));
        }

        public Attempt Grapple(Turn turn, Actor target, Attack strike = null)
        {
            Attempt save = Unarmed(turn, target, strike);

            if (save == null || save.Succeeded) return save;

            if (target.Apply(Condition.Grappled, turn.Actor))
            {
                _grapples[target] = (turn.Actor, UnarmedDc(turn.Actor));
                Changed(target, Condition.Grappled, true);
            }

            return save;
        }

        // pushed 5 feet straight away, or knocked Prone
        public Attempt ShoveAway(Turn turn, Actor target, bool prone, Attack strike = null)
        {
            Attempt save = Unarmed(turn, target, strike);

            if (save == null || save.Succeeded) return save;

            if (prone)
            {
                if (target.Apply(Condition.Prone, turn.Actor)) Changed(target, Condition.Prone, true);
            }
            else if (Field.Where(turn.Actor) is Cell me && Field.Where(target) is Cell at)
            {
                var away = new Cell(at.X + Math.Sign(at.X - me.X), at.Y + Math.Sign(at.Y - me.Y));

                if (Field.Map.CanCross(at, away)) Shove(target, away);
            }

            return save;
        }

        // an action, and a Strength (Athletics) or Dexterity (Acrobatics) check - its choice, the
        // better - against the grapple's DC
        public Attempt EscapeGrapple(Turn turn)
        {
            if (turn == null || turn.Ended || !_grapples.TryGetValue(turn.Actor, out var held))
                return null;

            if (!turn.Take(Spend.Action)) return null;

            Skill skill = turn.Actor.CheckModifier(Skill.Athletics) >= turn.Actor.CheckModifier(Skill.Acrobatics)
                ? Skill.Athletics
                : Skill.Acrobatics;

            Attempt check = Checks.Check(_resolver, turn.Actor, skill, held.Dc);

            if (check.Succeeded) Release(turn.Actor);

            return check;
        }

        void Release(Actor target)
        {
            if (!_grapples.Remove(target)) return;

            target.Remove(Condition.Grappled);
            Changed(target, Condition.Grappled, false);
        }

        // SRD 5.2.1 Grappled: it ends when the grappler is Incapacitated or the two are no longer
        // within reach of each other
        void LetGo()
        {
            foreach (var g in _grapples.ToList())
                if (g.Value.By.IsDown || !g.Value.By.CanAct || g.Key.IsDown ||
                    Field.Distance(g.Value.By, g.Key) > 1)
                    Release(g.Key);
        }
    }
}
