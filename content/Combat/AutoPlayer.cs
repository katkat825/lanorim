using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Content.Classes;
using Core.Combat;
using Core.Space;

namespace Content.Combat
{
    // A PLAYER WHO PLAYS THE PLAIN, SENSIBLE WAY, through the same CombatSession a person uses: switch
    // on a stance (Rage) at the start, drink or heal when low, otherwise take whatever the previews
    // say does the most damage - a sword, a cantrip, a slot - and walk in when nothing reaches.
    // For the balance sim and headless play-throughs; nothing in the game plays the hero with it.
    // Not clever on purpose: it measures the numbers, not tactics.
    public sealed class AutoPlayer
    {
        // hit points at or under this share of the maximum and it heals first
        public double LowAt { get; init; } = 1.0 / 3;

        // saves its leveled spells for when they out-damage a swing by this much
        public double SpellMargin { get; init; } = 1.5;

        // what it decided and why, for the sim's trace
        public Action<string> Note { get; init; }

        public Outcome Play(CombatSession session, int roundLimit = 60)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            if (session.Phase == SessionPhase.Waiting && session.Turn == null) session.Start();

            while (session.Phase != SessionPhase.Over && session.Fight.Round <= roundLimit)
            {
                TakeTurn(session);

                if (session.Phase != SessionPhase.Over) session.EndTurn();
            }

            return session.Fight.Judge();
        }

        public void TakeTurn(CombatSession session)
        {
            Note?.Invoke($"turn: {session.Phase}, {session.Options().Count} options");

            bool stanced = false;

            for (int step = 0; step < 16 && session.Phase == SessionPhase.Choosing; step++)
            {
                if (!stanced)
                {
                    stanced = true;

                    if (Stance(session)) continue;
                }

                if (Low(session) && Heal(session)) continue;

                if (Strike(session)) continue;

                if (Approach(session)) continue;

                // out of actions with a foe still standing: an Action Surge if there is one
                ActionOption surge = session.Options().FirstOrDefault(o => o.Kind == OptionKind.Surge && o.Enabled);

                if (surge != null && session.Take(surge).Done) continue;

                break;
            }
        }

        bool Low(CombatSession session)
        {
            Health health = session.Hero.Actor.Health;
            return health.Current <= health.Maximum * LowAt;
        }

        static bool Stance(CombatSession session)
        {
            ActionOption stance = session.Options()
                                         .FirstOrDefault(o => o.Enabled && o.Kind == OptionKind.Feature &&
                                                              o.Feature?.Trait == Trait.Stance);

            return stance != null && session.Take(stance).Done;
        }

        bool Heal(CombatSession session)
        {
            foreach (ActionOption option in session.Options().Where(o => o.Enabled))
            {
                if (option.Kind == OptionKind.Item && option.Item != null && !option.Item.Heals.IsNothing ||
                    option.Kind == OptionKind.Feature && option.Feature?.Trait == Trait.Recovery)
                {
                    if (session.Take(option).Done) return true;
                    continue;
                }

                if (option.Kind == OptionKind.Spell && option.Spell.Does(Core.Magic.Primitive.Heal) &&
                    session.Select(option))
                {
                    if (session.LegalTargets().Contains(session.Hero.Actor) &&
                        session.Confirm(session.Hero.Actor).Done)
                        return true;

                    session.Cancel();
                }
            }

            return false;
        }

        // the options and aims the previews rate highest, tried best first until one goes through
        // (a spell can preview well and still be refused - a mode to choose, nothing in the line);
        // a leveled spell has to beat the best weapon by the margin, so the slots last past the
        // first goblin
        bool Strike(CombatSession session)
        {
            var candidates = new List<(ActionOption option, Actor target, Cell? square, Facing facing, double damage)>();

            foreach (ActionOption option in session.Options().Where(o => o.Enabled &&
                                                                        (o.Kind == OptionKind.Attack ||
                                                                         o.Kind == OptionKind.Spell ||
                                                                         o.Kind == OptionKind.Again)))
            {
                if (!session.Select(option)) continue;

                foreach ((Actor target, Cell? square, Facing facing, double damage) aim in Aims(session, option).ToList())
                    if (aim.damage > 0) candidates.Add((option, aim.target, aim.square, aim.facing, aim.damage));

                session.Cancel();
            }

            double bestWeapon = candidates.Where(c => c.option.Kind == OptionKind.Attack)
                                          .Select(c => c.damage)
                                          .DefaultIfEmpty(0)
                                          .Max();

            foreach (var c in candidates.OrderByDescending(c => c.damage)
                                        .ThenBy(c => c.option.Id, StringComparer.Ordinal))
            {
                bool leveled = c.option.Spell != null && c.option.Spell.Level > 0;

                if (leveled && bestWeapon > 0 && c.damage < bestWeapon * SpellMargin) continue;

                if (!session.Select(c.option)) continue;

                session.Point(c.facing);

                ActionResult done = c.square.HasValue
                    ? session.Confirm(c.square.Value)
                    : c.target != null
                        ? session.Confirm(c.target)
                        : session.Confirm();

                Note?.Invoke($"{c.option.Id} at {c.target?.Id ?? c.square?.ToString() ?? c.facing.ToString()} " +
                             $"(~{c.damage:0.0}): {(done.Done ? "done" : done.WhyNotKey)}");

                if (done.Done) return true;

                session.Cancel();
            }

            return false;
        }

        static IEnumerable<(Actor, Cell?, Facing, double)> Aims(CombatSession session, ActionOption option)
        {
            switch (option.Targeting)
            {
                case Targeting.Creature:
                case Targeting.Creatures:
                    foreach (Actor target in session.LegalTargets().Where(t => t.Side != session.Hero.Actor.Side))
                        yield return (target, null, session.Facing, session.Preview(target).ExpectedDamage);
                    break;

                case Targeting.Square:
                {
                    var squares = session.Fight.Field.Enemies(session.Hero.Actor)
                                         .Select(e => session.Fight.Field.Where(e))
                                         .Where(c => c.HasValue && session.LegalSquares().Contains(c.Value))
                                         .Select(c => c.Value)
                                         .ToList();

                    foreach (Cell square in squares)
                    {
                        Preview preview = session.Preview(Array.Empty<Actor>(), square);

                        // a blast that would catch the hero is not worth it
                        if (preview.Targets.Contains(session.Hero.Actor)) continue;

                        yield return (null, square, session.Facing, preview.ExpectedDamage);
                    }

                    break;
                }

                case Targeting.Direction:
                    foreach (Facing facing in Enum.GetValues<Facing>())
                    {
                        session.Point(facing);
                        Preview preview = session.Preview(Array.Empty<Actor>(), null);

                        if (preview.Targets.Contains(session.Hero.Actor)) continue;

                        yield return (null, null, facing, preview.ExpectedDamage);
                    }

                    break;
            }
        }

        // a step toward the nearest foe, as far as the turn allows
        static bool Approach(CombatSession session)
        {
            Encounter fight = session.Fight;
            Actor me = session.Hero.Actor;

            Actor quarry = fight.Field.Enemies(me)
                                .Where(e => !e.IsDown)
                                .OrderBy(e => fight.Field.Distance(me, e))
                                .ThenBy(e => e.Id, StringComparer.Ordinal)
                                .FirstOrDefault();

            if (quarry == null || !(fight.Field.Where(quarry) is Cell there) || !(fight.Field.Where(me) is Cell here))
                return false;

            int now = Battlefield.Distance(here, there);

            if (now <= 1) return false;

            Cell? step = session.Reachable()
                                .Where(p => Battlefield.Distance(p.Key, there) < now)
                                .OrderBy(p => Battlefield.Distance(p.Key, there))
                                .ThenBy(p => p.Value)
                                .Select(p => (Cell?)p.Key)
                                .FirstOrDefault();

            return step.HasValue && session.Move(step.Value).Done;
        }
    }
}
