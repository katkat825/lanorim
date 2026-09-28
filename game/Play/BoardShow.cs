using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Combat;
using Core.Rules;
using Core.Space;

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
}
