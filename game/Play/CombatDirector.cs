using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Content.Combat;
using Core.Characters;
using Core.Combat;
using Core.Space;
using Game.Screens;
using Godot;

namespace Game.Play
{
    // THE FIGHT AT THE TABLE (Tier 3a): the board, the HUD, and the hero's hands. Every press is a
    // CombatSession call; the ones that can roll dice go to the rules thread. See docs/combat_ux.md.
    public partial class CombatDirector : Node
    {
        public Game.Board.Board Board { get; set; }

        public Game.Table.TableCamera Camera { get; set; }

        public Game.Table.GmScreen Screen { get; set; }

        public RulesThread Rules { get; set; }

        public TrayDice Dice { get; set; }

        public bool Auto { get; set; }

        public string HeroName { get; set; } = "";

        // the fight is over and every step shown: the director hands the outcome back
        public event Action<Outcome> Finished;

        public CombatSession Session { get; private set; }

        public Battle Battle { get; private set; }

        readonly ConcurrentQueue<BoardShow.Step> _steps = new ConcurrentQueue<BoardShow.Step>();

        CombatHudUi _hud;
        double _wait;
        bool _skipping;
        bool _finished;
        bool _shownMine;
        Cell? _hover;

        public BoardShow Show(Func<Actor, bool> isHero) => new BoardShow(_steps, isHero);

        public void Mount(Control ui)
        {
            _hud = new CombatHudUi(this);
            ui.AddChild(_hud);
            _hud.Visible = false;
        }

        // the rules made the battle; lay the map and stand everyone on it (main thread, rules idle
        // between BattleFor and Start - the positions are the starting ones)
        public void Lay(Battle battle, IReadOnlyList<(Actor Actor, Cell At)> places,
                        IEnumerable<Content.Maps.Prop> props = null)
        {
            Battle = battle;
            _finished = false;

            // a monster stands as its statblock's mini (v1_minis_map.md); the hero as the board's own
            Board.FigureFor = actor => battle.StatblockOf(actor) is { } monster
                ? Game.Board.MiniModels.For(monster.Mini)
                : null;

            Board.ClearAll();
            Board.Lay(battle.Fight.Field.Map);
            Board.Dress(props);
            Screen?.StandBehind(Board);

            foreach ((Actor actor, Cell at) in places) Board.Place(actor, at);

            _hud.Visible = true;
        }

        public void Started(CombatSession session)
        {
            Session = session;
            Refresh();
        }

        public void Clear()
        {
            Session = null;
            Battle = null;
            _hud.Visible = false;
            Board.Unflash();
            Board.ClearAll();

            if (Camera != null) Camera.Following = null;
        }

        bool Presenting => !_steps.IsEmpty || _wait > 0;

        // choosing an option, or aiming the one picked. Targeting is still the hero's move: when it
        // was left out, picking an attack switched off the click that aims it, the right-click and
        // Esc that cancel it, and the bar, and the strip said "enemy turn" (cc_ui_issues_9-25-2026.md)
        bool HerosMove =>
            Session != null && !Rules.Busy && !Presenting &&
            Session.Phase is SessionPhase.Choosing or SessionPhase.Targeting &&
            ReferenceEquals(Session.Turn?.Actor, Session.Hero.Actor);

        double StepSeconds(BoardShow.Step step) =>
            _skipping ? 0
            : step.Hero ? 0.35
            : Math.Max(0.05, GameState.Settings.SecondsPerEnemyStep);

        public override void _Process(double delta)
        {
            if (Session == null && Battle == null) return;

            if (_wait > 0) _wait -= delta;

            while (_wait <= 0 && _steps.TryDequeue(out BoardShow.Step step))
            {
                Play(step);
                _wait = StepSeconds(step);

                if (!_skipping) break;
            }

            if (_steps.IsEmpty && _wait <= 0)
            {
                _skipping = false;

                if (Camera != null && HerosMove) Camera.Following = null;
            }

            // the moment the hero's move begins (the rules idle, the last enemy step shown) the bar,
            // the pips and the reach lights come up; and they go when it ends
            bool mine = HerosMove;

            if (mine != _shownMine)
            {
                _shownMine = mine;
                Refresh();
            }

            if (Session == null || Rules.Busy || Presenting) return;

            if (Session.Phase == SessionPhase.Over && !_finished)
            {
                _finished = true;
                Finished?.Invoke(Session.Fight.Judge());
                return;
            }

            if (HerosMove && Auto)
            {
                var player = new AutoPlayer();
                Rules.Post(() =>
                {
                    player.TakeTurn(Session);
                    if (Session.Phase != SessionPhase.Over) Session.EndTurn();
                }, Refresh);
            }
        }

        void Play(BoardShow.Step step)
        {
            switch (step.What)
            {
                case "turn":
                    if (Camera != null && GameState.Settings.FollowEnemies && !step.Hero && Board.Of(step.Actor) is { } mini)
                        Camera.Following = mini.GlobalPosition;
                    break;

                case "aside":
                    Board.SetAside(step.Actor);
                    break;

                // a single square is a piece put straight down: back from Banishment or a Maze
                case "move" when step.Route.Length == 1:
                    Board.BringBack(step.Actor, step.Route[0]);
                    break;

                case "move":
                    if (step.Route.Length > 1)
                    {
                        if (_skipping) Board.Place(step.Actor, step.Route[^1]);
                        else Board.Walk(step.Actor, step.Route);

                        if (Camera != null && GameState.Settings.FollowEnemies && !step.Hero)
                            Camera.Following = Board.ToGlobal(Board.Where(step.Route[^1]));
                    }
                    break;

                case "strike":
                    if (!_skipping) Board.Strike(step.Actor, step.Other);
                    if (step.Hit && !_skipping) Board.Wobble(step.Other);
                    break;

                case "wobble":
                    if (!_skipping) Board.Wobble(step.Actor);
                    break;

                case "down":
                    Board.Topple(step.Actor);
                    break;

                case "log":
                    _hud?.Write(LogText.Say(step.Line, Battle, HeroName));
                    break;
            }

            _hud?.RefreshStrip();
        }

        public void Refresh()
        {
            _hud?.Refresh();
            ShowSquares();
        }

        // --- what the HUD reads -------------------------------------------------------------------------

        public string Describe() =>
            Session == null
                ? "no fight"
                : $"fight {Session.Phase}, turn {Session.Turn?.Actor.Id ?? "-"}, round {Session.Fight.Round}, " +
                  $"steps {_steps.Count}, selected {Session.Selected?.Id ?? "-"}";

        public bool CanAct => HerosMove;
    }
}
