using System.Collections.Generic;
using Core.Characters;
using Core.Resolution;

namespace Core.Magic
{
    // AFFLICT: a condition, for a duration, and everything about how it ends (its LingerSpec)
    public sealed class AfflictHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Afflict;

        public Condition? Inflicts(SpellEffect effect) => effect.Condition;

        // Invisible is the one condition a creature is glad of (Invisibility, Greater Invisibility)
        public bool Helps(SpellEffect effect) => effect.Condition == Condition.Invisible;

        public IReadOnlyList<string> Keys { get; } = new[]
        {
            "disarms", "pinned",
            // what it leaves on the creature, and how that ends
            "escape", "repeat_save", "on_damage", "while_in_zone", "ends_on_act", "shakeable", "flees",
            "permanent_after_rounds",
        };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.Condition == Condition.None) yield return "an afflict effect with no 'condition'";

            if (effect.Linger.OnDamage == OnDamage.EndsAtZero)
                yield return "'on_damage': 'ends_at_zero' is for a sway - a condition ends on damage with " +
                             "'ends' or 'ends_if_caster_side'";

            if (effect.Linger.OnDamage == OnDamage.SavesAgain && effect.Linger.RepeatSave == null)
                yield return "'on_damage': 'saves_again' needs a 'repeat_save' to make";
        }

        public Landing Apply(Contact c)
        {
            SpellEffect effect = c.Effect;
            Actor target = c.Target;
            Attempt attempt = c.Attempt;

            if (c.Resisted) return new Landing(effect, target, false, 0, attempt);

            bool applied = target.Apply(effect.Condition, c.Caster.Actor);

            // Fear: what it holds falls where it stands
            if (effect.Disarms && (applied || target.Has(effect.Condition)))
                target.Disarm(c.Fight?.Field.Where(target));

            if (applied && effect.Duration == Duration.Concentration)
                c.Magic.Remember(c.Caster.Actor, target, c.Spell.Id, effect.Condition);

            if (applied)
            {
                Placement placement = c.Magic.PlacementFor(c, effect.Duration);

                placement.Condition = effect.Condition;

                // the save that put it there counts: Sleep worsens on the second failure, Flesh to
                // Stone on the third
                placement.Fails = attempt != null && attempt.Kind == RollKind.Save && attempt.Failed ? 1 : 0;

                c.Magic.Place(placement, c.Fight);

                if (effect.Pinned) target.Pin(effect.Condition);

                c.Fight?.Changed(target, effect.Condition, true);
            }

            // SRD 5.2.1: an Incapacitated creature's concentration is broken
            if (applied && target.IsIncapacitated && target.IsConcentrating)
                c.Magic.Release(target);

            return new Landing(effect, target, applied, 0, attempt, effect.Condition);
        }
    }
}
