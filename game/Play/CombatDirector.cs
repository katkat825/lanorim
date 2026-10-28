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

        public Game.Table.CompanionFigure Companion { get; set; }

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

        // a line of the table's own in the fight's log: the companion's bark (cc_task_i 2.4)
        public void Write(string line) => _hud?.Write(line);

        // what the log has shown this fight (CastProbe)
        public System.Collections.Generic.IReadOnlyList<string> LogWritten =>
            _hud?.Written ?? (System.Collections.Generic.IReadOnlyList<string>)Array.Empty<string>();

        public void Mount(Control ui)
        {
            _hud = new CombatHudUi(this);
            ui.AddChild(_hud);
            _hud.Visible = false;
        }

        // the rules made the battle; lay the map and stand everyone on it (main thread, rules idle
        // between BattleFor and Start - the positions are the starting ones)
        // what stands on the map, for the named NPC under the pointer (cc_task_m3 Part 10)
        List<Content.Maps.Prop> _props = new();

        public void Lay(Battle battle, IReadOnlyList<(Actor Actor, Cell At)> places,
                        IEnumerable<Content.Maps.Prop> props = null, Content.Maps.MapLook look = null)
        {
            Battle = battle;
            _finished = false;

            // a monster stands as its statblock's mini (v1_minis_map.md); the hero as its class's (HeroMinis,
            // task G), or the board's own when that's switched off
            Board.FigureFor = actor => battle.StatblockOf(actor) is { } monster
                ? Game.Board.MiniModels.For(monster.Mini, actor.Id)
                : GameState.Run?.Hero is { } hero && ReferenceEquals(hero.Actor, actor)
                    ? Game.Board.HeroMinis.For(hero)
                    : null;

            _hud.ClearLog();

            Board.ClearAll();
            Board.Lay(battle.Fight.Field.Map, look);
            Board.Dress(props);
            _props = props == null ? new List<Content.Maps.Prop>() : new List<Content.Maps.Prop>(props);
            Screen?.StandBehind(Board);
            Companion?.StandBy(Board, Screen);

            foreach ((Actor actor, Cell at) in places)
            {
                Board.Place(actor, at);

                // a fight resumed from a save: whoever lay down when it was saved is lying down still
                if (actor.Has(Condition.Prone)) Board.LieDown(actor);
            }

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

        // the board is still playing back what the rules already did: the turns, the blows, the falls
        public bool Presenting => !_steps.IsEmpty || _wait > 0;

        // whose turn the table is playing back while it is behind the rules; null when it has caught up
        Actor _showing;

        public Actor TurnShown => Presenting ? _showing : null;

        // the hit points the table has shown so far, while it is behind the rules (BoardShow.Step.Health); null is the
        // live number
        readonly System.Collections.Generic.Dictionary<Actor, int> _health = new();

        public int? HealthShown(Actor actor) => Presenting && _health.TryGetValue(actor, out int shown) ? shown : null;

        bool _wasPresenting;

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

            // caught up: the strip goes back to the live numbers
            if (_wasPresenting && !Presenting) _hud?.RefreshStrip();
            _wasPresenting = Presenting;

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
            if (step.Health != null)
                foreach (var (actor, hitPoints) in step.Health) _health[actor] = hitPoints;

            switch (step.What)
            {
                case "turn":
                    _showing = step.Actor;

                    if (Camera != null && GameState.Settings.FollowEnemies && !step.Hero && Board.Of(step.Actor) is { } mini)
                        Camera.Following = mini.GlobalPosition;
                    break;

                case "aside":
                    Board.SetAside(step.Actor);
                    break;

                // the keepsake held the hero up (cc_task_h 3.2): shown here, after the blow that would have dropped
                // them, so the companion startles at the right moment rather than ahead of the board
                case "held":
                    if (!_skipping) Board.Wobble(step.Actor);
                    Companion?.Feel(Content.Companions.Mood.Alert);
                    break;

                // ran off the map (cc_task_h 3.4): gone from the board, and nothing to clear after it
                case "left":
                    if (Board.Of(step.Actor) is { } fled) fled.Visible = false;
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

                // a Command's word taking the turn (cc_task_m3 Part 2)
                case "obey":
                    if (!_skipping && Board.Of(step.Actor) != null)
                    {
                        Board.Wobble(step.Actor);
                        if (Battle?.Fight.Field.Where(step.Actor) is Core.Space.Cell square) Board.Pulse(square);
                    }
                    break;

                // what a narrated spell shows on its square (Board.Sight)
                case "sight" when step.At is Core.Space.Cell at:
                    if (!_skipping) Board.Sight(step.Spell, at);
                    break;

                case "prone":
                    Board.LieDown(step.Actor);
                    break;

                case "stand":
                    Board.StandUp(step.Actor);
                    break;

                case "door" when step.Door is Border door && Battle != null:
                    Board.Reopen(Battle.Fight.Field.Map, door);
                    break;

                // a fallen foe is cleared off the mat by the companion; the hero stays where they fell
                case "down":
                    Board.Topple(step.Actor);
                    if (!step.Hero) Companion?.Huff(Board.Of(step.Actor));
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
