using System;
using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Combat;
using Core.Localization;

namespace Content.Screens
{
    public sealed class TurnChip
    {
        public Actor Actor { get; init; }

        // a monster's statblock name; the hero's is the name the player typed (NameKey null)
        public string NameKey { get; init; }

        public string Name { get; init; } = "";

        public bool Hero { get; init; }

        public bool Current { get; init; }

        public int Initiative { get; init; }

        public int HitPoints { get; init; }

        public int MaxHitPoints { get; init; }

        public bool Down { get; init; }
    }

    // THE COMBAT HUD (Tier 2.8, docs/combat_ux.md): the turn-order strip, the action / bonus /
    // reaction pips and the movement left, the action bar on 1-9 with each greyed option's reason,
    // and End Turn. Everything it shows is read off the CombatSession; nothing here decides a rule.
    public sealed class CombatHud
    {
        public CombatHud(CombatSession session, FightLog log = null)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Log = log;
        }

        public CombatSession Session { get; }

        public FightLog Log { get; }

        Encounter Fight => Session.Fight;

        public int Round => Fight.Round;

        public IReadOnlyList<TurnChip> Order
        {
            get
            {
                Actor now = Session.Turn?.Actor;

                return Fight.Order
                            .Select(roll => new TurnChip
                            {
                                Actor = roll.Actor,
                                Hero = roll.Actor == Session.Hero.Actor,
                                Name = roll.Actor == Session.Hero.Actor ? Session.Hero.Name : "",
                                NameKey = Session.Battle.StatblockOf(roll.Actor) is { } monster
                                              ? KeyConventions.MonsterName(monster.Id)
                                              : null,
                                Current = roll.Actor == now,
                                Initiative = roll.Total,
                                HitPoints = roll.Actor.Health.Current,
                                MaxHitPoints = roll.Actor.Health.Maximum,
                                Down = roll.Actor.Health.Current <= 0,
                            })
                            .ToList();
            }
        }

        public bool HerosTurn => Session.Phase != SessionPhase.Waiting && Session.Turn != null &&
                                 Session.Turn.Actor == Session.Hero.Actor;

        public int Actions => Session.Turn?.Actions ?? 0;

        public int BonusActions => Session.Turn?.BonusActions ?? 0;

        public int Reactions => Fight.ReactionsLeft(Session.Hero.Actor);

        public int SquaresLeft => Session.Turn?.SquaresLeft ?? 0;

        // the action bar: the session's options, with 1-9 on the first nine
        public IReadOnlyList<ActionOption> Bar => HerosTurn ? Session.Options() : Array.Empty<ActionOption>();

        public ActionOption Hotkey(int key) =>
            key < 1 || key > 9 ? null : Bar.FirstOrDefault(o => o.Hotkey == key);

        // the log, newest last; the panel shows the last few and opens to the rest
        public IReadOnlyList<LogLine> LastLines(int count) =>
            Log == null
                ? Array.Empty<LogLine>()
                : Log.Lines.Skip(Math.Max(0, Log.Lines.Count - count)).ToList();

        public static readonly string EndTurnKey = ScreenKeys.Key("combat", "end_turn");
        public static readonly string ActionsKey = ScreenKeys.Key("combat", "actions");
        public static readonly string BonusKey = ScreenKeys.Key("combat", "bonus_action");
        public static readonly string ReactionKey = ScreenKeys.Key("combat", "reaction");
        public static readonly string MovementKey = ScreenKeys.Key("combat", "movement");
        public static readonly string RoundKey = ScreenKeys.Key("combat", "round");
        public static readonly string LogKey = ScreenKeys.Key("combat", "log");
        public static readonly string EnemyTurnKey = ScreenKeys.Key("combat", "enemy_turn");
        public static readonly string SkipKey = ScreenKeys.Key("combat", "skip");
        public static readonly string FleeKey = ScreenKeys.Key("combat", "flee_edge");
        public static readonly string HitChanceKey = ScreenKeys.Key("combat", "hit_chance");
        public static readonly string SaveChanceKey = ScreenKeys.Key("combat", "save_chance");
        public static readonly string ExpectedDamageKey = ScreenKeys.Key("combat", "expected_damage");
        public static readonly string PathCostKey = ScreenKeys.Key("combat", "path_cost");
        public static readonly string YesKey = ScreenKeys.Key("combat", "yes");
        public static readonly string NoKey = ScreenKeys.Key("combat", "no");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                EndTurnKey, ActionsKey, BonusKey, ReactionKey, MovementKey, RoundKey, LogKey,
                EnemyTurnKey, SkipKey, FleeKey, HitChanceKey, SaveChanceKey, ExpectedDamageKey,
                PathCostKey, YesKey, NoKey,
            };
    }
}
