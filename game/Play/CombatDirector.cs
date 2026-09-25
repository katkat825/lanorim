using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Screens;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Rules;
using Core.Space;
using Game.Screens;
using Godot;

namespace Game.Play
{
    // WHAT THE FIGHT SHOWS, PACED. The rules run ahead on their own thread and tell this observer what
    // happened; each thing becomes a step - a mini moving, a strike, a fall, a line of the log - that
    // the director plays one after another at the enemy-turn speed. So the rules never wait on an
    // animation, and the table never shows something before it happened.
    public sealed class BoardShow : CombatObserver
    {
        public sealed class Step
        {
            public string What;
            public Actor Actor;
            public Actor Other;
            public Cell[] Route;
            public bool Hit;
            public LogLine Line;
            public bool Hero;
        }

        readonly ConcurrentQueue<Step> _steps;
        readonly Func<Actor, bool> _isHero;

        public BoardShow(ConcurrentQueue<Step> steps, Func<Actor, bool> isHero)
        {
            _steps = steps;
            _isHero = isHero;
        }

        void Add(Step step) => _steps.Enqueue(step);

        public override void TurnBegan(Turn turn) =>
            Add(new Step { What = "turn", Actor = turn.Actor, Hero = _isHero(turn.Actor) });

        public override void Moved(Actor actor, IReadOnlyList<Cell> route) =>
            Add(new Step { What = "move", Actor = actor, Route = route?.ToArray() ?? Array.Empty<Cell>(), Hero = _isHero(actor) });

        public override void Struck(Blow blow) =>
            Add(new Step { What = "strike", Actor = blow.Attacker, Other = blow.Target, Hit = blow.Hit, Hero = _isHero(blow.Attacker) });

        public override void ConditionChanged(Actor actor, Condition condition, bool applied)
        {
            if (applied) Add(new Step { What = "wobble", Actor = actor });
        }

        public override void Downed(Actor actor) => Add(new Step { What = "down", Actor = actor });

        // Banishment, Maze: off the board and beside it, and back. the square it comes back to
        // follows as a Moved of one square
        public override void Away(Actor actor, bool away)
        {
            if (away) Add(new Step { What = "aside", Actor = actor });
        }

        public void Log(LogLine line) => Add(new Step { What = "log", Line = line });
    }

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

        bool HerosMove =>
            Session != null && !Rules.Busy && !Presenting && Session.Phase == SessionPhase.Choosing &&
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

        // --- the hero's hands -------------------------------------------------------------------------

        void ShowSquares()
        {
            Board.Unflash();

            if (!HerosMove) return;

            if (Session.Selected == null)
            {
                Board.Flash(Session.Reachable().Keys, new Color(0.35f, 0.55f, 0.85f, 0.45f));
                return;
            }

            switch (Session.Selected.Targeting)
            {
                case Targeting.Creature:
                case Targeting.Creatures:
                    Board.Flash(Session.LegalTargets().Select(a => Session.Fight.Field.Where(a))
                                       .Where(c => c.HasValue).Select(c => c.Value),
                                new Color(0.85f, 0.3f, 0.25f, 0.5f));
                    break;

                case Targeting.Square:
                case Targeting.Direction:
                    Preview preview = Session.Preview(Array.Empty<Actor>(), _hover);
                    Board.Flash(preview.Template, new Color(0.9f, 0.55f, 0.2f, 0.5f));
                    break;
            }
        }

        public void Pick(ActionOption option)
        {
            if (!HerosMove || option == null || !option.Enabled) return;

            if (option.Targeting == Targeting.None && option.Kind != OptionKind.Spell && option.Kind != OptionKind.Again)
            {
                Rules.Post(() => Session.Take(option), Refresh);
                return;
            }

            Session.Select(option);

            // a spell that aims at nothing (Shield on yourself, a self-centred aura) goes at once
            if (option.Targeting == Targeting.None)
            {
                Rules.Post(() => Session.Confirm(), Refresh);
                return;
            }

            Refresh();
        }

        public void EndTurn()
        {
            if (!HerosMove) return;

            Rules.Post(() => Session.EndTurn(), Refresh);
        }

        public void Skip()
        {
            if (Presenting) _skipping = true;
        }

        void Cancel()
        {
            Session.Cancel();
            _hud?.Preview(null);
            Refresh();
        }

        void Click(Cell cell)
        {
            ActionOption selected = Session.Selected;

            if (selected == null)
            {
                if (Session.Reachable().ContainsKey(cell)) Rules.Post(() => Session.Move(cell), Refresh);
                return;
            }

            switch (selected.Targeting)
            {
                case Targeting.Creature:
                case Targeting.Creatures:
                {
                    Actor target = Session.LegalTargets().FirstOrDefault(a => Session.Fight.Field.Where(a) == cell);

                    if (target != null) Rules.Post(() => Session.Confirm(target), Refresh);
                    break;
                }

                case Targeting.Square:
                    if (Session.LegalSquares().Contains(cell)) Rules.Post(() => Session.Confirm(cell), Refresh);
                    break;

                case Targeting.Direction:
                    Rules.Post(() => Session.Confirm(), Refresh);
                    break;
            }
        }

        void Hover(Cell? cell)
        {
            if (cell == _hover) return;

            _hover = cell;

            if (!HerosMove) return;

            ActionOption selected = Session.Selected;

            if (selected == null)
            {
                ShowSquares();

                if (cell.HasValue && Session.Reachable().TryGetValue(cell.Value, out int cost))
                {
                    Board.Flash(Session.PathTo(cell.Value), new Color(0.95f, 0.85f, 0.4f, 0.6f));
                    _hud?.Preview(Ui.Say(ScreenKeys.Key("combat", "path_cost"), cost));
                }
                else _hud?.Preview(null);

                return;
            }

            if (selected.Targeting is Targeting.Creature or Targeting.Creatures)
            {
                Actor target = cell.HasValue
                    ? Session.LegalTargets().FirstOrDefault(a => Session.Fight.Field.Where(a) == cell.Value)
                    : null;

                _hud?.Preview(target == null ? null : Describe(Session.Preview(target)));
                return;
            }

            ShowSquares();
            _hud?.Preview(Describe(Session.Preview(Array.Empty<Actor>(), cell)));
        }

        static string Describe(Preview preview)
        {
            if (preview == null) return null;

            var parts = new List<string>();

            if (preview.HitChance >= 0) parts.Add(Ui.Say(CombatHud.HitChanceKey, Math.Round(preview.HitChance * 100)));
            if (preview.FailChance >= 0) parts.Add(Ui.Say(CombatHud.SaveChanceKey, Math.Round(preview.FailChance * 100)));
            if (preview.Damage.RollsAnything) parts.Add(preview.Damage.ToString());
            parts.Add(Ui.Say(CombatHud.ExpectedDamageKey, preview.ExpectedDamage.ToString("0.0")));

            return string.Join("   ", parts);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Session == null) return;

            if (@event is InputEventMouseMotion motion)
            {
                Hover(Board.CellUnder(motion.Position));
                return;
            }

            if (!HerosMove) return;

            if (@event is InputEventMouseButton { Pressed: true } click)
            {
                if (click.ButtonIndex == MouseButton.Right && Session.Selected != null)
                {
                    Cancel();
                    GetViewport().SetInputAsHandled();
                }
                else if (click.ButtonIndex == MouseButton.Left && Board.CellUnder(click.Position) is Cell cell)
                {
                    Click(cell);
                    GetViewport().SetInputAsHandled();
                }
                else if (Session.Selected?.Targeting == Targeting.Direction &&
                         click.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
                {
                    Session.Rotate(click.ButtonIndex == MouseButton.WheelUp ? 1 : -1);
                    ShowSquares();
                    _hud?.Preview(Describe(Session.Preview(Array.Empty<Actor>(), null)));
                    GetViewport().SetInputAsHandled();
                }

                return;
            }

            if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

            if (key.IsActionPressed("ui_cancel") && Session.Selected != null)
            {
                Cancel();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (Session.Selected?.Targeting == Targeting.Direction &&
                (key.IsActionPressed("turn_left") || key.IsActionPressed("turn_right")))
            {
                Session.Rotate(key.IsActionPressed("turn_left") ? -1 : 1);
                ShowSquares();
                _hud?.Preview(Describe(Session.Preview(Array.Empty<Actor>(), null)));
                GetViewport().SetInputAsHandled();
                return;
            }

            if (Session.Selected?.Targeting is Targeting.Direction && key.Keycode == Key.Enter)
            {
                Rules.Post(() => Session.Confirm(), Refresh);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9)
            {
                Pick(new CombatHud(Session).Hotkey((int)(key.Keycode - Key.Key0)));
                GetViewport().SetInputAsHandled();
            }
        }

        // Space when nothing is waiting on the tray: End Turn on the hero's turn, skip otherwise
        public bool GoOn()
        {
            if (Session == null) return false;

            if (HerosMove)
            {
                EndTurn();
                return true;
            }

            if (Presenting)
            {
                Skip();
                return true;
            }

            return false;
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
