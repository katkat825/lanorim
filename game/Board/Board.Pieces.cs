using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Space;
using Godot;

namespace Game.Board
{
    public partial class Board
    {
        // --- the pieces ---------------------------------------------------------------------

        // put a piece on the board for an actor. the same actor twice moves the piece it has
        public Mini Place(Actor actor, Cell at)
        {
            if (actor == null) return null;

            if (!_minis.TryGetValue(actor, out Mini mini))
            {
                mini = Make(actor);

                if (mini == null) return null;

                _minis[actor] = mini;
            }

            mini.PlaceAt(Metrics.Centre(at));

            return mini;
        }

        // WHICH MODEL A PIECE IS, when something knows better than its allegiance: the fight asks the
        // statblock's mini (MiniModels). Null, or an answer of null, and it is HeroFigure/EnemyFigure
        public System.Func<Actor, (PackedScene Model, float Height)?> FigureFor { get; set; }

        public Mini Of(Actor actor) =>
            actor != null && _minis.TryGetValue(actor, out Mini mini) ? mini : null;

        Mini Make(Actor actor)
        {
            if (MiniScene == null)
            {
                GD.PushError("board: no mini scene - there is nothing to stand on the map");
                return null;
            }

            var mini = MiniScene.Instantiate<Mini>();

            if (mini == null)
            {
                GD.PushError("board: the mini scene is not a Mini");
                return null;
            }

            mini.Name = actor.Id;
            mini.Paint = Paint;
            mini.CellSize = Metrics.CellSize;

            // set before the piece enters the tree: Mini.Stand runs in _Ready and measures whatever
            // figure it finds, so a model handed over after AddChild would be scaled off the
            // stand-in's height rather than its own
            mini.FigureModel = actor.Side == Allegiance.Hero ? HeroFigure : EnemyFigure;

            if (FigureFor?.Invoke(actor) is { } figure)
            {
                mini.FigureModel = figure.Model;
                mini.FigureHeight = figure.Height;
            }

            _miniRoot.AddChild(mini);

            return mini;
        }

        // A MINI IS AN OBJECT THAT GETS MOVED. It arcs up, travels, sets down - it does not walk,
        // because a painted miniature has no legs that work (ART_DIRECTION section 5).
        public bool Walk(Actor actor, IReadOnlyList<Cell> route)
        {
            Mini mini = Of(actor);

            if (mini == null || route == null || route.Count < 2) return false;

            mini.Follow(route.Select(Metrics.Centre).ToList());

            return true;
        }

        public void Strike(Actor attacker, Actor target)
        {
            Mini mini = Of(attacker);
            Mini at = Of(target);

            if (mini == null || at == null) return;

            mini.Strike(at.Position);
        }

        // it took a condition: a wobble and a scuff, so the player reads the state off the figure
        // and not only off a bar
        public void Wobble(Actor actor) => Of(actor)?.Wobble();

        // and it went over. THE BODY STAYS ON THE MAP
        public void Topple(Actor actor) => Of(actor)?.Topple();

        // Prone: laid down inside its own square; and stood back up, upright and centred (Mini.Prone.cs)
        public void LieDown(Actor actor) => Of(actor)?.LieDown();

        public void StandUp(Actor actor) => Of(actor)?.GetUp();

        // OFF THE BOARD FOR A WHILE (Banishment, Maze): the piece stands on the table beside the map,
        // just past its east edge, one square down for each piece already there, until the spell
        // ends and it is Placed back on its square
        public void SetAside(Actor actor)
        {
            Mini mini = Of(actor);

            if (mini == null || Metrics == null) return;

            int already = _aside.Count(a => !ReferenceEquals(a, actor));

            if (!_aside.Contains(actor)) _aside.Add(actor);

            mini.PlaceAt(Metrics.Centre(new Cell(Metrics.Columns, Mathf.Min(already, Metrics.Rows - 1))) +
                         new Vector3(Metrics.CellSize * 0.5f, 0f, 0f));
        }

        public void BringBack(Actor actor, Cell at)
        {
            _aside.Remove(actor);
            Place(actor, at);
        }

        readonly List<Actor> _aside = new();

        public void Clear(Actor actor)
        {
            if (actor == null || !_minis.Remove(actor, out Mini mini)) return;

            mini.QueueFree();
        }

        public void ClearAll()
        {
            _aside.Clear();

            foreach (Mini mini in _minis.Values) mini.QueueFree();

            _minis.Clear();
        }
    }
}
