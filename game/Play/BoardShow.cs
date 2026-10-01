using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
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
            public Border? Door;
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

        // PRONE IS A PLACE ON THE BOARD, NOT A DEATH (cc_task_working-notes-10-01.md 1.2): Prone lays the piece
        // down in its square and its end stands it back up. Unconscious brings Prone with it (SRD 5.2.1), so it
        // lays the piece down too; every other condition is a wobble
        public override void ConditionChanged(Actor actor, Condition condition, bool applied)
        {
            if (condition is Condition.Prone or Condition.Unconscious)
            {
                if (applied || condition == Condition.Prone) Add(new Step { What = applied ? "prone" : "stand", Actor = actor });
                return;
            }

            if (applied) Add(new Step { What = "wobble", Actor = actor });
        }

        // a monster at 0 is out, and topples (death); a hero at 0 is unconscious and Prone, and lies in its
        // square until the death save says otherwise
        public override void Downed(Actor actor)
        {
            bool hero = _isHero(actor);
            Add(new Step { What = hero ? "prone" : "down", Actor = actor, Hero = hero });
        }

        // the hero's save failed: that is the death, and the topple that never stands up
        public override void DeathSaved(Actor actor, Attempt attempt)
        {
            if (!attempt.Succeeded) Add(new Step { What = "down", Actor = actor, Hero = _isHero(actor) });
        }

        // a shut door opened (Encounter.Doors.cs): it swings on the board
        public override void DoorOpened(Actor actor, Border door) =>
            Add(new Step { What = "door", Actor = actor, Door = door, Hero = _isHero(actor) });

        // Banishment, Maze: off the board and beside it, and back. the square it comes back to
        // follows as a Moved of one square
        public override void Away(Actor actor, bool away)
        {
            if (away) Add(new Step { What = "aside", Actor = actor });
        }

        public void Log(LogLine line) => Add(new Step { What = "log", Line = line });

        // hit, miss, a save made or failed: told at once rather than paced, because the hero's damage
        // throw is asked for right behind it and the tray's caption should say why (rules thread)
        public event Action<Actor, Actor, Attempt> Judging;

        public override void Judged(Actor by, Actor target, Attempt attempt) => Judging?.Invoke(by, target, attempt);

        // a spell cast and resolved: who cast what, at whom, and what it changed - for the tray's
        // caption, told at once like a verdict (rules thread). A Shield cast inside a spell's attack
        // is resolved inside it, so who each cast was aimed at is kept on a stack
        public event Action<Actor, string, IReadOnlyList<Actor>, IReadOnlyList<Change>> Casting;

        readonly Stack<IReadOnlyList<Actor>> _aimedAt = new Stack<IReadOnlyList<Actor>>();

        public override void Casts(Actor caster, string spellKey, IReadOnlyList<Actor> at) =>
            _aimedAt.Push(at ?? Array.Empty<Actor>());

        public override void Cast(Actor caster, string spellKey, IReadOnlyList<Change> changes) =>
            Casting?.Invoke(caster, spellKey, _aimedAt.Count > 0 ? _aimedAt.Pop() : Array.Empty<Actor>(),
                            changes ?? Array.Empty<Change>());
    }
}
