using System;
using Core.Characters;
using Core.Dice;
using Core.Resolution;

namespace Core.Rules
{
    // the front door for every d20 the game rolls at an actor. nothing else assembles a modifier:
    // if a bonus isn't visible here it isn't in the game, which is what makes the character sheet
    // and the roll agree by construction.
    public static class Checks
    {
        public static Attempt Check(IResolver resolver, Actor actor, Skill skill, int dc,
                                    Advantage extra = Advantage.Flat)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (actor == null) throw new ArgumentNullException(nameof(actor));

            return resolver.Resolve(RollKind.Check,
                                    actor.CheckModifier(skill) + Boonus(resolver, actor, skill),
                                    dc, actor.CheckAdvantageFor(skill.Governs(), skill).And(extra),
                                    actor);
        }

        public static Attempt Check(IResolver resolver, Actor actor, Skill skill,
                                    Difficulty difficulty, Advantage extra = Advantage.Flat) =>
            Check(resolver, actor, skill, difficulty.Dc(), extra);

        // a raw ability check - no skill named, so no proficiency, per SRD
        public static Attempt Check(IResolver resolver, Actor actor, Ability ability, int dc,
                                    Advantage extra = Advantage.Flat)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (actor == null) throw new ArgumentNullException(nameof(actor));

            return resolver.Resolve(RollKind.Check,
                                    actor.CheckModifier(ability) +
                                    Boonus(resolver, actor, Skill.None),
                                    dc, actor.CheckAdvantageFor(ability, Skill.None).And(extra),
                                    actor);
        }

        public static Attempt Save(IResolver resolver, Actor actor, Ability ability, int dc,
                                   Advantage extra = Advantage.Flat)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (actor == null) throw new ArgumentNullException(nameof(actor));

            // SRD: stunned and unconscious auto-fail STR and DEX saves. the roll still happens, so
            // the tray shows a die and the log records one - it just can't win.
            foreach (Condition condition in actor.Conditions)
            {
                if (!condition.AutoFailsSave(ability)) continue;

                return new Attempt(RollKind.Save, D20Roll.Fixed(1, 0), int.MaxValue);
            }

            int dice = 0;

            foreach (DiceRoll boon in actor.Boons.DiceOnSave(ability)) dice += resolver.Roll(boon, actor);

            return resolver.Resolve(RollKind.Save, actor.SaveModifier(ability) + dice, dc,
                                    actor.SaveAdvantage(ability).And(extra), actor);
        }

        // the solo delta: one d20, no modifiers, 10 or better and you are back up on 1 hit point
        public static Attempt DeathSave(IResolver resolver, Actor actor)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            Attempt attempt = resolver.Resolve(RollKind.Death, 0, Resolution.DeathSave.Dc,
                                               Advantage.Flat, actor);

            if (actor == null) return attempt;

            if (attempt.Succeeded)
            {
                actor.Health.Revive();
                actor.Remove(Condition.Unconscious);
            }
            else
            {
                actor.Perish();
            }

            return attempt;
        }

        // a Bless or a Guidance is +1d4, not +2 - it is rolled fresh at the moment it applies,
        // which is why it cannot live in the sheet's flat modifier
        static int Boonus(IResolver resolver, Actor actor, Skill skill)
        {
            int total = 0;

            foreach (DiceRoll boon in actor.Boons.DiceOnCheck(skill)) total += resolver.Roll(boon, actor);

            return total;
        }

        // the passive score a hidden thing is measured against - SRD 10 + the check modifier.
        // the GM rolls nothing for it, which is the point: no die on the table gives it away.
        public static int Passive(Actor actor, Skill skill) =>
            actor == null ? 10 : 10 + actor.CheckModifier(skill);
    }
}
