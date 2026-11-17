using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Space;

namespace Content.Combat
{
    // A RANGED CASTER STEPS OUT OF MELEE (cc_task_cb Part 1.2), the way a sensible player does, once a turn and before
    // the attack. Only when all of these hold:
    //   - the best option this turn is an attack roll that a foe beside you puts at disadvantage: a ranged weapon or a
    //     spell attack with a range (a saving-throw spell doesn't care, so nobody steps for one);
    //   - at most one foe can take an opportunity attack as you go (it has its reaction and a melee attack); a person
    //     doesn't walk away from two;
    //   - the attack without disadvantage is worth more than that opportunity attack is expected to cost (its hit
    //     chance against your Armor Class times its average damage);
    //   - a square you can reach this turn is next to no foe and still sees the target within the option's range.
    // It never Disengages: that spends the action the attack needed. The previews don't rate an opportunity attack, so
    // the bot rates it here, in the previews' own terms (CombatSession.AttackChance)
    public sealed partial class AutoPlayer
    {
        // off: the AutoPlayer as task H measured it (`sim --bot plain`)
        public bool KeepsAway { get; init; } = true;

        bool KeepAway(CombatSession session)
        {
            Encounter fight = session.Fight;
            Actor me = session.Hero.Actor;

            if (!fight.Crowded(me)) return false;

            var best = Ranked(session).FirstOrDefault();

            if (best.option == null || best.target == null || !(fight.Field.Where(best.target) is Cell there))
                return false;

            int range = Range(session, best.option);

            if (range <= 1) return false;

            List<Actor> threats = fight.Field.Enemies(me)
                                       .Where(e => !e.IsDown && fight.Field.Distance(me, e) <= 1 &&
                                                   fight.OpportunityAttackOf(e) != null && fight.ReactionsLeft(e) > 0)
                                       .ToList();

            if (threats.Count > 1) return false;

            double cost = threats.Sum(e =>
            {
                Attack swing = fight.OpportunityAttackOf(e);
                return CombatSession.AttackChance(swing.Modifier(e), me.ArmorClass, Advantage.Flat) *
                       Math.Max(0, swing.DamageFor(e).Average);
            });

            int modifier = best.option.Attack?.Modifier(me) ?? session.Hero.Caster.AttackModifierFor(best.option.Spell);
            int armor = best.target.ArmorClass + fight.Cover(me, best.target);
            double crowded = CombatSession.AttackChance(modifier, armor, Advantage.Disadvantage);
            double clear = CombatSession.AttackChance(modifier, armor, Advantage.Flat);

            if (crowded <= 0 || best.damage * (clear / crowded - 1) <= cost) return false;

            List<Cell> foes = fight.Field.Enemies(me).Where(e => !e.IsDown)
                                   .Select(e => fight.Field.Where(e)).Where(c => c.HasValue).Select(c => c.Value)
                                   .ToList();

            Cell? step = session.Reachable()
                                .Where(p => foes.All(f => Battlefield.Distance(p.Key, f) > 1))
                                .Where(p => Battlefield.Distance(p.Key, there) <= range && fight.Field.CanSee(p.Key, there))
                                .OrderBy(p => p.Value)
                                .Select(p => (Cell?)p.Key)
                                .FirstOrDefault();

            if (!step.HasValue) return false;

            Note?.Invoke($"steps out of melee to {step.Value} (worth ~{best.damage * (clear / crowded - 1):0.0}, " +
                         $"risks ~{cost:0.0})");

            return session.Move(step.Value).Done;
        }

        // squares, for an attack roll a foe beside you hurts; 0 for anything else
        static int Range(CombatSession session, ActionOption option)
        {
            if (option.Attack != null) return option.Attack.IsRanged ? option.Attack.Range : 0;

            return option.Spell != null && option.Spell.Effects.Any(e => e.AttackRoll)
                ? option.Spell.RangeAt(session.Hero.Actor.Level)
                : 0;
        }
    }
}
