using System;
using System.Collections.Generic;
using System.Linq;
using Content.Sheet;
using Core.Characters;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Combat
{
    public sealed partial class CombatSession
    {
        // --- previews ----------------------------------------------------------------------------

        public Preview Preview(Actor target) => Preview(new[] { target }, null);

        public Preview Preview(IReadOnlyList<Actor> targets, Cell? square)
        {
            if (Selected == null || !MyTurn) return new Preview { Legal = false, WhyNotKey = Why("no_target") };

            Actor first = targets?.FirstOrDefault(t => t != null);

            if (Selected.Kind == OptionKind.Attack) return AttackPreview(first);

            return Selected.Spell == null ? new Preview { Legal = true } : SpellPreview(Selected.Spell, targets, square);
        }

        Preview AttackPreview(Actor first)
        {
            Actor me = Hero.Actor;

            if (first == null) return new Preview { Legal = false, WhyNotKey = Why("no_target") };

            bool legal = LegalTargets().Contains(first);
            Attack attack = Selected.Attack;

            bool close = Fight.Field.Distance(me, first) <= 1;
            Advantage lean = Strike.Lean(me, first, close, Fight.Sees(me, first), Fight.Sees(first, me),
                                         Fight.Band(me, first, attack));
            int cover = Fight.Cover(me, first);

            double hit = AttackChance(attack.Modifier(me), first.ArmorClass + cover, lean);
            DiceRoll swing = attack.DamageFor(me);

            return new Preview
            {
                Legal = legal,
                WhyNotKey = legal ? null : Why("out_of_range"),
                Targets = new[] { first },
                HitChance = hit,
                Damage = swing,
                ExpectedDamage = hit * Math.Max(0, swing.Average),
            };
        }

        Preview SpellPreview(Spell spell, IReadOnlyList<Actor> targets, Cell? square)
        {
            Actor me = Hero.Actor;

            bool again = Selected.Kind == OptionKind.Again;
            List<SpellEffect> effects = spell.Effects
                                             .Where(e => again ? e.Lands.Repeats() : e.Lands != Lands.OnRepeat)
                                             .Where(e => e.InMode(Mode))
                                             .ToList();

            IReadOnlyList<Cell> template = Template(effects, square);

            IReadOnlyList<Actor> caught = Selected.Targeting == Targeting.Direction ||
                                          Selected.Targeting == Targeting.Square
                ? Fight.Field.Caught(template).ToList()
                : (targets ?? Array.Empty<Actor>()).Where(t => t != null).ToList();

            Actor about = caught.FirstOrDefault(a => a.Side != me.Side) ?? caught.FirstOrDefault();

            SpellEffect damage = effects.FirstOrDefault(e => e.Kind == Primitive.Damage);
            SpellEffect saved = effects.FirstOrDefault(e => e.Save.HasValue);
            SpellEffect rolled = effects.FirstOrDefault(e => e.AttackRoll);

            DiceRoll dice = damage?.AmountAt(spell.Level, Math.Max(CastAt, spell.Level), me.Level) ?? default;

            double hitChance = -1, fail = -1, expected = 0;

            if (about != null && rolled != null)
            {
                bool close = Fight.Field.Distance(me, about) <= 1;
                Advantage lean = Strike.Lean(me, about, close, Fight.Sees(me, about), Fight.Sees(about, me));
                hitChance = AttackChance(Hero.Caster.AttackModifierFor(spell), about.ArmorClass + Fight.Cover(me, about),
                                         lean);
                expected = hitChance * Math.Max(0, dice.Average);
            }
            else if (about != null && saved != null)
            {
                fail = SaveFailChance(about.SaveModifier(saved.Save.Value), Hero.Caster.SaveDcFor(spell),
                                      about.SaveAdvantage(saved.Save.Value));
                expected = Math.Max(0, dice.Average) *
                           (fail + (saved.OnSave == OnSave.Half ? (1 - fail) * 0.5 : 0));
            }
            else
            {
                expected = Math.Max(0, dice.Average);
            }

            // a template that catches no foe promises nothing - the HUD must not show a cone into
            // empty air as its full damage
            bool templated = Selected.Targeting == Targeting.Direction || Selected.Targeting == Targeting.Square;
            int enemies = templated
                ? caught.Count(a => a.Side != me.Side)
                : Math.Max(1, caught.Count(a => a.Side != me.Side));

            return new Preview
            {
                Legal = LegalAim(targets, square),
                Targets = caught,
                HitChance = hitChance,
                SaveDc = saved != null ? Hero.Caster.SaveDcFor(spell) : null,
                SaveAbility = saved?.Save,
                HalfOnSave = saved?.OnSave == OnSave.Half,
                FailChance = fail,
                Damage = dice,
                ExpectedDamage = expected * (Selected.Targeting == Targeting.Creature ? 1 : enemies),
                Template = template,
                Facing = Selected.Targeting == Targeting.Direction ? Facing : null,
            };
        }

        bool LegalAim(IReadOnlyList<Actor> targets, Cell? square)
        {
            switch (Selected.Targeting)
            {
                case Targeting.Creature:
                case Targeting.Creatures:
                    return targets != null && targets.Count > 0 && targets.All(t => LegalTargets().Contains(t));

                case Targeting.Square:
                    return square.HasValue && LegalSquares().Contains(square.Value);

                default:
                    return true;
            }
        }

        // the squares a spell's shape would cover for this aim - what the board lights up under
        // the mouse
        IReadOnlyList<Cell> Template(IReadOnlyList<SpellEffect> effects, Cell? square)
        {
            if (!(Fight.Field.Where(Hero.Actor) is Cell here)) return Array.Empty<Cell>();

            var cells = new List<Cell>();

            foreach (SpellEffect e in effects)
            {
                switch (e.AimKind)
                {
                    case AimKind.Line:
                        cells.AddRange(Fight.Field.Line(here, Facing, e.Length, Math.Max(1, e.Width)));
                        break;
                    case AimKind.Cone:
                        cells.AddRange(Fight.Field.Cone(here, Facing, e.Length));
                        break;
                    case AimKind.Cube:
                        cells.AddRange(Fight.Field.Cube(here, Facing, e.Length));
                        break;
                    case AimKind.Burst when square.HasValue:
                        cells.AddRange(Fight.Field.Burst(square.Value, e.Radius));
                        break;
                    case AimKind.Square when square.HasValue:
                        cells.AddRange(Fight.Field.Square(square.Value, Math.Max(1, e.Length)));
                        break;
                    case AimKind.Around:
                        cells.AddRange(Fight.Field.Burst(here, e.Radius));
                        break;
                    case AimKind.Place when square.HasValue && e.Handler.MakesAZone:
                        cells.AddRange(Fight.Field.Burst(square.Value, e.Radius));
                        break;
                }
            }

            return cells.Distinct().ToList();
        }

        // SRD: a natural 1 misses and a natural 20 hits, whatever the numbers
        public static double AttackChance(int modifier, int armorClass, Advantage advantage)
        {
            double p = Math.Clamp((21 - (armorClass - modifier)) / 20.0, 0.05, 0.95);

            return Lean(p, advantage);
        }

        // a save has no automatic success or failure in v1 (Attempt's rule)
        public static double SaveFailChance(int modifier, int dc, Advantage advantage)
        {
            double pass = Math.Clamp((21 - (dc - modifier)) / 20.0, 0, 1);

            return 1 - Lean(pass, advantage);
        }

        static double Lean(double p, Advantage advantage) => advantage switch
        {
            Advantage.Advantage => 1 - (1 - p) * (1 - p),
            Advantage.Disadvantage => p * p,
            _ => p,
        };
    }
}
