using System;
using System.Collections.Generic;
using Core.Characters;

namespace Core.Magic
{
    // SWAY: a boon on the creature - Bless, Bane, Guidance, Shield's +5 AC, Haste - and the two
    // things a sway does that aren't a boon: Aid's raised maximum and Banishment's trip away
    public sealed class SwayHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Sway;

        // its own two, what the boon is (BoonSpecReader.Keys, read by that reader), and how it ends
        public IReadOnlyList<string> Keys { get; } = new[]
        {
            "banishes", "raises_maximum",
            "escape", "repeat_save", "gone", "on_damage", "while_in_zone",
        };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            // a sway puts on a boon, raises a maximum (Aid) or sends the target away
            // (Banishment) - and it has to do one of them
            if (!effect.Boon.DoesSomething && !effect.RaisesMaximum && !effect.Banishes)
                yield return "a sway of nothing - give it a boon ('flat', 'dice', 'leans', 'mark', " +
                             "'forbids'...), 'raises_maximum' or 'banishes'";

            if (effect.RaisesMaximum && effect.Amount.IsNothing)
                yield return "'raises_maximum' by how much? give it an 'amount'";

            if (effect.Linger.Escape != null && !effect.Banishes)
                yield return "'escape' on a sway is a way back from being sent away - it needs 'banishes'";

            if (effect.Linger.Gone != null && !effect.Banishes)
                yield return "'gone' is for a creature sent away - it needs 'banishes'";

            if (effect.Linger.OnDamage != OnDamage.Nothing && effect.Linger.OnDamage != OnDamage.EndsAtZero)
                yield return "a sway ends on damage only at 0 hit points, 'on_damage': 'ends_at_zero'";

            if (effect.Linger.WhileInZone && effect.AimKind != AimKind.Zone)
                yield return "'while_in_zone' on a sway is an aura - it reaches the 'zone'";

            if (effect.Linger.RepeatSave != null && !effect.Save.HasValue)
                yield return "a sway's 'repeat_save' repeats a save it began with - give it a 'save'";

            if (effect.Linger.RepeatSave?.Worsens is Condition worse && worse != Condition.None)
                yield return "a sway has no condition to worsen - 'worsens' is for an afflict";
        }

        public Landing Apply(Contact c)
        {
            SpellEffect effect = c.Effect;
            Actor target = c.Target;

            if (c.Resisted) return new Landing(effect, target, false, 0, c.Attempt);

            // Aid's 5, rolled once - the maximum and the current both go up by it
            int raise = effect.RaisesMaximum ? Math.Max(0, c.Resolver.Roll(c.Amount, c.Caster.Actor)) : 0;

            target.Boons.Add(BoonFor(c.Spell, effect, c.Caster, c.Aim, raise));

            if (raise > 0) target.Mend(raise);

            if (effect.Duration == Duration.Concentration)
                c.Magic.Remember(c.Caster.Actor, target, c.Spell.Id);

            // Slow's save at the end of each of its turns, Banishment's minute, Gaseous Form's end
            c.Magic.Place(c.Magic.PlacementFor(c, effect.Duration), c.Fight);

            // Banishment, Maze: off the board until the spell ends on it
            if (effect.Banishes) c.Fight?.Banish(target, c.Spell.Id);

            return new Landing(effect, target, true, raise > 0 ? raise : effect.Boon.Flat, c.Attempt);
        }

        // A SWAY'S BOON: the effect's BoonSpec, with only what happens at the cast added - the
        // skill or ability the caster chose, Shillelagh's die for the caster's tier, whose turn
        // ends it, and Aid's rolled raise
        internal static Boon BoonFor(Spell spell, SpellEffect effect, Caster caster, Aim aim, int raise = 0,
                                     string id = null)
        {
            BoonSpec spec = effect.Boon with
            {
                Duration = effect.Duration.Plain(),
                Skill = effect.Boon.SkillChoices.Count > 0 ? aim.Skill : effect.Boon.Skill,
                Ability = effect.Boon.AbilityChoices.Count > 0 ? aim.Ability : effect.Boon.Ability,
                Rewrite = effect.Boon.Rewrite?.For(caster.Ability,
                                                   SpellEffect.CantripTiers(caster.Actor.Level)),
            };

            // a mark is the caster's by definition - it pays out on *their* hits
            Actor owner = effect.Duration.OnCastersTurn() || spec.Mark != null ? caster.Actor : null;

            return Boon.Of(spec, id ?? spell.Id, spell.Id, owner, raise);
        }
    }
}
