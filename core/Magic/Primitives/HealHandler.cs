using System;
using System.Collections.Generic;
using Core.Characters;

namespace Core.Magic
{
    // HEAL: hit points on, never past the maximum - and Raise Dead's, on the dead
    public sealed class HealHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Heal;

        public bool Helps(SpellEffect effect) => true;

        public bool KindWhateverElse => true;

        public bool Allows(string setting) => setting == "add_modifier";

        public IReadOnlyList<string> Keys { get; } = new[] { "revives" };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.Amount.IsNothing) yield return "a heal effect with no 'amount'";
        }

        public Landing Apply(Contact c)
        {
            SpellEffect effect = c.Effect;
            Actor target = c.Target;
            Actor caster = c.Caster.Actor;

            // Raise Dead: the dead come back with the amount, and nothing else heals them.
            // Disintegrate's dust is past Raise Dead (only True Resurrection or Wish)
            if (effect.Revives && target.Dust)
                return new Landing(effect, target, false);

            if (effect.Revives && target.IsDead)
            {
                int at = Math.Max(1, c.Resolver.Roll(c.Amount, caster) + c.Modifier);

                target.Raise(at);

                return new Landing(effect, target, true, target.Health.Current);
            }

            // SRD 5.2.1 Supreme Healing (p.40): the healing dice at their highest
            int dice = caster.Is("supreme_healing")
                ? c.Amount.Maximum
                : c.Resolver.Roll(c.Amount, caster);

            // Disciple of Life (p.40): a spell slot's healing restores 2 + the slot's level more
            int disciple = c.CastAt >= 1 && caster.Is("disciple_of_life") ? 2 + c.CastAt : 0;

            int healed = target.Mend(Math.Max(0, dice + c.Modifier + disciple));

            // Blessed Healer (p.40): healing someone else with a slot heals you 2 + its level
            if (healed > 0 && c.CastAt >= 1 && !ReferenceEquals(target, caster) &&
                caster.Is("blessed_healer") && !c.Magic.BlessedThisCast)
            {
                caster.Mend(2 + c.CastAt);
                c.Magic.BlessedThisCast = true;
            }

            return new Landing(effect, target, healed > 0, healed);
        }
    }
}
