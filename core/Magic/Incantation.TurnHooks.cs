using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Magic
{
    // WHAT HAPPENS ON TURNS AND HURTS to what a spell left on a creature: a burning, a delayed
    // splash, a timed ending, Command's word, Fear's flight, the repeat save, a waking
    public sealed partial class Incantation
    {
        readonly HashSet<Encounter> _watching = new();

        internal void Watch(Encounter fight)
        {
            if (fight == null || !_watching.Add(fight)) return;

            fight.TurnStarting += turn => TurnStarts(fight, turn);
            fight.TurnEnding += turn => TurnEnds(fight, turn.Actor);
            fight.Damaged += (attacker, target, amount) => Hurt(fight, attacker, target);
            fight.Damaged += (attacker, target, amount) => Unveil(attacker, fight);
            fight.AttackRolled += attacker => Unveil(attacker, fight);
            fight.Moved += _ => Refresh(fight);
            fight.Stepped += _ => Refresh(fight);
        }

        // somebody's turn is starting: a thing that lasted until then ends; a thing that lasts
        // until the end of this turn is armed; a burning bites its bearer
        void TurnStarts(Encounter fight, Turn turn)
        {
            Actor whose = turn.Actor;

            foreach (Placement placed in _placed.ToList())
            {
                if (!_placed.Contains(placed)) continue;

                // Command: the turn is the word's, and then the word is spent
                if (placed.Command != Command.None && ReferenceEquals(placed.Target, whose))
                {
                    _placed.Remove(placed);

                    if (!turn.Ended && whose.CanAct) Obey(fight, turn, placed);

                    continue;
                }

                // Fear: a Dash away from the caster before anything else
                if (placed.Spec.Flees && ReferenceEquals(placed.Target, whose) && !turn.Ended &&
                    placed.Caster != null)
                {
                    if (fight.Dash(turn)) RunFrom(fight, turn, placed.Caster.Actor);
                }

                // SRD 5.2.1 Banishment: held for the full minute, a creature of another plane
                // doesn't come back, and one of this plane does - the spell is over either way
                if (placed.Spec.Gone is Gone gone && ReferenceEquals(placed.Target, whose) &&
                    fight.Round - placed.Since >= gone.AfterRounds)
                {
                    if (gone.TagRules.Give(whose, TagOutcome.Only))
                    {
                        _placed.RemoveAll(p => ReferenceEquals(p.Target, whose) && p.Spell == placed.Spell);
                        whose.Boons.EndFrom(placed.Spell);
                        fight.Dismiss(whose);
                    }
                    else
                    {
                        Lift(whose, placed.Spell, fight);
                    }

                    if (placed.Caster != null && placed.Caster.Actor.Concentrating == placed.Spell &&
                        !_placed.Any(p => p.Spell == placed.Spell && ReferenceEquals(p.Caster, placed.Caster)))
                        Release(placed.Caster.Actor);

                    continue;
                }

                // SRD 5.2.1 Flesh to Stone: held the full minute, the Petrified stays until Greater
                // Restoration ends it - the spell is over and dropping it no longer lifts it
                if (placed.Spec.PermanentAfterRounds > 0 && placed.Caster != null &&
                    ReferenceEquals(placed.Caster.Actor, whose) &&
                    fight.Round - placed.Since >= placed.Spec.PermanentAfterRounds &&
                    placed.Condition == Condition.Petrified && placed.Target.Has(Condition.Petrified))
                {
                    Actor stone = placed.Target;

                    _placed.RemoveAll(p => ReferenceEquals(p.Target, stone) && p.Spell == placed.Spell);
                    Forget(placed.Caster.Actor, stone, placed.Spell);

                    if (placed.Caster.Actor.Concentrating == placed.Spell) Release(placed.Caster.Actor);

                    continue;
                }

                // Finger of Death: the start of the caster's next turn, and the dead rise
                if (placed.RaisesAs.Length > 0 && placed.Caster != null &&
                    ReferenceEquals(placed.Caster.Actor, whose))
                {
                    _placed.Remove(placed);
                    fight.Raise(placed.Target, placed.RaisesAs, whose);
                    continue;
                }

                bool mine = ReferenceEquals(placed.Owner ?? placed.Target, whose);

                if (placed.Duration == Duration.NextTurn && mine)
                {
                    Drop(placed, fight);
                    continue;
                }

                if (placed.Duration == Duration.NextTurnEnd && mine) placed.Armed = true;

                if (!placed.Burns.IsNothing && ReferenceEquals(placed.Target, whose))
                {
                    int suffered = whose.Suffer(Math.Max(0, _resolver.Roll(placed.Burns, placed.Caster?.Actor)),
                                                placed.DamageType);

                    fight.Hurt(placed.Caster?.Actor, whose, suffered, placed.DamageType);

                    // SRD 5.2.1 Searing Smite: "takes 1d6 Fire damage and then makes a
                    // Constitution saving throw" - a success ends it. the save track of a burning
                    // comes here, right after the burn, and not at the end of the turn
                    if (placed.Spec.RepeatSave is RepeatSave track && _placed.Contains(placed) && !whose.IsDown &&
                        Checks.Save(_resolver, whose, track.Ability, placed.Dc, whose.AgainstSpells).Succeeded)
                        Lift(whose, placed.Spell, fight);
                }
            }

            Drift(fight, whose);
            Refresh(fight);

            Check(fight.Actors);
        }

        // the end of a creature's turn: a delayed splash lands, timed things end, and every spell
        // holding it that allows a save at the end of its turn gets one
        void TurnEnds(Encounter fight, Actor creature)
        {
            foreach (Placement placed in _placed.ToList())
            {
                if (!_placed.Contains(placed)) continue;

                bool mine = ReferenceEquals(placed.Owner ?? placed.Target, creature);

                if (placed.Duration == Duration.TurnEnd ||
                    placed.Duration == Duration.NextTurnEnd && placed.Armed && mine)
                {
                    if (!placed.Later.IsNothing)
                    {
                        int suffered = placed.Target.Suffer(
                            Math.Max(0, _resolver.Roll(placed.Later, placed.Caster?.Actor)), placed.DamageType);

                        fight.Hurt(placed.Caster?.Actor, placed.Target, suffered, placed.DamageType);
                    }

                    Drop(placed, fight);
                }
            }

            SaveAgain(fight, creature);

            Check(fight.Actors);
        }

        // SRD 5.2.1 Command's five words, on the creature's turn
        void Obey(Encounter fight, Turn turn, Placement word)
        {
            Actor me = turn.Actor;
            Actor caster = word.Caster?.Actor;

            switch (word.Command)
            {
                // toward the caster by the shortest route, the turn over once within 5 feet
                case Command.Approach:
                    {
                        if (caster == null || !(fight.Field.Where(caster) is Cell them)) break;

                        Cell? best = fight.Field.Reachable(me, turn.SquaresLeft)
                                          .OrderBy(p => Battlefield.Distance(p.Key, them))
                                          .ThenBy(p => p.Value)
                                          .ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X)
                                          .Select(p => (Cell?)p.Key)
                                          .FirstOrDefault();

                        if (best.HasValue &&
                            Battlefield.Distance(best.Value, them) < fight.Field.Distance(me, caster))
                            fight.Walk(turn, best.Value);

                        if (fight.Field.Distance(me, caster) <= 1) fight.Forfeit(turn);
                        break;
                    }

                // what it holds falls, and the turn is over
                case Command.Drop:
                    me.Disarm(fight.Field.Where(me));
                    fight.Forfeit(turn);
                    break;

                // the whole turn spent getting away by the fastest means: a Dash and a run
                case Command.Flee:
                    if (caster != null)
                    {
                        fight.Dash(turn);
                        RunFrom(fight, turn, caster);
                    }

                    fight.Forfeit(turn);
                    break;

                // Prone, and the turn is over
                case Command.Grovel:
                    if (me.Apply(Condition.Prone)) fight.Changed(me, Condition.Prone, true);
                    fight.Forfeit(turn);
                    break;

                // no move, no action, no bonus action
                case Command.Halt:
                    fight.Forfeit(turn);
                    break;
            }
        }

        // as far from the caster as the turn's movement goes, by the safest route: the square
        // with the fewest enemies beside it among the furthest
        internal static void RunFrom(Encounter fight, Turn turn, Actor from)
        {
            Actor me = turn.Actor;

            if (!(fight.Field.Where(from) is Cell them)) return;

            int now = fight.Field.Distance(me, from);

            Cell? away = fight.Field.Reachable(me, turn.SquaresLeft)
                              .Where(p => Battlefield.Distance(p.Key, them) > now)
                              .OrderByDescending(p => Battlefield.Distance(p.Key, them))
                              .ThenBy(p => fight.Field.Adjacent(p.Key)
                                                .Count(a => a.Side != me.Side && !a.IsDown))
                              .ThenBy(p => p.Value)
                              .ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X)
                              .Select(p => (Cell?)p.Key)
                              .FirstOrDefault();

            if (away.HasValue) fight.Walk(turn, away.Value);
        }

        // hit points came off a creature: a Sleep ends, a Charm Person ends if it was the
        // caster's side that did it, a Hideous Laughter saves again with advantage
        void Hurt(Encounter fight, Actor attacker, Actor target)
        {
            foreach (Placement placed in _placed.Where(p => ReferenceEquals(p.Target, target))
                                                .ToList())
            {
                if (!_placed.Contains(placed)) continue;

                OnDamage onDamage = placed.Spec.OnDamage;

                bool ends = onDamage == OnDamage.Ends ||
                            onDamage == OnDamage.EndsIfCastersSide && attacker != null &&
                            placed.Caster != null && attacker.Side == placed.Caster.Actor.Side ||
                            // Gaseous Form: it ends on a target that drops to 0 hit points
                            onDamage == OnDamage.EndsAtZero && target.IsDown;

                if (ends)
                {
                    Lift(target, placed.Spell, fight);
                    continue;
                }

                if (onDamage == OnDamage.SavesAgain && placed.Spec.RepeatSave != null)
                    SaveOnce(fight, placed, Advantage.Advantage);
            }

            Check(fight.Actors);
        }

        // the end of a creature's turn: every spell holding it that allows a repeat save gets one
        void SaveAgain(Encounter fight, Actor creature)
        {
            foreach (Placement hold in _placed.Where(p => ReferenceEquals(p.Target, creature) &&
                                                          p.SavesAtTurnEnd)
                                              .ToList())
            {
                if (!_placed.Contains(hold)) continue;

                // Fear: only a creature that ends its turn where it cannot see the caster
                if (hold.Spec.RepeatSave.OnlyUnseen && hold.Caster != null &&
                    fight.Field.CanSee(creature, hold.Caster.Actor)) continue;

                SaveOnce(fight, hold, Advantage.Flat);
            }
        }

        void SaveOnce(Encounter fight, Placement hold, Advantage extra)
        {
            Actor creature = hold.Target;
            RepeatSave track = hold.Spec.RepeatSave;

            // the same advantage on a save to end it
            if (hold.Condition != Condition.None && creature.AdvantageOnSaveAgainst(hold.Condition))
                extra = extra.And(Advantage.Advantage);

            extra = extra.And(creature.AgainstSpells);

            Attempt save = Checks.Save(_resolver, creature, track.Ability, hold.Dc, extra);

            if (save.Succeeded)
            {
                hold.Successes++;

                if (hold.Successes >= track.EndsAfter) Lift(creature, hold.Spell, fight);

                return;
            }

            hold.Fails++;

            // Sleep's second failure, Flesh to Stone's third: the condition gets worse and the
            // saving stops
            if (track.Worsens != Condition.None && hold.Fails >= track.WorsensAfter)
            {
                creature.Remove(hold.Condition);
                fight.Changed(creature, hold.Condition, false);

                creature.Apply(track.Worsens, hold.Caster?.Actor);
                fight.Changed(creature, track.Worsens, true);

                ReplaceThread(hold, track.Worsens);

                hold.Condition = track.Worsens;
                hold.Spec = hold.Spec with
                {
                    RepeatSave = null,
                    OnDamage = hold.Spec.OnDamage == OnDamage.SavesAgain ? OnDamage.Nothing : hold.Spec.OnDamage,
                };
            }
        }

        // the concentration thread follows a worsened condition, so letting go ends the new one
        void ReplaceThread(Placement hold, Condition now)
        {
            if (hold.Caster != null) _held.Swap(hold.Caster.Actor, hold.Target, hold.Spell, hold.Condition, now);
        }
    }
}
