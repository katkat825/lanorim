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

        // WHAT THE TURN STARTED WITH, so a pip can be drawn hollow once it is spent rather than just
        // disappearing: "Actions ● ○" is one left of two. A Surge's extra is counted in when taken
        public int ActionsGiven =>
            Session.Turn is { } turn ? Math.Max(turn.Actions, turn.Budget.ActionsFor(turn.Round)) : 0;

        public int BonusActionsGiven =>
            Session.Turn is { } turn ? Math.Max(turn.BonusActions, turn.Budget.BonusActionsFor(turn.Round)) : 0;

        public int ReactionsGiven =>
            Session.Turn is { } turn ? Math.Max(Reactions, turn.Budget.ReactionsFor(Fight.Round)) : 0;

        public int SquaresMoved => Session.Turn?.SquaresMoved ?? 0;

        public int SquaresGiven => Session.Turn?.SquaresGiven ?? 0;

        // the pips as words: a filled dot for each one left, a hollow one for each spent
        public static string Pips(int left, int given) =>
            new string('●', Math.Max(0, left)) + new string('○', Math.Max(0, given - left));

        // the action bar: the session's options, numbered 1-9 the HUD's way (below). read once per HUD,
        // so the three parts below are the same options
        public IReadOnlyList<ActionOption> Bar => _bar ??= Numbered(HerosTurn ? Session.Options() : Array.Empty<ActionOption>());

        IReadOnlyList<ActionOption> _bar;

        // THE BAR IN THREE PARTS (Kathleen, 2026-09-28): a caster's options wrapped to three rows and
        // covered the board. What a turn reaches for stays a button - attacks, features, items, a
        // shape, Surge, Flee; the spells are one menu and the manoeuvres every creature has another,
        // so the bar is one row. The hotkeys don't move
        public IReadOnlyList<ActionOption> Buttons =>
            Bar.Where(o => o.Kind != OptionKind.EndTurn && !IsSpell(o) && !IsManoeuvre(o)).ToList();

        public IReadOnlyList<ActionOption> Spells => Bar.Where(IsSpell).ToList();

        public IReadOnlyList<ActionOption> Manoeuvres => Bar.Where(IsManoeuvre).ToList();

        static bool IsSpell(ActionOption o) => o.Kind is OptionKind.Spell or OptionKind.Again;

        static bool IsManoeuvre(ActionOption o) =>
            o.Kind is OptionKind.Dash or OptionKind.Disengage or OptionKind.Hide or OptionKind.Grapple or
                OptionKind.Shove or OptionKind.StandUp or OptionKind.BreakFree or OptionKind.Shake;

        // THE HOTKEYS, THE HUD'S WAY (docs/combat_ux.md, Keys): the bar's buttons from the left, then the
        // Spells menu, then More actions - so the buttons on the bar are 1, 2, 3 and not "1, 2, 9" with the
        // manoeuvres in between. An option counts whether or not it can be taken right now, so a hidden or
        // greyed one keeps its number and nothing renumbers under the player's hand mid-turn; the ones that
        // only turn up partway through a turn (a Light weapon's bonus attack, Flee on an open edge) come
        // last, for the same reason. The session's own order (Options) is left alone: the AutoPlayer and
        // the sim read it
        static IReadOnlyList<ActionOption> Numbered(IReadOnlyList<ActionOption> options)
        {
            List<ActionOption> buttons = options.Where(o => o.Kind != OptionKind.EndTurn && !IsSpell(o) && !IsManoeuvre(o)).ToList();

            IEnumerable<ActionOption> order = buttons.Where(o => !Late(o))
                                                     .Concat(options.Where(IsSpell))
                                                     .Concat(options.Where(IsManoeuvre))
                                                     .Concat(buttons.Where(Late));

            int key = 1;

            foreach (ActionOption option in order) option.Hotkey = key <= 9 ? key++ : 0;

            return options;
        }

        static bool Late(ActionOption o) => o.Kind == OptionKind.Flee || o.Id.StartsWith("bonus_attack:", StringComparison.Ordinal);

        public ActionOption Hotkey(int key) =>
            key < 1 || key > 9 ? null : Bar.FirstOrDefault(o => o.Hotkey == key);

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
        public static readonly string SpellsMenuKey = ScreenKeys.Key("combat", "spells_menu");
        public static readonly string ManoeuvresMenuKey = ScreenKeys.Key("combat", "manoeuvres_menu");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                EndTurnKey, ActionsKey, BonusKey, ReactionKey, MovementKey, RoundKey, LogKey,
                EnemyTurnKey, SkipKey, FleeKey, HitChanceKey, SaveChanceKey, ExpectedDamageKey,
                PathCostKey, YesKey, NoKey, SpellsMenuKey, ManoeuvresMenuKey,
            };
    }
}
