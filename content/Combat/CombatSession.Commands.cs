using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Combat
{
    public sealed partial class CombatSession
    {
        // --- doing it ----------------------------------------------------------------------------

        public ActionResult Confirm(Actor target) =>
            Confirm(target == null ? Array.Empty<Actor>() : new[] { target }, null);

        public ActionResult Confirm(Cell square) => Confirm(Array.Empty<Actor>(), square);

        // a line, a cone, or anything with nothing to aim at
        public ActionResult Confirm() => Confirm(Array.Empty<Actor>(), null);

        public ActionResult Confirm(IReadOnlyList<Actor> targets, Cell? square)
        {
            if (!MyTurn) return ActionResult.No(Why("not_on_your_turn"));

            ActionOption option = Selected;

            if (option == null) return ActionResult.No(Why("no_target"));

            ActionResult result = Do(option, targets ?? Array.Empty<Actor>(), square);

            if (result.Done)
            {
                Selected = null;
                Mode = null;
                DamageType = DamageType.None;

                Battle.Magic.Check(Fight.Actors);
                Fight.Judge();

                if (Fight.Over) Phase = SessionPhase.Over;
                else if (MyTurn) Phase = SessionPhase.Choosing;
            }

            return result;
        }

        // pick and do in one - what a hotkey on an option with nothing to aim at does
        public ActionResult Take(ActionOption option)
        {
            if (option?.Kind == OptionKind.EndTurn) return EndTurn();

            if (!Select(option)) return ActionResult.No(option?.WhyNotKey ?? Why("no_target"));

            return Confirm();
        }

        ActionResult Do(ActionOption option, IReadOnlyList<Actor> targets, Cell? square) => option.Kind switch
        {
            OptionKind.Attack => DoAttack(option, targets),
            OptionKind.Spell or OptionKind.Again => DoCast(option, targets, square),
            OptionKind.Dash => Done(Fight.Dash(Turn, option.Cost)),
            OptionKind.Disengage => Done(Fight.Disengage(Turn, option.Cost)),
            OptionKind.Hide => DoHide(option),
            OptionKind.Grapple or OptionKind.Shove => DoWrestle(option, targets),
            OptionKind.StandUp => Done(Turn.StandUp(), "rooted"),
            OptionKind.BreakFree => DoBreakFree(),
            OptionKind.Shake => DoShake(targets),
            OptionKind.Item => Done(Hero.Use(option.Item.Id, Fight.Resolver, Turn) >= 0, "no_bonus"),
            OptionKind.Feature => DoFeature(option.Feature),
            OptionKind.Shape => Done(Hero.Shift(option.Form, Turn), "no_uses"),
            OptionKind.Surge => Done(Turn.Surge(), "no_surge"),
            OptionKind.Flee => Done(Fight.Flee(Turn), "not_an_edge"),
            OptionKind.EndTurn => EndTurn(),
            _ => ActionResult.No(Why("no_target")),
        };

        ActionResult DoAttack(ActionOption option, IReadOnlyList<Actor> targets)
        {
            Actor target = targets.FirstOrDefault();

            if (target == null) return ActionResult.No(Why("no_target"));

            Blow blow = Hero.Hit(Fight, Turn, target, option.Attack, spend: option.Cost);

            return blow == null ? ActionResult.No(Why("out_of_range")) : new ActionResult { Done = true, Blow = blow };
        }

        ActionResult DoCast(ActionOption option, IReadOnlyList<Actor> targets, Cell? square)
        {
            Aim aim = AimFor(option, targets, square);

            if (option.Modes.Count > 0 && string.IsNullOrEmpty(Mode))
                return ActionResult.No(Why("choose_mode"));

            Casting casting = option.Kind == OptionKind.Again
                ? Battle.Magic.Again(Hero.Caster, option.Spell, aim, Turn, Fight)
                : Battle.Magic.Cast(Hero.Caster, option.Spell, aim, CastAt, Fight, Turn);

            if (!casting.Cast) return new ActionResult { Done = false, WhyNotKey = Why("no_target"), Casting = casting };

            Hero.Receive(casting, _shelf);

            return new ActionResult { Done = true, Casting = casting };
        }

        ActionResult DoHide(ActionOption option)
        {
            // SRD 5.2.1: not while an enemy can see you
            if (!Fight.CanHide(Hero.Actor)) return ActionResult.No(Why("seen"));

            Attempt hid = Fight.Hide(Turn, option.Cost);
            return hid == null ? ActionResult.No(Why("no_action")) : new ActionResult { Done = true, Attempt = hid };
        }

        ActionResult DoWrestle(ActionOption option, IReadOnlyList<Actor> targets)
        {
            Actor target = targets.FirstOrDefault();

            if (target == null) return ActionResult.No(Why("no_target"));

            Attempt save = option.Kind == OptionKind.Grapple
                ? Fight.Grapple(Turn, target, option.Attack)
                : Fight.ShoveAway(Turn, target, option.ShoveMode == "prone", option.Attack);

            return save == null
                ? ActionResult.No(Why("out_of_range"))
                : new ActionResult { Done = true, Attempt = save };
        }

        ActionResult DoBreakFree()
        {
            Actor me = Hero.Actor;

            // a grapple made with hands is escaped from the fight's own record
            if (Fight.IsHeldByGrapple(me) && !me.Has(Condition.Restrained))
            {
                Attempt free = Fight.EscapeGrapple(Turn);

                return free == null
                    ? ActionResult.No(Why("nothing_to_break"))
                    : new ActionResult { Done = true, Attempt = free };
            }

            Condition held = me.Has(Condition.Restrained) ? Condition.Restrained : Condition.Grappled;
            Attempt escape = Battle.Magic.BreakFree(Fight, Turn, held);

            return escape == null
                ? ActionResult.No(Why("nothing_to_break"))
                : new ActionResult { Done = true, Attempt = escape };
        }

        ActionResult DoShake(IReadOnlyList<Actor> targets)
        {
            Actor sleeper = targets.FirstOrDefault() ??
                            (Fight.Field.Where(Hero.Actor) is Cell here
                                 ? Fight.Field.Adjacent(here).FirstOrDefault(a => Battle.Magic.CanBeShaken(a))
                                 : null);

            return Done(Battle.Magic.Shake(Fight, Turn, sleeper), "nobody_to_shake");
        }

        ActionResult DoFeature(Feature feature)
        {
            if (Hero.UsesLeft(feature) <= 0) return ActionResult.No(Why("no_uses"));

            if (!Turn.Take(feature.UseTime))
                return ActionResult.No(Why(feature.UseTime == Spend.Bonus ? "no_bonus" : "no_action"));

            bool invoked = Hero.Invoke(feature, Fight.Resolver);

            // Adrenaline Rush: the bonus action is a Dash too
            if (invoked && feature.Trait == Trait.Boost) Turn.Hasten();

            return Done(invoked, "no_uses");
        }

        static ActionResult Done(bool done, string why = "no_action") =>
            done ? new ActionResult { Done = true } : ActionResult.No(Why(why));

        Aim AimFor(ActionOption option, IReadOnlyList<Actor> targets, Cell? square)
        {
            var aim = new Aim(targets, square,
                              option.Targeting == Targeting.Direction ? Facing : (Facing?)null,
                              mode: Mode, damageType: DamageType)
            {
                Side = Side,
            };

            return aim;
        }
    }
}
