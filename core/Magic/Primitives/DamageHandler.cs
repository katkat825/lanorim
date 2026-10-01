using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Core.Magic
{
    // DAMAGE: hit points off, by damage type - and what rides on a damaging spell: a kill line, dust,
    // the dead rising, a leap, extra dice, a burning each turn
    public sealed class DamageHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Damage;

        // a burning: the only thing that lands on each turn, or at the end of the next
        public bool Allows(string setting) => setting is "add_modifier" or "each_turn" or "next_turn_end";

        public IReadOnlyList<string> Keys { get; } = new[]
        {
            "slays_at_or_below", "dust", "raises", "leaps", "extra_dice", "reverts_shape",
            "reaction_flee", "near_first", "near_zone", "within_zone",
            // a burning's save track (Searing Smite)
            "repeat_save",
        };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.Amount.IsNothing) yield return "a damage effect with no 'amount'";

            if (effect.DamageType == DamageType.None && !effect.ChosenDamageType)
                yield return "damage with no 'damage_type' - every hit is typed, because resistance is " +
                             "read off the type";

            if (effect.Leaps > 0 && !effect.AttackRoll) yield return "'leaps' is for damage with an attack roll";

            if (effect.OnSave == OnSave.OnSuccess)
                yield return "damage that lands only on a successful save - did you mean 'half'?";

            if (effect.Linger.RepeatSave != null && effect.Lands != Lands.EachTurn)
                yield return "a 'repeat_save' on damage ends a burning - it needs 'lands': 'each_turn'";

            if (effect.Lands == Lands.EachTurn && effect.Duration == Duration.Instant)
                yield return "damage that lands each turn needs a 'duration'";

            if (effect.ExtraDice is ExtraDice extra &&
                (extra.Dice.IsNothing || extra.TagRules.Count > 0 == !string.IsNullOrEmpty(extra.Setting)))
                yield return "'extra_dice' has 'dice', and 'tag_rules' (tags on the target) or 'setting' " +
                             "(a tag on the fight), not both";

            if (effect.Raises != null && string.IsNullOrEmpty(effect.Raises.As))
                yield return "'raises' needs the statblock it rises 'as'";
        }

        // in order: does it land at all, does it kill outright, does it burn later; then the roll,
        // what lessens it, what rides on the hit, and what comes after the hurt
        public Landing Apply(Contact c)
        {
            SpellEffect effect = c.Effect;
            Actor target = c.Target;

            // a missed attack roll deals nothing, the same as a negating save. SRD 5.2.1 Potent
            // Cantrip (p.82): a damaging cantrip that misses, or is saved against, still does half
            // its damage and nothing else
            bool potent = !c.Landed && c.Spell.IsCantrip && c.Caster.Actor.Is("potent_cantrip") &&
                          (effect.AttackRoll || effect.OnSave == OnSave.Negates);

            if (!c.Landed && !potent && (effect.AttackRoll || effect.OnSave == OnSave.Negates))
                return new Landing(effect, target, false, 0, c.Attempt);

            DamageType type = effect.ChosenDamageType ? c.Aim.DamageType : effect.DamageType;

            // Shield: "you take no damage from Magic Missile"
            if (target.Boons.Wards(c.Spell.Id))
                return new Landing(effect, target, false, 0, c.Attempt);

            if (effect.SlaysAtOrBelow > 0 && !target.IsDown && target.Health.Current <= effect.SlaysAtOrBelow)
                return Slay(c);

            if (effect.Lands == Lands.EachTurn || effect.Lands == Lands.NextTurnEnd)
                return Later(c, type);

            int rolled = Lessened(c, Rolled(c), potent);

            int suffered = target.Suffer(rolled, type);

            c.Fight?.Observer.Dealt(new Harm(c.Caster.Actor, target, c.Spell.NameKey,
                                             Critical(c) ? c.Amount.Doubled() : c.Amount, rolled, suffered, type));

            suffered += MarksPaid(c);

            if (c.Fight != null && suffered > 0) c.Fight.Hurt(c.Caster.Actor, target, suffered, type, Critical(c), rolled);

            After(c, suffered);

            // a save-for-half that was made still landed *something*, and the report has to say
            // so or the log reads as a miss
            return new Landing(effect, target,
                               c.Landed || effect.OnSave == OnSave.Half || suffered > 0,
                               suffered, c.Attempt);
        }

        // Power Word Kill: at or below the line, no damage roll - it simply dies. a Death Ward
        // stops it, and is spent doing so
        static Landing Slay(Contact c)
        {
            Actor target = c.Target;
            Boon ward = target.Boons.DeathWard;

            if (ward != null)
            {
                target.Boons.Remove(ward);
                return new Landing(c.Effect, target, false, 0, c.Attempt);
            }

            int had = target.Health.Current;

            target.Suffer(had, DamageType.None);
            if (target.Side == Allegiance.Hero) target.Perish();

            return new Landing(c.Effect, target, true, had, c.Attempt);
        }

        // Searing Smite's burning, Vitriolic Sphere's second splash: nothing now, a placement
        // the target's turns will settle
        static Landing Later(Contact c, DamageType type)
        {
            SpellEffect effect = c.Effect;
            bool later = effect.Lands == Lands.NextTurnEnd;

            c.Magic.Place(new Placement
            {
                Target = c.Target,
                Caster = c.Caster,
                Spell = c.Spell.Id,
                Level = c.CastAt,
                Dc = c.Dc,
                Duration = later ? Duration.NextTurnEnd : effect.Duration,
                Burns = later ? default : c.Amount,
                Later = later ? c.Amount : default,
                DamageType = type,
                Spec = effect.Linger,
            }, c.Fight);

            if (effect.Duration == Duration.Concentration)
                c.Magic.Remember(c.Caster.Actor, c.Target, c.Spell.Id);

            return new Landing(effect, c.Target, true, 0, c.Attempt);
        }

        // the roll, and what adds to it: a critical, extra dice, Empowered Evocation
        static int Rolled(Contact c)
        {
            Actor caster = c.Caster.Actor;

            int rolled = Math.Max(0, c.Resolver.Roll(c.Amount, caster, out IReadOnlyList<int> faces)) + c.Modifier;

            c.Magic.LastFaces = faces;

            bool critical = Critical(c);

            if (critical) rolled += Math.Max(0, c.Resolver.Roll(c.Amount, caster));

            // Divine Smite's extra die against a fiend or an undead, Call Lightning's in a storm
            if (c.Effect.ExtraDice is ExtraDice extra && extra.Applies(c.Target, c.Fight?.Setting))
                rolled += Math.Max(0, c.Resolver.Roll(critical ? extra.Dice.Doubled() : extra.Dice, caster));

            // SRD 5.2.1 Empowered Evocation (p.82): the Intelligence modifier on one damage roll
            // of an Evocation spell - the first it rolls
            if (c.Spell.School == School.Evocation && caster.Is("empowered_evocation") &&
                !c.Magic.EmpoweredThisCast)
            {
                rolled += Math.Max(0, caster.AbilityModifier(c.Caster.Ability));
                c.Magic.EmpoweredThisCast = true;
            }

            return rolled;
        }

        // SRD: a critical doubles a spell's damage dice the same as a weapon's - and a smite's
        // dice are the hit's, so its critical doubles them too
        static bool Critical(Contact c) =>
            c.Attempt != null && c.Attempt.IsCritical ||
            c.Answering?.Trigger == Trigger.Struck && c.Answering.Attempt != null && c.Answering.Attempt.IsCritical;

        // what takes it down: Evasion, a save for half, a potent cantrip's miss
        static int Lessened(Contact c, int rolled, bool potent)
        {
            SpellEffect effect = c.Effect;

            // SRD 5.2.1 Evasion (p.63): a Dexterity save for half is none on a success and half on
            // a failure - while the creature can act
            bool evades = effect.Save == Ability.Dexterity && effect.OnSave == OnSave.Half &&
                          c.Target.Is("evasion") && !c.Target.IsIncapacitated;

            if (evades) rolled = c.Landed ? rolled / 2 : 0;
            else if (!c.Landed && effect.OnSave == OnSave.Half) rolled /= 2;

            if (potent) rolled /= 2;

            return rolled;
        }

        // a mark pays out on any attack roll that hits, a spell's included
        static int MarksPaid(Contact c)
        {
            if (!c.Effect.AttackRoll || !c.Landed) return 0;

            int suffered = 0;

            foreach (Boon mark in c.Target.Boons.MarksFrom(c.Caster.Actor).ToList())
            {
                DiceRoll dice = c.Attempt.IsCritical ? mark.Spec.Mark.Dice.Doubled() : mark.Spec.Mark.Dice;
                int more = Math.Max(0, c.Resolver.Roll(dice, c.Caster.Actor));

                suffered += c.Target.Suffer(more, mark.Spec.Mark.Type);
            }

            return suffered;
        }

        // what the hurt brings: dust, a shape lost, the dead rising, a flight
        static void After(Contact c, int suffered)
        {
            SpellEffect effect = c.Effect;
            Actor target = c.Target;
            Encounter fight = c.Fight;
            Attempt attempt = c.Attempt;

            // Disintegrate: brought to 0 by it, the creature is gray dust
            if (effect.Dust && suffered > 0 && target.Health.Current <= 0)
            {
                target.TurnToDust();
                if (target.Side == Allegiance.Hero) target.Perish();
            }

            // Moonbeam: a failed save turns a shape-shifted creature back, and it can't shift
            // again until it is out of the beam
            if (effect.RevertsShape && attempt != null && attempt.Failed && target.IsShifted)
            {
                target.Revert();
                target.Boons.Add(Boon.Of(new BoonSpec
                {
                    Duration = Duration.Concentration,
                    Forbids = Forbid.Shifting
                },
                                         Incantation.RevertedId(c.Spell.Id), c.Spell.Id));
            }

            // Finger of Death: a Humanoid it kills rises at the start of the caster's next turn,
            // on the caster's side
            if (effect.Raises != null && fight != null && suffered > 0 &&
                effect.Raises.TagRules.Touch(target) &&
                (target.IsDead || target.IsDown && target.Side != Allegiance.Hero))
                c.Magic.Place(new Placement
                {
                    Target = target,
                    Caster = c.Caster,
                    Spell = c.Spell.Id,
                    Level = c.CastAt,
                    Duration = Duration.Encounter,
                    RaisesAs = effect.Raises.As,
                }, fight);

            // Dissonant Whispers: a failed save sends it running on its own reaction
            if (effect.ReactionFlee && attempt != null && attempt.Failed && fight != null &&
                !target.IsDown && target.CanAct && fight.TakeReaction(target))
                Incantation.RunFrom(fight, new Turn(target, fight.BudgetFor(target), fight.Round),
                                    c.Caster.Actor);

            // Chromatic Orb: two matching dice and it leaps (the leap is resolved by the caller,
            // which has the other targets)
        }
    }
}
