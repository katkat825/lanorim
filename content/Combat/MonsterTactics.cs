using System;
using System.Collections.Generic;
using System.Linq;
using Content.Monsters;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Space;

namespace Content.Combat
{
    // WHAT A MONSTER DOES WITH ITS TURN when it has more than a weapon: a breath, a web, a spell.
    //
    // Still the "standard enemy AI" of decisions_checklist.md section 3 - no behaviour tree, no
    // per-monster script. It weighs each thing it could cast right now against what its best
    // attack would do, casts while that is the better use of an action, and hands the rest of the
    // turn to BasicTactics (approach and swing). Everything is scored in expected damage, ties go
    // by id, and every roll goes through the fight's own resolver, so the same seed plays the same
    // fight - which is what lets the sim test it.
    public sealed class MonsterTactics : ITactics
    {
        readonly Monster _monster;
        readonly Caster _caster;
        readonly Incantation _incantation;
        readonly BasicTactics _basic;

        public MonsterTactics(Monster monster, Caster caster, Incantation incantation)
        {
            _monster = monster ?? throw new ArgumentNullException(nameof(monster));
            _caster = caster;
            _incantation = incantation ?? throw new ArgumentNullException(nameof(incantation));
            _basic = new BasicTactics(monster.Attacks, monster.Instinct);
        }

        // what the last turn cast, in order - for the log and for tests
        public IReadOnlyList<Casting> Cast => _cast;

        readonly List<Casting> _cast = new List<Casting>();

        bool Is(Instinct flag) => (_monster.Instinct & flag) == flag;

        public void Take(Encounter fight, Turn turn)
        {
            if (fight == null || turn == null || turn.Ended) return;

            Actor me = turn.Actor;
            _cast.Clear();

            if (!me.CanAct)
            {
                fight.EndTurn();
                return;
            }

            // SRD: a spent recharge rolls its d6 at the start of the monster's turn
            _caster?.Recharge(fight.Resolver);

            // a craven thing that is bloodied runs, and does nothing else
            if (Is(Instinct.Craven) && me.Health.IsBloodied && Flee(fight, turn))
            {
                if (!turn.Ended) fight.EndTurn();
                return;
            }

            if (_caster != null)
            {
                var tried = new HashSet<string>();

                // actions first, then the bonus action - a spell for each while one beats a swing
                while (!turn.Ended && !fight.Over && turn.Can(Spend.Action) &&
                       CastBest(fight, turn, CastingTime.Action, tried))
                {
                }

                if (!turn.Ended && !fight.Over && turn.Can(Spend.Bonus))
                    CastBest(fight, turn, CastingTime.BonusAction, tried);
            }

            if (turn.Ended || fight.Over) return;

            _basic.Take(fight, turn);
        }


        // --- running ------------------------------------------------------------------------------

        // a Dash and as far from every enemy as it can get
        bool Flee(Encounter fight, Turn turn)
        {
            Actor me = turn.Actor;
            List<Actor> enemies = fight.Field.Enemies(me).Where(a => !a.IsDown).ToList();

            if (enemies.Count == 0) return false;

            fight.Dash(turn);

            Cell? away = fight.Field.Reachable(me, turn.SquaresLeft)
                              .OrderByDescending(p => enemies.Min(e => fight.Field.Where(e) is Cell c
                                                                         ? Battlefield.Distance(p.Key, c)
                                                                         : 0))
                              .ThenBy(p => p.Value)
                              .ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X)
                              .Select(p => (Cell?)p.Key)
                              .FirstOrDefault();

            if (!away.HasValue) return false;

            fight.Walk(turn, away.Value);
            return true;
        }


        // --- casting ------------------------------------------------------------------------------

        sealed class Plan
        {
            public Spell Spell;
            public Aim Aim;
            public double Value;
        }

        bool CastBest(Encounter fight, Turn turn, CastingTime time, HashSet<string> tried)
        {
            Actor me = turn.Actor;

            double swing = time == CastingTime.Action ? BestSwing(fight, me) : 0;

            Plan best = null;

            foreach (Spell spell in _caster.Known.Where(s => s.CastingTime == time && !s.Answers &&
                                                            !tried.Contains(s.Id))
                                           .OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                if (!_caster.CanCast(spell, spell.Level)) continue;

                // a concentration spell does not drop a better one it is already holding
                if (spell.Concentration && me.IsConcentrating) continue;

                Plan plan = PlanFor(fight, me, spell);

                if (plan == null || plan.Value <= 0) continue;

                if (best == null || plan.Value > best.Value) best = plan;
            }

            if (best == null || best.Value <= swing) return false;

            tried.Add(best.Spell.Id);

            // a special action carries its own printed DC
            int? was = _caster.FixedDc;
            MonsterAction action = _monster.ActionFor(best.Spell.Id);

            if (action != null && action.Dc > 0) _caster.FixedDc = action.Dc;

            Casting casting = _incantation.Cast(_caster, best.Spell, best.Aim, best.Spell.Level,
                                                fight, turn);

            _caster.FixedDc = was;

            if (casting.Cast) _cast.Add(casting);

            return casting.Cast;
        }

        // what one swing of its best attack is worth right now, from where it stands - or what it
        // would be worth after walking up, halved, because walking up is not free
        double BestSwing(Encounter fight, Actor me)
        {
            double best = 0;

            foreach (Actor enemy in fight.Field.Enemies(me).Where(a => !a.IsDown))
                foreach (Attack attack in _monster.Attacks.Where(me.CanUse))
                {
                    double value = attack.DamageFor(me).Average * 0.65;

                    if (!fight.Field.InRange(me, enemy, attack.Reaches)) value *= 0.5;

                    best = Math.Max(best, value);
                }

            return best * Math.Max(1, _monster.Multiattack);
        }

        Plan PlanFor(Encounter fight, Actor me, Spell spell)
        {
            // a spell that needs a choice the monster cannot sensibly make is left alone
            if (spell.Modes.Count > 0 || spell.Strikes ||
                spell.Effects.Any(e => e.ChosenSkill || e.ChosenAbility))
                return null;

            Cell? here = fight.Field.Where(me);

            if (!here.HasValue) return null;

            List<Actor> enemies = fight.Field.Enemies(me).Where(a => !a.IsDown).ToList();

            if (enemies.Count == 0) return null;

            int range = Math.Max(1, spell.RangeAt(me.Level));

            // healing: the most hurt friend in reach, itself included, when anyone is bloodied
            if (spell.Does(Primitive.Heal) && !spell.Does(Primitive.Damage))
            {
                Actor hurt = fight.Actors.Where(a => a.Side == me.Side && !a.IsDead &&
                                                     a.Health.IsBloodied &&
                                                     (ReferenceEquals(a, me) ||
                                                      fight.Field.InRange(me, a, range)))
                                  .OrderBy(a => a.Health.Current)
                                  .ThenBy(a => a.Id, StringComparer.Ordinal)
                                  .FirstOrDefault();

                if (hurt == null) return null;

                return new Plan
                {
                    Spell = spell,
                    Aim = Aim.At(hurt),
                    Value = Average(spell, Primitive.Heal) * 0.8,
                };
            }

            double perTarget = Average(spell, Primitive.Damage) * 0.6 +
                               (spell.Does(Primitive.Afflict) ? 5 : 0);

            if (perTarget <= 0) return null;

            SpellEffect shape = spell.Effects.FirstOrDefault(e => e.Reach.IsArea()) ??
                                spell.Effects.First();

            switch (shape.Reach)
            {
                case Reach.Line:
                case Reach.Cone:
                case Reach.Cube:
                {
                    Plan best = null;

                    foreach (Facing facing in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
                    {
                        IEnumerable<Cell> swept = shape.Reach switch
                        {
                            Reach.Line => fight.Field.Line(here.Value, facing, shape.Length,
                                                           Math.Max(1, shape.Width)),
                            Reach.Cone => fight.Field.Cone(here.Value, facing, shape.Length),
                            _ => fight.Field.Cube(here.Value, facing, shape.Length),
                        };

                        double value = Score(fight.Field.Caught(swept).ToList(), me) * perTarget;

                        if (best == null || value > best.Value)
                            best = new Plan { Spell = spell, Aim = Aim.Toward(facing), Value = value };
                    }

                    return best;
                }

                case Reach.Burst:
                case Reach.Square:
                case Reach.Place:
                {
                    Plan best = null;

                    foreach (Actor enemy in enemies.OrderBy(a => a.Id, StringComparer.Ordinal))
                    {
                        if (!(fight.Field.Where(enemy) is Cell at)) continue;

                        if (Battlefield.Distance(here.Value, at) > range ||
                            !fight.Field.CanSee(here.Value, at))
                            continue;

                        IEnumerable<Actor> caught = shape.Reach == Reach.Square
                            ? fight.Field.Caught(fight.Field.Square(at, Math.Max(1, shape.Length)))
                            : fight.Field.Caught(at, shape.Radius);

                        double value = Score(caught.ToList(), me) * perTarget;

                        if (best == null || value > best.Value)
                            best = new Plan { Spell = spell, Aim = Aim.On(at), Value = value };
                    }

                    return best;
                }

                case Reach.Around:
                {
                    List<Actor> caught = fight.Field.Caught(here.Value, shape.Radius)
                                              .Where(a => !ReferenceEquals(a, me)).ToList();

                    return new Plan
                    {
                        Spell = spell,
                        Aim = Aim.Nothing,
                        Value = Score(caught, me) * perTarget,
                    };
                }

                case Reach.Creature:
                case Reach.Creatures:
                {
                    // the one it can reach: the weakest, when it is a finisher, else the nearest
                    Actor target = enemies.Where(e => fight.Field.InRange(me, e, range) &&
                                                      fight.Sees(me, e) &&
                                                      !me.HasFrom(Condition.Charmed, e) &&
                                                      !AlreadyHas(e, spell))
                                          .OrderBy(e => Is(Instinct.Finisher) ? e.Health.Current : 0)
                                          .ThenBy(e => fight.Field.Distance(me, e))
                                          .ThenBy(e => e.Id, StringComparer.Ordinal)
                                          .FirstOrDefault();

                    if (target == null) return null;

                    return new Plan { Spell = spell, Aim = Aim.At(target), Value = perTarget };
                }

                default:
                    return null;
            }
        }

        // enemies caught count for it, friends caught against it - twice, so a monster does not
        // breathe fire on its own pack for one extra enemy
        static double Score(IReadOnlyList<Actor> caught, Actor me) =>
            caught.Count(a => a.Side != me.Side && !a.IsDown) -
            2.0 * caught.Count(a => a.Side == me.Side && !ReferenceEquals(a, me));

        // a condition spell on something that already has the condition wastes the action
        static bool AlreadyHas(Actor target, Spell spell) =>
            spell.Effects.Where(e => e.Kind == Primitive.Afflict)
                 .Any(e => target.Has(e.Condition)) &&
            !spell.Does(Primitive.Damage);

        static double Average(Spell spell, Primitive kind) =>
            spell.Effects.Where(e => e.Kind == kind)
                 .Sum(e => Math.Max(e.Amount.Average, 0) +
                           (e.AddsModifier ? 3 : 0));

        public override string ToString() => $"{_monster.Id}: casts and swings";
    }
}
