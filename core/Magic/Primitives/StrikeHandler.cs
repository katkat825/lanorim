using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Dice;
using Core.Rules;

namespace Core.Magic
{
    // STRIKE: one attack with the weapon chosen at the cast, swinging with the caster's
    // spellcasting ability, dealing the weapon's type or the spell's own (the better for the
    // caster), with extra dice by cantrip tier: True Strike. it goes through the fight's own
    // swing, so the target's reactions and every rule of an attack roll apply
    public sealed class StrikeHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Strike;

        public IReadOnlyList<string> Keys { get; } = new[] { "extra_tiers" };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.AimKind != AimKind.Creature) yield return "a strike is one attack at one creature";
        }

        public Landing Apply(Contact c)
        {
            SpellEffect effect = c.Effect;
            Actor target = c.Target;
            Attack weapon = c.Aim.Weapon;

            if (weapon == null) return new Landing(effect, target, false);

            // the spell's own type instead of the weapon's, when the target takes more from it
            DamageType type = weapon.DamageType;

            if (effect.DamageType != DamageType.None &&
                target.DefenseAgainst(effect.DamageType).Apply(100) >
                target.DefenseAgainst(weapon.DamageType).Apply(100))
                type = effect.DamageType;

            var attack = new Attack(weapon.Id, weapon.Damage, type, c.Caster.Ability,
                                    proficient: true, reach: weapon.Reach, range: weapon.Range,
                                    longRange: weapon.LongRange, hand: weapon.Hand,
                                    attackBonus: weapon.AttackBonus,
                                    damageBonus: weapon.DamageBonus,
                                    addsAbilityToDamage: weapon.AddsAbilityToDamage);

            var riders = new List<Rider>();

            if (effect.ExtraTiers.Count > 0)
            {
                DiceRoll extra = effect.ExtraTiers[Math.Min(effect.ExtraTiers.Count - 1,
                                                            SpellEffect.CantripTiers(c.Caster.Actor.Level))];

                if (!extra.IsNothing)
                    riders.Add(new Rider(c.Spell.Id, extra,
                                         effect.DamageType == DamageType.None
                                             ? DamageType.Radiant
                                             : effect.DamageType));
            }

            Blow blow;

            if (c.Fight != null)
            {
                if (!c.Fight.Field.InRange(c.Caster.Actor, target, attack.Reaches))
                    return new Landing(effect, target, false);

                blow = c.Fight.Swing(c.Caster.Actor, target, attack,
                                     c.Fight.Band(c.Caster.Actor, target, attack), riders);
            }
            else
            {
                blow = Strike.Make(c.Resolver, c.Caster.Actor, target, attack, riders: riders);
            }

            return new Landing(effect, target, blow.Hit, blow.Suffered, blow.Attempt);
        }
    }
}
