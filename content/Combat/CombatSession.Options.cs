using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Items;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Localization;
using Core.Magic;
using Core.Space;

namespace Content.Combat
{
    public sealed partial class CombatSession
    {
        // --- what the hero can do --------------------------------------------------------------------

        public IReadOnlyList<ActionOption> Options()
        {
            var options = new List<ActionOption>();

            if (!MyTurn) return options;

            // attacks, then spells, then the rest: the order is the hotkeys'
            options.AddRange(AttackOptions());
            options.AddRange(LightBonusOptions());
            options.AddRange(SpellOptions());
            options.AddRange(AgainOptions());
            options.AddRange(ManoeuvreOptions());
            options.AddRange(HeldOptions());
            options.AddRange(DoorOptions());
            options.AddRange(UnarmedOptions());
            options.AddRange(ShakeOptions());
            options.AddRange(ItemOptions());
            options.AddRange(FeatureOptions());
            options.AddRange(ShapeOptions());
            options.AddRange(FreeOptions());

            // 1 to 9 for the first nine that do something - attacks, then spells, then the rest
            int key = 1;

            foreach (ActionOption option in options.Where(o => o.Kind != OptionKind.EndTurn))
                option.Hotkey = key <= 9 ? key++ : 0;

            return options;
        }

        IEnumerable<ActionOption> AttackOptions() =>
            Hero.Attacks.Select(attack => new ActionOption
            {
                Id = "attack:" + attack.Id + (attack.Hand == Hand.Off ? OffHand : ""),
                Kind = OptionKind.Attack,
                NameKey = attack.NameKey,
                Cost = Spend.Action,
                Attack = attack,
                Targeting = Targeting.Creature,
                Enabled = WhyNotAttack(attack) == null,
                WhyNotKey = WhyNotAttack(attack),
            }).ToList();

        // an off-hand weapon's options say so in their id: a dagger in each hand is two options
        public const string OffHand = "@off";

        // "(off hand)", after the weapon's name on the bar
        public static readonly string OffHandKey = UiName("off_hand");

        // SRD 5.2.1 Light (p.89): after an attack with a Light weapon, the other Light weapon
        // attacks with the bonus action, without the ability modifier on its damage
        IEnumerable<ActionOption> LightBonusOptions()
        {
            if (!ReferenceEquals(Hero.LightTurn, Turn)) yield break;

            foreach (Attack other in Hero.Attacks.Where(a => a.Light && a.Hand != Hero.LightHand && a.Hand != Hand.None))
            {
                bool bonus = Turn.Can(Spend.Bonus);

                yield return new ActionOption
                {
                    Id = "bonus_attack:" + other.Id + (other.Hand == Hand.Off ? OffHand : ""),
                    Kind = OptionKind.Attack,
                    NameKey = other.NameKey,
                    Cost = Spend.Bonus,
                    Attack = Hero.LightBonusAttack(other),
                    Targeting = Targeting.Creature,
                    Enabled = bonus,
                    WhyNotKey = bonus ? null : Why("no_bonus"),
                };
            }
        }

        IEnumerable<ActionOption> SpellOptions()
        {
            if (Hero.Caster == null) yield break;

            // a spell a party of one can never cast isn't offered while the hero is alone (Solo)
            bool alone = !Fight.Actors.Any(a => a.Side == Hero.Actor.Side && !ReferenceEquals(a, Hero.Actor));

            foreach (Spell spell in Hero.Caster.Known.Where(s => !s.Answers && !(alone && s.Solo == Solo.Unavailable)))
            {
                string why = WhyNotSpell(spell);

                yield return new ActionOption
                {
                    Id = "spell:" + spell.Id,
                    Kind = OptionKind.Spell,
                    NameKey = spell.NameKey,
                    Cost = spell.CastingTime == Spend.Bonus ? Spend.Bonus : Spend.Action,
                    Spell = spell,
                    Targeting = TargetingOf(spell, false),
                    MaxTargets = MaxTargets(spell, spell.Level),
                    Modes = spell.Modes,
                    DamageChoices = spell.Effects.SelectMany(e => e.DamageChoices).Distinct().ToList(),
                    Enabled = why == null,
                    WhyNotKey = why,
                };
            }
        }

        // a held spell's later turns: Spiritual Weapon's strike, Moonbeam's move
        IEnumerable<ActionOption> AgainOptions()
        {
            if (Hero.Caster == null) yield break;

            foreach (Spell held in Hero.Caster.Known.Where(s => s.Repeat.HasValue &&
                                                               Battle.Magic.CanRepeat(Hero.Caster, s)))
            {
                Spend cost = held.Repeat == Spend.Bonus ? Spend.Bonus : Spend.Action;
                string why = Turn.Can(cost) ? null : Why(cost == Spend.Bonus ? "no_bonus" : "no_action");

                yield return new ActionOption
                {
                    Id = "again:" + held.Id,
                    Kind = OptionKind.Again,
                    NameKey = held.NameKey,
                    Cost = cost,
                    Spell = held,
                    Targeting = TargetingOf(held, true),
                    Enabled = why == null,
                    WhyNotKey = why,
                };
            }
        }

        IEnumerable<ActionOption> ManoeuvreOptions()
        {
            yield return Simple("dash", OptionKind.Dash, Spend.Action);
            yield return Simple("disengage", OptionKind.Disengage, Spend.Action);
            yield return Simple("hide", OptionKind.Hide, Spend.Action);

            // the Rogue's Cunning Action: the same three for a bonus action
            foreach ((string id, OptionKind kind, Manoeuvre m) in new[]
                     {
                         ("dash", OptionKind.Dash, Manoeuvre.Dash),
                         ("disengage", OptionKind.Disengage, Manoeuvre.Disengage),
                         ("hide", OptionKind.Hide, Manoeuvre.Hide),
                     })
                if (Hero.Actor.QuickOnBonus.HasFlag(m))
                    yield return Simple(id, kind, Spend.Bonus, "bonus_" + id);
        }

        // down or held: standing up, breaking free
        IEnumerable<ActionOption> HeldOptions()
        {
            Actor me = Hero.Actor;

            if (me.Has(Condition.Prone))
                yield return new ActionOption
                {
                    Id = "stand_up",
                    Kind = OptionKind.StandUp,
                    NameKey = UiName("stand_up"),
                    Cost = Spend.Movement,
                    Enabled = !me.IsPinned(Condition.Prone),
                    WhyNotKey = me.IsPinned(Condition.Prone) ? Why("pinned") : null,
                };

            if (me.Has(Condition.Restrained) || me.Has(Condition.Grappled))
                yield return Simple("break_free", OptionKind.BreakFree, Spend.Action);
        }

        // the Unarmed Strike's other two options (SRD 5.2.1 p.190): each one attack
        IEnumerable<ActionOption> UnarmedOptions()
        {
            if (Hero.Actor.IsShifted) yield break;

            foreach ((string id, OptionKind kind, string mode) in new[]
                     {
                         ("grapple", OptionKind.Grapple, ""),
                         ("shove", OptionKind.Shove, "push"),
                         ("shove_prone", OptionKind.Shove, "prone"),
                     })
            {
                string why = WhyNotAttack(Content.Sheet.Hero.UnarmedStrike);

                yield return new ActionOption
                {
                    Id = id,
                    Kind = kind,
                    NameKey = UiName(id),
                    Cost = Spend.Action,
                    Attack = Content.Sheet.Hero.UnarmedStrike,
                    Targeting = Targeting.Creature,
                    ShoveMode = mode,
                    Enabled = why == null,
                    WhyNotKey = why,
                };
            }
        }

        IEnumerable<ActionOption> ShakeOptions()
        {
            if (Fight.Field.Where(Hero.Actor) is Cell here &&
                Fight.Field.Adjacent(here).Any(a => Battle.Magic.CanBeShaken(a)))
                yield return Simple("shake", OptionKind.Shake, Spend.Action);
        }

        IEnumerable<ActionOption> ItemOptions() =>
            Hero.Pack.Stacks.Where(s => s.Item.Kind == ItemKind.Consumable && !s.Item.Heals.IsNothing)
                .Select(stack => new ActionOption
                {
                    Id = "item:" + stack.Item.Id,
                    Kind = OptionKind.Item,
                    NameKey = stack.Item.NameKey,
                    Cost = stack.Item.UseTime,
                    Item = stack.Item,
                    Enabled = Turn.Can(stack.Item.UseTime),
                    WhyNotKey = Turn.Can(stack.Item.UseTime) ? null : Why("no_bonus"),
                }).ToList();

        IEnumerable<ActionOption> FeatureOptions()
        {
            foreach (Feature feature in Hero.Activatable.Where(f => f.Trait == Trait.Stance ||
                                                                    f.Trait == Trait.Recovery ||
                                                                    f.Trait == Trait.Boost))
            {
                bool uses = Hero.UsesLeft(feature) > 0;

                // what it costs is the feature's: Rage a bonus action, Preserve Life an action,
                // Reckless Attack nothing (SRD 5.2.1)
                bool paid = Turn.Can(feature.UseTime);

                yield return new ActionOption
                {
                    Id = "feature:" + feature.Id,
                    Kind = OptionKind.Feature,
                    NameKey = feature.NameKey,
                    Cost = feature.UseTime,
                    Feature = feature,
                    Enabled = uses && paid,
                    WhyNotKey = !uses ? Why("no_uses")
                              : !paid ? Why(feature.UseTime == Spend.Bonus ? "no_bonus" : "no_action")
                              : null,
                };
            }
        }

        IEnumerable<ActionOption> ShapeOptions()
        {
            foreach (Form form in Hero.FormsOpen(_forms))
            {
                string refused = Hero.RefusesShape(form, Turn);

                yield return new ActionOption
                {
                    Id = "shape:" + form.Id,
                    Kind = OptionKind.Shape,
                    NameKey = form.NameKey,
                    Cost = Spend.Bonus,
                    Form = form,
                    Enabled = refused == null,
                    WhyNotKey = refused == null ? null : Why("no_uses"),
                };
            }
        }

        // A SHUT DOOR BESIDE THE HERO (cc_task_open-questions-answers.md 2.1): one option a door, free while the
        // turn's one object interaction is unspent and an action after it (SRD 5.2.1, Encounter.Doors.cs). Two
        // doors beside you are told apart by which side they are on
        IEnumerable<ActionOption> DoorOptions()
        {
            if (!(Fight.Field.Where(Hero.Actor) is Core.Space.Cell here)) yield break;

            Spend cost = Encounter.DoorCost(Turn);

            foreach (Core.Space.Border door in Fight.DoorsBeside(Hero.Actor))
            {
                string side = door.Vertical ? (door.Cell.X == here.X ? "west" : "east")
                                            : (door.Cell.Y == here.Y ? "north" : "south");

                string why = !Hero.Actor.CanAct ? Why("cannot_act") : Turn.Can(cost) ? null : Why("no_action");

                yield return new ActionOption
                {
                    Id = "open_door_" + side,
                    Kind = OptionKind.OpenDoor,
                    NameKey = UiName("open_door"),
                    Cost = cost,
                    Door = door,
                    Enabled = why == null,
                    WhyNotKey = why,
                };
            }
        }

        // what costs nothing: Action Surge, running for it, and ending the turn
        IEnumerable<ActionOption> FreeOptions()
        {
            if (Hero.Budget.ExtraActionsLeft > 0) yield return Free("surge", OptionKind.Surge);

            if (Fight.CanFlee(Turn)) yield return Free("flee", OptionKind.Flee);

            yield return Free("end_turn", OptionKind.EndTurn);
        }

        static ActionOption Free(string id, OptionKind kind) =>
            new ActionOption { Id = id, Kind = kind, NameKey = UiName(id), Cost = Spend.Free, Enabled = true };

        static string UiName(string id) => KeyConventions.Key(KeyConventions.UiNs, "combat_action", id, "name");

        public static IEnumerable<string> ActionKeys() =>
            new[] { "dash", "disengage", "hide", "bonus_dash", "bonus_disengage", "bonus_hide",
                    "stand_up", "break_free", "shake", "surge", "flee", "end_turn",
                    "grapple", "shove", "shove_prone", "open_door", "off_hand" }
                .Select(UiName)
                .Concat(Keys())
                .Concat(ReactionPolicies.Keys());

        ActionOption Simple(string id, OptionKind kind, Spend cost, string name = null)
        {
            string why = Turn.Can(cost) ? null : Why(cost == Spend.Bonus ? "no_bonus" : "no_action");

            if (!Hero.Actor.CanAct) why = Why("cannot_act");

            return new ActionOption
            {
                Id = (cost == Spend.Bonus ? "bonus_" : "") + id,
                Kind = kind,
                NameKey = UiName(name ?? id),
                Cost = cost,
                Enabled = why == null,
                WhyNotKey = why,
            };
        }

        string WhyNotAttack(Attack attack)
        {
            Actor me = Hero.Actor;

            if (!me.CanAct) return Why("cannot_act");

            if (!Turn.Can(Spend.Action) && Turn.Limited <= 0) return Why("no_action");

            if (!me.CanUse(attack)) return Why("no_target");

            if (!Fight.Field.Enemies(me).Any(e => !e.IsDown && Fight.Field.InRange(me, e, attack.Reaches)))
                return Why("out_of_range");

            return null;
        }

        string WhyNotSpell(Spell spell)
        {
            if (!Hero.Actor.CanAct) return Why("cannot_act");

            if (Hero.Actor.IsShifted) return Why("shifted");

            if (Hero.Actor.Boons.Forbids(Forbid.Casting)) return Why("cannot_cast");

            // Identify's minute, Raise Dead's hour: cast between fights, not in one
            if (spell.OutOfCombat) return Why("too_long");

            if (!Hero.Caster.CanCast(spell, spell.Level)) return Why("spent");

            Spend cost = spell.CastingTime == Spend.Bonus ? Spend.Bonus : Spend.Action;

            if (!Turn.Can(cost)) return Why(cost == Spend.Bonus ? "no_bonus" : "no_action");

            return null;
        }

        static Targeting TargetingOf(Spell spell, bool again)
        {
            IEnumerable<SpellEffect> effects = spell.Effects.Where(e => again ? e.Lands.Repeats() : e.Lands != Lands.OnRepeat);

            if (effects.Any(e => e.AimKind.IsDirected())) return Targeting.Direction;

            if (effects.Any(e => e.AimKind.NeedsATargetSquare() ||
                                 e.Kind == Primitive.Shift && e.AimKind == AimKind.Caster ||
                                 e.Kind == Primitive.Shift && e.AimKind == AimKind.Zone))
                return Targeting.Square;

            if (effects.Any(e => e.AimKind == AimKind.Creatures)) return Targeting.Creatures;

            if (effects.Any(e => e.AimKind == AimKind.Creature)) return Targeting.Creature;

            return Targeting.None;
        }

        static int MaxTargets(Spell spell, int castAt) =>
            spell.Effects.Where(e => e.AimKind == AimKind.Creatures)
                 .Select(e => e.TargetsAt(spell.Level, castAt))
                 .DefaultIfEmpty(1)
                 .Max();
    }
}
